"""Fail-closed synthetic checks of compiled Android backup/transfer policy."""
import unittest
from scripts.pkforge_backup_manifest_gate import validate_xml
from scripts.pkforge_offline_preview import OfflineRefusal

MANIFEST = """E: manifest (line=1)
  E: application (line=2)
    A: android:allowBackup(0x01010280)=(type 0x12)0x0
    A: android:fullBackupContent(0x010104eb)=(type 0x12)0x0
    A: android:dataExtractionRules(0x0101063e)=@0x7f140000
"""
RULES = """E: data-extraction-rules (line=1)
  E: cloud-backup (line=2)
    E: exclude (line=3)
      A: domain="root"
      A: path="."
  E: device-transfer (line=4)
    E: exclude (line=5)
      A: domain="root"
      A: path="."
"""

class ManifestGateTests(unittest.TestCase):
    def test_valid_compiled_policy(self):
        validate_xml(MANIFEST, RULES)

    def test_aapt2_namespace_uri_attributes(self):
        uri = MANIFEST.replace("android:", "http://schemas.android.com/apk/res/android:")
        validate_xml(uri, RULES)

    def test_refuse_backup_true_missing_or_resource(self):
        for bad in (MANIFEST.replace("(type 0x12)0x0", "(type 0x12)0x1", 1),
                    MANIFEST.replace("android:allowBackup", "android:unknown"),
                    MANIFEST.replace("fullBackupContent(0x010104eb)=(type 0x12)0x0",
                                     "fullBackupContent(0x010104eb)=@0x7f000001"),
                    MANIFEST.replace("@0x7f140000", "0x0")):
            with self.subTest(bad=bad), self.assertRaises(OfflineRefusal):
                validate_xml(bad, RULES)

    def test_refuse_missing_cloud_d2d_or_exclusion(self):
        for bad in (RULES.replace("cloud-backup", "other"),
                    RULES.replace("device-transfer", "other"),
                    RULES.replace('domain="root"', 'domain="file"', 1),
                    RULES.replace('path="."', 'path="cache"', 1),
                    RULES + "    E: include (line=6)\n",
                    RULES + "  E: cloud-backup (line=7)\n"):
            with self.subTest(bad=bad), self.assertRaises(OfflineRefusal):
                validate_xml(MANIFEST, bad)

if __name__ == "__main__":
    unittest.main()
