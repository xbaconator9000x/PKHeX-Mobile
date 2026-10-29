"""Offline diagnostic source adaptation: exact-pin, fail-closed regression tests."""
import tempfile
import unittest
from pathlib import Path

from scripts import pkforge_offline_preview as offline


class OfflinePreviewTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        for relative, guards in offline.PATCHES.items():
            path = self.root / relative
            path.parent.mkdir(parents=True, exist_ok=True)
            source = "\n".join(anchor + "\n        // synthetic body\n    }\n" for anchor, _ in guards)
            source += "\n".join(before + "\n" for before, _ in offline.REPLACEMENTS.get(relative, []))
            path.write_text(source, encoding="utf-8")
        manifest = self.root / offline.MANIFEST
        manifest.parent.mkdir(parents=True, exist_ok=True)
        manifest.write_text(offline.MANIFEST_OPEN + "\n</manifest>\n", encoding="utf-8")

    def tearDown(self):
        self.temp.cleanup()

    def test_every_media_and_installer_guard_is_injected(self):
        staged = offline.transform_sources(self.root)
        count = 0
        for relative, guards in offline.PATCHES.items():
            output = staged[self.root / relative]
            for anchor, guard in guards:
                self.assertEqual(output.count(anchor + "\n        " + guard), 1, relative)
                count += 1
        self.assertEqual(count, 23)

    def test_update_permissions_are_excluded_from_diagnostic_build(self):
        staged = offline.transform_sources(self.root)
        text = staged[self.root / (offline.APP + "Services/AppUpdateService.cs")]
        self.assertIn("#if ANDROID && !DIAGNOSTIC\n[assembly: UsesPermission", text)
        self.assertIn("!PreviewRuntimePolicy.Offline && CanRequestInstalls()", text)
        self.assertIn("Offline diagnostic refuses APK installation.", text)

    def test_manifest_removes_transitive_network_and_installer_permissions(self):
        staged = offline.transform_sources(self.root)
        manifest = staged[self.root / offline.MANIFEST]
        for name in ("INTERNET", "REQUEST_INSTALL_PACKAGES",
                     "UPDATE_PACKAGES_WITHOUT_USER_ACTION"):
            self.assertIn('android.permission.' + name + '" tools:node="remove"', manifest)
        self.assertIn('xmlns:tools="http://schemas.android.com/tools"', manifest)

    def test_policy_enabled_only_for_diagnostic_flavor(self):
        staged = offline.transform_sources(self.root)
        policy = staged[self.root / offline.POLICY]
        self.assertIn("#if DIAGNOSTIC\n            return true;", policy)
        self.assertIn("#else\n            return false;", policy)

    def test_missing_pinned_method_fails_before_writes(self):
        path = self.root / (offline.APP + "Services/ItemArt.cs")
        original = path.read_text()
        path.write_text(original.replace("public static async Task<string?> GetAsync", "public static async Task<string?> ChangedAsync"))
        before = {p: p.read_bytes() for p in self.root.rglob("*") if p.is_file()}
        with self.assertRaises(offline.OfflineRefusal):
            offline.transform_sources(self.root)
        self.assertEqual(before, {p: p.read_bytes() for p in self.root.rglob("*") if p.is_file()})

    def test_duplicate_anchor_is_refused(self):
        path = self.root / (offline.APP + "Services/SpritePackDownloader.cs")
        source = path.read_text()
        anchor = offline.PATCHES[offline.APP + "Services/SpritePackDownloader.cs"][0][0]
        path.write_text(source + "\n" + anchor)
        with self.assertRaises(offline.OfflineRefusal):
            offline.transform_sources(self.root)

    def test_second_application_is_refused(self):
        staged = offline.transform_sources(self.root)
        for path, content in staged.items():
            path.write_text(content)
        with self.assertRaises(offline.OfflineRefusal):
            offline.transform_sources(self.root)

    def test_symlinked_upstream_source_is_refused(self):
        path = self.root / (offline.APP + "Services/ItemArt.cs")
        path.unlink()
        outside = self.root.parent / (self.root.name + "-external.txt")
        try:
            outside.write_text("outside")
            path.symlink_to(outside)
            with self.assertRaises(offline.OfflineRefusal):
                offline.transform_sources(self.root)
        finally:
            path.unlink()
            outside.unlink()

    def test_existing_policy_is_refused(self):
        policy = self.root / offline.POLICY
        policy.write_text("untrusted")
        with self.assertRaises(offline.OfflineRefusal):
            offline.transform_sources(self.root)

    def test_changed_manifest_is_refused(self):
        manifest = self.root / offline.MANIFEST
        manifest.write_text("<manifest/>")
        with self.assertRaises(offline.OfflineRefusal):
            offline.transform_sources(self.root)

    def test_offline_fallbacks_do_not_mutate_save_writer(self):
        self.assertTrue(all("SaveSession" not in p and "Writer" not in p
                            for p in offline.PATCHES))
        self.assertIn("return null;", dict(offline.PATCHES[offline.APP + "Services/ItemArt.cs"])
                      ["public static async Task<string?> GetAsync(string itemName)\n    {"])


if __name__ == "__main__":
    unittest.main()
