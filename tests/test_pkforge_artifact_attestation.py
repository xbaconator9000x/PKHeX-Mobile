"""Synthetic, no-game-data tests for APK identity/provenance handoff."""
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import zipfile

from scripts import pkforge_artifact_attestation as audit


PROJECT_XML = """<Project Sdk="Microsoft.NET.Sdk">
<PropertyGroup><TargetFramework>net10.0-android</TargetFramework>
<UseMaui>true</UseMaui><ApplicationId>org.pkforge.app</ApplicationId></PropertyGroup>
<PropertyGroup Condition="'$(DiagnosticBuild)' == 'true'">
<ApplicationId>org.pkforge.app.debug</ApplicationId></PropertyGroup>
<ItemGroup><MauiAsset Include="Resources/UI/*.png" /></ItemGroup>
</Project>"""


class ArtifactAttestationTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name) / "pkforge"
        self.mobile = Path(self.temp.name) / "mobile"
        self.app = self.root / "src/PKForge.App"
        self.app.mkdir(parents=True)
        (self.app / "PKForge.App.csproj").write_text(PROJECT_XML)
        (self.app / "Resources/UI").mkdir(parents=True)
        (self.app / "Resources/UI/icon.png").write_bytes(b"synthetic-image")
        obj = self.app / "obj"
        obj.mkdir()
        (obj / "project.assets.json").write_text(
            json.dumps({"libraries": {"PKHeX.Core/26.10.18": {}}}))
        self.apk_dir = self.app / "bin/Debug/net10.0-android"
        self.apk_dir.mkdir(parents=True)
        self.apk = self.apk_dir / "pkforge-debug.apk"
        self.make_apk()
        (self.mobile / "scripts").mkdir(parents=True)
        (self.mobile / "scripts/pkforge_test.py").write_text("# synthetic")
        self.packager = Path(self.temp.name) / "aapt2"
        self.set_package(audit.EXPECTED_PACKAGE)

    def make_apk(self, valid=True, *, dex=True, native=True, managed=True,
                 extra_entries=None):
        if valid:
            with zipfile.ZipFile(self.apk, "w") as archive:
                archive.writestr("AndroidManifest.xml", b"synthetic manifest")
                if dex:
                    archive.writestr("classes.dex", b"synthetic dex")
                if native:
                    archive.writestr("lib/arm64-v8a/libmonodroid.so", b"synthetic runtime")
                if managed:
                    for component in ("App", "Engine", "Infrastructure"):
                        archive.writestr(f"assemblies/PKForge.{component}.dll",
                                         b"synthetic managed component")
                for name, data in (extra_entries or []):
                    archive.writestr(name, data)
        else:
            self.apk.write_bytes(b"not a zip")

    def set_package(self, package):
        self.packager.write_text(
            "#!/bin/sh\nprintf \"package: name='%s' versionCode='1'\\n\" "
            + "'" + package + "'\n")
        self.packager.chmod(0o755)

    def make_report(self, source_snapshot=None):
        def head(path):
            if path == self.root:
                return audit.PKFORGE
            if path == self.root / "external/PKHeX":
                return audit.FORK
            if path == self.root / "external/PKHeX-Plugins":
                return audit.PLUGINS
            if path == self.mobile:
                return "a" * 40
            raise AssertionError(path)
        with patch.object(audit, "git_head", side_effect=head):
            return audit.attestation(self.root, self.mobile, self.packager,
                                     source_snapshot=source_snapshot)

    def test_reports_actual_apk_hash_and_disallows_distribution(self):
        report = self.make_report()
        self.assertEqual(report["apks"][0]["sha256"], audit.digest(self.apk))
        self.assertEqual(report["apks"][0]["application_id"], audit.EXPECTED_PACKAGE)
        self.assertFalse(report["redistribution_authorized"])
        self.assertFalse(report["save_writes_authorized"])
        self.assertFalse(report["device_certified"])
        self.assertFalse(report["rights_review_complete"])
        self.assertEqual(report["nuget"]["libraries"], ["PKHeX.Core/26.10.18"])
        self.assertTrue(report["assets"][0]["sha256"])

    def test_frozen_source_snapshot_matches_after_packaging(self):
        snapshot = Path(self.temp.name) / "source-freeze.json"
        audit.write_source_snapshot(self.root, snapshot)
        report = self.make_report(snapshot)
        self.assertTrue(report["frozen_source"]["source_unchanged_after_build"])
        self.assertEqual(report["frozen_source"]["sha256"], audit.digest(snapshot))

    def test_source_mutation_after_freeze_refused(self):
        snapshot = Path(self.temp.name) / "source-freeze.json"
        audit.write_source_snapshot(self.root, snapshot)
        (self.app / "PKForge.App.csproj").write_text(PROJECT_XML + "\n")
        with self.assertRaisesRegex(audit.AttestationError, "Source changed"):
            self.make_report(snapshot)

    def test_new_source_file_after_freeze_refused(self):
        snapshot = Path(self.temp.name) / "source-freeze.json"
        audit.write_source_snapshot(self.root, snapshot)
        new_file = self.root / "src/PKForge.Engine/Unexpected.cs"
        new_file.parent.mkdir()
        new_file.write_text("class Unexpected {}")
        with self.assertRaisesRegex(audit.AttestationError, "Unexpected.cs"):
            self.make_report(snapshot)

    def test_generated_obj_files_do_not_break_source_freeze(self):
        snapshot = Path(self.temp.name) / "source-freeze.json"
        audit.write_source_snapshot(self.root, snapshot)
        (self.app / "obj/Generated.cs").write_text("class Generated {}")
        self.assertTrue(self.make_report(snapshot)["frozen_source"]
                        ["source_unchanged_after_build"])

    def test_missing_snapshot_is_refused(self):
        snapshot = Path(self.temp.name) / "missing.json"
        with self.assertRaisesRegex(audit.AttestationError, "frozen source snapshot"):
            self.make_report(snapshot)

    def test_source_symlink_is_refused(self):
        target = Path(self.temp.name) / "other.cs"
        target.write_text("class Other {}")
        (self.app / "Other.cs").symlink_to(target)
        with self.assertRaisesRegex(audit.AttestationError, "Symlink"):
            audit.source_tree_manifest(self.root)

    def test_payload_gate_rejects_manifest_only_spoof(self):
        self.make_apk(dex=False, native=False, managed=False)
        with self.assertRaisesRegex(audit.AttestationError, "classes.dex"):
            self.make_report()

    def test_missing_native_runtime_refused(self):
        self.make_apk(native=False)
        with self.assertRaisesRegex(audit.AttestationError, "native runtime"):
            self.make_report()

    def test_missing_managed_editor_payload_refused(self):
        self.make_apk(managed=False)
        with self.assertRaisesRegex(audit.AttestationError, "managed payload"):
            self.make_report()

    def test_packaged_entry_manifest_changes_when_unlisted_asset_changes(self):
        self.make_apk(extra_entries=[("assets/hidden.png", b"unlisted-a")])
        first = self.make_report()["apks"][0]
        self.make_apk(extra_entries=[("assets/hidden.png", b"unlisted-b")])
        second = self.make_report()["apks"][0]
        self.assertNotEqual(first["packaged_entry_manifest_sha256"],
                            second["packaged_entry_manifest_sha256"])
        self.assertEqual(first["packaged_media_entry_count_unreviewed"], 1)
        self.assertFalse(first["packaged_media_rights_verified"])
        self.assertEqual(first["managed_payload_entries"],
                         ["assemblies/PKForge.App.dll",
                          "assemblies/PKForge.Engine.dll",
                          "assemblies/PKForge.Infrastructure.dll"])

    def test_opaque_assembly_store_without_editor_identity_is_refused(self):
        self.make_apk(managed=False, extra_entries=[
            ("assemblies/assemblies.blob", b"synthetic store")])
        with self.assertRaisesRegex(audit.AttestationError, "PKForge.App"):
            self.make_report()

    def test_real_dotnet_android_embedded_dll_so_layout(self):
        self.make_apk(managed=False, extra_entries=[
            (f"lib/arm64-v8a/lib_PKForge.{part}.dll.so", b"synthetic assembly")
            for part in ("App", "Engine", "Infrastructure")])
        entry = self.make_report()["apks"][0]
        self.assertEqual(len(entry["managed_payload_entries"]), 3)
        self.assertIn("lib/arm64-v8a/lib_PKForge.App.dll.so",
                      entry["managed_payload_entries"])

    def test_missing_engine_assembly_refused_even_if_app_exists(self):
        self.make_apk(managed=False, extra_entries=[
            ("lib/arm64-v8a/lib_PKForge.App.dll.so", b"synthetic app")])
        with self.assertRaisesRegex(audit.AttestationError, "PKForge.Engine"):
            self.make_report()

    def test_duplicate_zip_entry_refused(self):
        import warnings
        with warnings.catch_warnings():
            warnings.simplefilter("ignore", UserWarning)
            self.make_apk(extra_entries=[("classes.dex", b"duplicate")])
        with self.assertRaisesRegex(audit.AttestationError, "Duplicate"):
            self.make_report()

    def test_zip_path_traversal_refused(self):
        self.make_apk(extra_entries=[("../private.png", b"synthetic")])
        with self.assertRaisesRegex(audit.AttestationError, "Unsafe APK"):
            self.make_report()

    def test_package_mismatch_refused(self):
        self.set_package("org.pkforge.app")
        with self.assertRaisesRegex(audit.AttestationError, "package ID"):
            self.make_report()

    def test_no_apk_refused(self):
        self.apk.unlink()
        with self.assertRaisesRegex(audit.AttestationError, "no APK"):
            self.make_report()

    def test_invalid_zip_refused(self):
        self.make_apk(valid=False)
        with self.assertRaisesRegex(audit.AttestationError, "Invalid APK"):
            self.make_report()

    def test_asset_escape_refused(self):
        outside = Path(self.temp.name) / "outside.png"
        outside.write_bytes(b"private")
        (self.app / "PKForge.App.csproj").write_text(
            PROJECT_XML.replace("Resources/UI/*.png", "../../../outside.png"))
        with self.assertRaisesRegex(audit.AttestationError, "escapes"):
            self.make_report()

    def test_dynamic_asset_expression_refused(self):
        (self.app / "PKForge.App.csproj").write_text(
            PROJECT_XML.replace("Resources/UI/*.png", "$(SecretAssets)"))
        with self.assertRaisesRegex(audit.AttestationError, "Unresolved"):
            self.make_report()

    def test_missing_restore_graph_refused(self):
        (self.app / "obj/project.assets.json").unlink()
        with self.assertRaisesRegex(audit.AttestationError, "restore graph"):
            self.make_report()

    def test_no_asset_declarations_refused(self):
        (self.app / "PKForge.App.csproj").write_text(
            PROJECT_XML.replace('<MauiAsset Include="Resources/UI/*.png" />', ""))
        with self.assertRaisesRegex(audit.AttestationError, "No MAUI"):
            self.make_report()

    def test_unmatched_assets_recorded_not_approved(self):
        (self.app / "Resources/UI/icon.png").unlink()
        report = self.make_report()
        self.assertEqual(report["assets"][0]["rights"],
                         "unresolved-pattern-no-distribution")

    def test_changed_upstream_pin_refused(self):
        with patch.object(audit, "git_head", return_value="0" * 40):
            with self.assertRaisesRegex(audit.AttestationError, "SHA mismatch"):
                audit.attestation(self.root, self.mobile, self.packager)

    def test_multiple_apks_are_individually_inspected(self):
        other = self.apk_dir / "other.apk"
        other.write_bytes(self.apk.read_bytes())
        report = self.make_report()
        self.assertEqual(len(report["apks"]), 2)


if __name__ == "__main__":
    unittest.main()
