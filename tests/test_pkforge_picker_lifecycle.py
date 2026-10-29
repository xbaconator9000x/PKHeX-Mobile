"""Pinned source-level tests for cancellation and late Android picker results."""
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from scripts.pkforge_official_core_probe import PKFORGE, ProbeRefusal
from scripts.pkforge_picker_lifecycle import PICKER, adapt_picker, prepare

PICKER_SOURCE = """using Android.Content;
    public ValueTask<PickedDocument?> PickSaveAsync(CancellationToken cancellationToken = default)
    {
        if (_pending is not null)
            throw new InvalidOperationException("A save picker is already open.");

        var activity = Platform.CurrentActivity ?? throw new InvalidOperationException("No foreground Android activity is available.");
        _pending = new TaskCompletionSource<PickedDocument?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _cancellationRegistration = cancellationToken.Register(() =>
        {
            var pending = _pending;
            _pending = null;
            pending?.TrySetCanceled(cancellationToken);
        });
        var intent = new Intent(Intent.ActionOpenDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType("*/*");
        intent.AddFlags((ActivityFlags)ReadOnlySourceGrants.RequestedFlags);
        activity.StartActivityForResult(intent, RequestCode);
        return new ValueTask<PickedDocument?>(_pending.Task);
    }
    public bool HandleActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        if (requestCode != RequestCode || _pending is null)
            return false;

        var pending = _pending;
        _pending = null;
        _cancellationRegistration.Dispose();
        if (resultCode != Result.Ok || data?.Data is not { } uri)
        {
            pending.TrySetResult(null);
            return true;
        }

        try
        {
            AndroidSafFileAccess.PersistPermission(uri, data);
            var documentId = uri.ToString() ?? throw new InvalidOperationException("The picked document URI could not be serialized.");
            pending.TrySetResult(new PickedDocument(documentId, QueryDisplayName(uri) ?? "Pokémon save"));
        }
        catch (Exception error)
        {
            pending.TrySetException(error);
        }
        return true;
    }
"""


class PickerLifecycleTests(unittest.TestCase):
    def test_cancelled_picker_never_rebinds_late_result(self):
        result = adapt_picker(PICKER_SOURCE)
        self.assertIn("cancellationToken.ThrowIfCancellationRequested();", result)
        self.assertIn("pending.TrySetCanceled(cancellationToken)", result)
        self.assertIn("if (pending.Task.IsCompleted)\n            return true;", result)
        self.assertNotIn("return new ValueTask<PickedDocument?>(_pending.Task)", result)
        self.assertIn("return new ValueTask<PickedDocument?>(pending.Task)", result)
        self.assertIn("try\n        {\n            activity.StartActivityForResult", result)
        self.assertIn("_cancellationRegistration.Dispose();\n            throw;", result)
        self.assertIn("ReadOnlySourceGrants.PersistedFlags((int)data.Flags)", result)
        self.assertIn("catch (Java.Lang.SecurityException)", result)
        self.assertNotIn("GrantWriteUriPermission", result)
        with self.assertRaises(ProbeRefusal):
            adapt_picker(result)

    def test_fail_closed_on_changed_pinned_source(self):
        with self.assertRaises(ProbeRefusal):
            adapt_picker(PICKER_SOURCE.replace("_pending.Task", "other.Task"))
        with self.assertRaises(ProbeRefusal):
            adapt_picker(PICKER_SOURCE.replace("pending?.TrySetCanceled", "pending?.TrySetResult"))
        with self.assertRaises(ProbeRefusal):
            adapt_picker(PICKER_SOURCE.replace("ReadOnlySourceGrants.RequestedFlags", "ActivityFlags.GrantWriteUriPermission"))
        with self.assertRaises(ProbeRefusal):
            adapt_picker(PICKER_SOURCE + PICKER_SOURCE)

    def test_preflight_is_non_destructive(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            picker = root / PICKER
            picker.parent.mkdir(parents=True)
            picker.write_text(PICKER_SOURCE)
            with patch("scripts.pkforge_picker_lifecycle.sha", return_value="wrong"):
                with self.assertRaises(ProbeRefusal):
                    prepare(root)
            self.assertEqual(picker.read_text(), PICKER_SOURCE)
            with patch("scripts.pkforge_picker_lifecycle.sha", return_value=PKFORGE):
                verdict = prepare(root)
                self.assertFalse(verdict["late_cancelled_results_rebound"])
                self.assertTrue(verdict["read_once_fallback"])
                with self.assertRaises(ProbeRefusal):
                    prepare(root)

    def test_symlinked_source_refused(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            picker = root / PICKER
            picker.parent.mkdir(parents=True)
            actual = root / "actual.cs"
            actual.write_text(PICKER_SOURCE)
            picker.symlink_to(actual)
            with patch("scripts.pkforge_picker_lifecycle.sha", return_value=PKFORGE):
                with self.assertRaises(ProbeRefusal):
                    prepare(root)


if __name__ == "__main__":
    unittest.main()
