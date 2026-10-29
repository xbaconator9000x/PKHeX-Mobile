"""Fail-closed source-level checks for the pinned preview-only PKForge adaptation.

The CI compile-probe separately compiles the adapted MAUI app. Portable C#
grant-policy assertions run against the generated policy source.
"""
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from scripts.pkforge_official_core_probe import PKFORGE, ProbeRefusal
from scripts.pkforge_preview_only_hardening import (
    ACCESS, GRANTS, GRANTS_SOURCE, PICKER, VIEWMODEL,
    adapt_access, adapt_picker, adapt_viewmodel, prepare,
)

PICKER_SOURCE = """using Android.Content;
intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission | ActivityFlags.GrantPersistableUriPermission);
intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantPersistableUriPermission);
"""

ACCESS_SOURCE = """using Android.OS;
    public ValueTask WriteAtomicallyAsync(string documentId, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default)
    {
        var uri = Parse(documentId);
        cancellationToken.ThrowIfCancellationRequested();

        // SAF providers do not expose a portable rename-and-swap primitive. The descriptor must support
        // reliable truncate/write/flush; providers that reject this mode fail closed.
        using var descriptor = Resolver.OpenFileDescriptor(uri, "rwt")
            ?? throw new IOException($"The document provider does not support safe truncate/write access for {uri}.");
        var output = new ParcelFileDescriptor.AutoCloseOutputStream(descriptor);
        cancellationToken.ThrowIfCancellationRequested();
        output.Write(bytes.ToArray());
        output.Flush();
        output.Close();
        return ValueTask.CompletedTask;
    }

    public static void PersistPermission(Android.Net.Uri uri, Intent intent)
    {
        var grants = intent.Flags & (ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);
        if (grants == 0)
            throw new InvalidOperationException("The picker result did not grant document access.");
        Resolver.TakePersistableUriPermission(uri, grants);
    }
"""

VIEWMODEL_SOURCE = """    [RelayCommand]
    private async Task SaveEditAsync()
    {
        if (IsBusy) return;
        engineSession.ApplyEdit(detail.Box, detail.Slot, edit);
    }

    public async Task<bool> RunMutationAsync(Func<ISaveEngineSession, GenerationOutcome> operation, int slot,
        bool refreshSlot = true, string? changeDescription = null, SaveAction action = SaveAction.EditMon)
    {
        if (HardcoreMode.Blocks(action, out var hardcoreStatus))
        {
        }
        var outcome = await Task.Run(() => operation(engineSession));
    }

    public bool BeginCarry()
    {
        if (Save is null || SelectedSlot < 0) return false;
    }

    public async Task DropAsync()
    {
        if (CarrySource is null || _dropWaiting) return;
        engineSession.MoveSlot(source.Box, source.Slot, target.Box, target.Slot);
    }
"""


class PreviewOnlyHardeningTests(unittest.TestCase):
    def test_single_picker_and_multi_picker_request_read_only(self):
        adapted = adapt_picker(PICKER_SOURCE)
        self.assertIn("ReadOnlySourceGrants.RequestedFlags", adapted)
        self.assertNotIn("GrantWriteUriPermission", adapted)
        self.assertIn("intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantPersistableUriPermission);", adapted)
        with self.assertRaises(ProbeRefusal):
            adapt_picker(adapted)
        with self.assertRaises(ProbeRefusal):
            adapt_picker(PICKER_SOURCE + PICKER_SOURCE)

    def test_direct_android_writer_and_persisted_write_grants_are_denied(self):
        adapted = adapt_access(ACCESS_SOURCE)
        self.assertNotIn('OpenFileDescriptor(uri, "rwt")', adapted)
        self.assertNotIn("GrantWriteUriPermission", adapted)
        self.assertIn("UnsafeSaveWriteException(ReadOnlySaveWriter.Reason)", adapted)
        self.assertIn("ReadOnlySourceGrants.PersistedFlags((int)intent.Flags)", adapted)
        with self.assertRaises(ProbeRefusal):
            adapt_access(adapted)
        with self.assertRaises(ProbeRefusal):
            adapt_access(ACCESS_SOURCE.replace("output.Flush();", "output.Flush(true);"))

    def test_all_live_mutation_families_guarded_before_execution(self):
        adapted = adapt_viewmodel(VIEWMODEL_SOURCE)
        self.assertEqual(adapted.count("RefuseLiveMutation()"), 5)
        self.assertLess(adapted.index("if (RefuseLiveMutation()) return;"),
                        adapted.index("engineSession.ApplyEdit("))
        self.assertLess(adapted.index("if (RefuseLiveMutation()) return false;"),
                        adapted.index("operation(engineSession)"))
        self.assertLess(adapted.index("if (RefuseLiveMutation())\n        {"),
                        adapted.index("engineSession.MoveSlot("))
        self.assertIn("CarrySource = null;", adapted)
        self.assertIn("No save was changed.", adapted)
        with self.assertRaises(ProbeRefusal):
            adapt_viewmodel(adapted)
        with self.assertRaises(ProbeRefusal):
            adapt_viewmodel(VIEWMODEL_SOURCE.replace("engineSession.MoveSlot(", "engineSession.SwapSlot("))

    def test_preflight_is_atomic_on_changed_source(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            for path, body in ((PICKER, PICKER_SOURCE), (ACCESS, ACCESS_SOURCE),
                               (VIEWMODEL, VIEWMODEL_SOURCE)):
                target = root / path
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_text(body, encoding="utf-8")
            (root / GRANTS).parent.mkdir(parents=True, exist_ok=True)
            picker = root / PICKER
            before = picker.read_bytes()
            vm = root / VIEWMODEL
            vm.write_text(VIEWMODEL_SOURCE.replace("BeginCarry()", "StartCarry()"))
            with patch("scripts.pkforge_preview_only_hardening.sha", return_value=PKFORGE):
                with self.assertRaises(ProbeRefusal):
                    prepare(root)
            self.assertEqual(picker.read_bytes(), before)
            self.assertFalse((root / GRANTS).exists())
            vm.write_text(VIEWMODEL_SOURCE)
            with patch("scripts.pkforge_preview_only_hardening.sha", return_value=PKFORGE):
                verdict = prepare(root)
            self.assertFalse(verdict["live_mutations_enabled"])
            self.assertFalse(verdict["persisted_source_write_grant"])
            self.assertEqual((root / GRANTS).read_text(encoding="utf-8"), GRANTS_SOURCE)
            with patch("scripts.pkforge_preview_only_hardening.sha", return_value=PKFORGE):
                with self.assertRaises(ProbeRefusal):
                    prepare(root)

    def test_unpinned_or_symlinked_input_refused(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            for path, body in ((PICKER, PICKER_SOURCE), (ACCESS, ACCESS_SOURCE),
                               (VIEWMODEL, VIEWMODEL_SOURCE)):
                target = root / path
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_text(body, encoding="utf-8")
            with patch("scripts.pkforge_preview_only_hardening.sha", return_value="wrong"):
                with self.assertRaises(ProbeRefusal):
                    prepare(root)
            target = root / PICKER
            original = root / "actual.cs"
            original.write_text(PICKER_SOURCE)
            target.unlink()
            target.symlink_to(original)
            with patch("scripts.pkforge_preview_only_hardening.sha", return_value=PKFORGE):
                with self.assertRaises(ProbeRefusal):
                    prepare(root)


if __name__ == "__main__":
    unittest.main()
