import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT / "scripts"))
from check_repository import failures, FORBIDDEN_SUFFIXES

class ScaffoldTests(unittest.TestCase):
    def test_foundation_is_complete(self):
        self.assertEqual([], failures())

    def test_full_gpl_v3_licence(self):
        body = (ROOT / "LICENSE").read_text()
        self.assertIn("GNU GENERAL PUBLIC LICENSE", body)
        self.assertIn("Version 3, 29 June 2007", body)
        self.assertGreater(len(body), 30000)

    def test_readme_stays_nondescript(self):
        readme = (ROOT / "README.md").read_text(encoding="utf-8")
        self.assertTrue(readme.startswith("# This is a test.\n\n"),
                        "Keep the README's public-facing opening generic.")

    def test_core_package_version_is_exact(self):
        project = (ROOT / "src/PKHeXMobile.ReadOnly/PKHeXMobile.ReadOnly.csproj").read_text()
        self.assertIn('PackageReference Include="PKHeX.Core" Version="[26.10.18]"', project)

    def test_read_only_projection_has_no_save_writer(self):
        source = (ROOT / "src/PKHeXMobile.ReadOnly/ReadOnlySaveInspector.cs").read_text()
        source = "\n".join(line.split("//", 1)[0] for line in source.splitlines())
        for forbidden in ("SaveFile.Write(", "save.Write(", "File.Write", "SetBoxSlotAtIndex(", "SetPartySlotAtIndex("):
            self.assertNotIn(forbidden, source)

    def test_read_only_tools_do_not_open_files_for_writing(self):
        for name in ("src/PKHeXMobile.Inspect/Program.cs",
                     "src/PKHeXMobile.Verify/Program.cs"):
            source = (ROOT / name).read_text()
            self.assertIn("FileAccess.Read", source)
            self.assertNotIn("FileAccess.Write", source)
            self.assertNotIn("FileMode.Create", source)
            self.assertNotIn("File.WriteAll", source)
        verifier = (ROOT / "src/PKHeXMobile.ReadOnly/SaveProjectionVerifier.cs").read_text()
        self.assertIn("[property: JsonRequired]", verifier)

    def test_android_prototype_is_read_only(self):
        manifest = (ROOT / "src/PKHeXMobile.Android/AndroidManifest.xml").read_text()
        source = (ROOT / "src/PKHeXMobile.Android/MainActivity.cs").read_text()
        self.assertNotIn("<uses-permission", manifest)
        self.assertIn("Intent.ActionOpenDocument", source)
        self.assertIn("ReadOnlyDocumentService.InspectWithIdentityAsync", source)
        for forbidden in ("OpenOutputStream(", "FileAccess.Write", "File.WriteAll",
                          "GrantWriteUriPermission", "Intent.ActionCreateDocument",
                          "TakePersistableUriPermission"):
            self.assertNotIn(forbidden, source)

    def test_android_ci_tracks_transitive_build_inputs(self):
        workflow = (ROOT / ".github/workflows/android-build.yml").read_text()
        for path in ("src/PKHeXMobile.Android/**", "src/PKHeXMobile.ReadOnly/**",
                     "src/PKHeXMobile.Domain/**", "global.json",
                     "Directory.Build.*", "Directory.Packages.props",
                     "NuGet.Config"):
            self.assertEqual(2, workflow.count(f"'{path}'"),
                             f"Android build must trigger on push and PR for {path}")
        self.assertIn("android-debug-apk.sha256", workflow)
        self.assertIn("android-build-provenance.txt", workflow)

    def test_binary_files_must_not_be_committed(self):
        self.assertTrue({".gba", ".sav", ".apk", ".jks"}.issubset(FORBIDDEN_SUFFIXES))

if __name__ == "__main__":
    unittest.main()
