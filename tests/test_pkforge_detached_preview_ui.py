"""Fail-closed tests for the exact pinned PKForge MAUI preview wiring.

These test source transformations only; the official-Core Android compile
workflow separately proves the resulting C# actually builds.
"""
import tempfile
import unittest
from pathlib import Path

from scripts.pkforge_detached_preview_ui import (
    VIEW, PROJECT, ProbeRefusal, adapt_view, adapt_project, prepare,
)


VIEW_SOURCE = """using PKForge.App.Services;
public sealed partial class BoxBrowserPage {
    private readonly HashSet<int> _lockedSlots = [];
        viewModel.PropertyChanged += (_, args) =>
        {
        }
        var save = FocusButton(EditorTool("Save changes", primary: true), "SAVE CHANGES");
        panel.Children.Add(save);
        panel.Children.Add(monActions);
}
"""
PROJECT_SOURCE = """<Project>
    <ProjectReference Include="../PKForge.Infrastructure/PKForge.Infrastructure.csproj" />
</Project>
"""


class PinnedMauiPreviewTests(unittest.TestCase):
    def test_button_is_nonpublishing_and_binds_selected_slot(self):
        result = adapt_view(VIEW_SOURCE)
        self.assertIn("ProviderBoundRetailNicknameEditor.PreviewAsync(", result)
        self.assertNotIn("SaveInputSnapshot.FromBytes(session.Serialize().Span)", result)
        self.assertIn("Interlocked.Increment(ref _previewRequest)", result)
        self.assertIn("Provider bytes rechecked", result)
        self.assertIn("var slot = _viewModel.SelectedSlot;", result)
        self.assertIn("panel.Children.Add(detachedPreview);", result)
        self.assertIn("save.IsEnabled = false;", result)
        self.assertIn("nothing saved.", result)
        self.assertNotIn("WriteAtomicallyAsync", result)
        self.assertLess(result.index("panel.Children.Add(detachedPreview);"),
                        result.index("panel.Children.Add(save);"))

    def test_refuses_missing_and_duplicate_anchors(self):
        with self.assertRaises(ProbeRefusal):
            adapt_view(VIEW_SOURCE.replace("panel.Children.Add(monActions);", ""))
        with self.assertRaises(ProbeRefusal):
            adapt_view(VIEW_SOURCE + VIEW_SOURCE)
        with self.assertRaises(ProbeRefusal):
            adapt_view(adapt_view(VIEW_SOURCE))

    def test_project_reference_requires_actual_adapter(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            with self.assertRaises(ProbeRefusal):
                adapt_project(PROJECT_SOURCE, root)
            target = root / "src/PKHeXMobile.PortConsolidation/PKHeXMobile.PortConsolidation.csproj"
            target.parent.mkdir(parents=True)
            target.write_text("<Project />", encoding="utf-8")
            result = adapt_project(PROJECT_SOURCE, root)
            self.assertIn(str(target), result)
            with self.assertRaises(ProbeRefusal):
                adapt_project(result, root)

    def test_validates_both_sources_before_writing(self):
        with tempfile.TemporaryDirectory() as temporary:
            base = Path(temporary)
            upstream = base / "upstream"
            mobile = base / "mobile"
            view = upstream / VIEW
            project = upstream / PROJECT
            view.parent.mkdir(parents=True)
            project.parent.mkdir(parents=True, exist_ok=True)
            view.write_text(VIEW_SOURCE, encoding="utf-8")
            project.write_text(PROJECT_SOURCE.replace("PKForge.Infrastructure", "Changed"),
                               encoding="utf-8")
            target = mobile / "src/PKHeXMobile.PortConsolidation/PKHeXMobile.PortConsolidation.csproj"
            target.parent.mkdir(parents=True)
            target.write_text("<Project />", encoding="utf-8")
            with self.assertRaises(ProbeRefusal):
                prepare(upstream, mobile)
            self.assertEqual(view.read_text(encoding="utf-8"), VIEW_SOURCE)
            project.write_text(PROJECT_SOURCE, encoding="utf-8")
            verdict = prepare(upstream, mobile)
            self.assertFalse(verdict["writes_certified"])
            self.assertFalse(verdict["device_tested"])
            self.assertEqual(verdict["changed_files"], [VIEW, PROJECT])
            with self.assertRaises(ProbeRefusal):
                prepare(upstream, mobile)

    def test_symlinked_source_refused(self):
        with tempfile.TemporaryDirectory() as temporary:
            base = Path(temporary)
            upstream = base / "upstream"
            mobile = base / "mobile"
            view = upstream / VIEW
            project = upstream / PROJECT
            view.parent.mkdir(parents=True)
            project.parent.mkdir(parents=True, exist_ok=True)
            actual = base / "actual.cs"
            actual.write_text(VIEW_SOURCE, encoding="utf-8")
            view.symlink_to(actual)
            project.write_text(PROJECT_SOURCE, encoding="utf-8")
            mobile.mkdir()
            with self.assertRaises(ProbeRefusal):
                prepare(upstream, mobile)


if __name__ == "__main__":
    unittest.main()
