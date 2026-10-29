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

    def make_apk(self, valid=True):
        if valid:
            with zipfile.ZipFile(self.apk, "w") as archive:
                archive.writestr("AndroidManifest.xml", b"synthetic manifest")
        else:
            self.apk.write_bytes(b"not a zip")

    def set_package(self, package):
        self.packager.write_text(
            "#!/bin/sh\nprintf \"package: name='%s' versionCode='1'\\n\" "
            + "'" + package + "'\n")
        self.packager.chmod(0o755)

    def make_report(self):
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
            return audit.attestation(self.root, self.mobile, self.packager)

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
