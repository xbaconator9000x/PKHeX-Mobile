"""Pinned-source negative tests for unverified whole-save sharing and Android backup."""
import tempfile
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path
from scripts import pkforge_export_backup_boundary as guard

SOURCE = '''    /// <summary>Exports the live edited save through the Android share sheet.</summary>
    private async Task ExportModifiedSaveAsync(Domain.ISaveEngineSession session)
    {
        await File.WriteAllBytesAsync("cache", session.Serialize().ToArray());
        await Share.Default.RequestAsync(new ShareFileRequest());
    }

    /// <summary>
    /// Pokemon Compass romhack settings:
'''

class BoundaryTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        self.box = self.root / guard.BOX
        self.box.parent.mkdir(parents=True)
        self.box.write_text(guard.MENU + guard.CASE + SOURCE)
        self.manifest = self.root / guard.MANIFEST
        self.manifest.parent.mkdir(parents=True)
        self.manifest.write_text('<manifest><application android:allowBackup="true"/></manifest>')

    def test_menu_dispatch_and_direct_export_are_removed(self):
        result = guard.adapt_box(self.box.read_text())
        for forbidden in ("new PadOption(\"Export modified save\"", "case \"Export modified save\"",
                          "File.WriteAllBytesAsync", "session.Serialize()", "Share.Default.RequestAsync"):
            self.assertNotIn(forbidden, result)
        self.assertIn('save export disabled', result)
        self.assertEqual(result.count("ExportModifiedSaveAsync("), 1)

    def test_auto_backup_and_d2d_cloud_transfer_excluded(self):
        manifest = guard.adapt_manifest(self.manifest.read_text())
        for token in ('android:allowBackup="false"', 'android:fullBackupContent="false"',
                      'android:dataExtractionRules="@xml/pkhex_no_transfer"'):
            self.assertIn(token, manifest)
        element = ET.fromstring(guard.RULES)
        self.assertEqual({node.tag for node in element}, {"cloud-backup", "device-transfer"})
        self.assertTrue(all(node[0].attrib == {"domain": "root", "path": "."} for node in element))

    def test_fail_closed_drift_and_idempotency(self):
        self.assertRaises(guard.OfflineRefusal, guard.adapt_box, SOURCE + guard.MENU)
        self.assertRaises(guard.OfflineRefusal, guard.adapt_box, guard.adapt_box(self.box.read_text()))
        self.assertRaises(guard.OfflineRefusal, guard.adapt_manifest,
                          guard.adapt_manifest(self.manifest.read_text()))

    def test_staging_rejects_drift_before_modifying_any_file(self):
        self.box.write_text(self.box.read_text().replace("session.Serialize()", "unknown()"))
        self.assertRaises(guard.OfflineRefusal, guard.transform, self.root)
        self.assertIn('allowBackup="true"', self.manifest.read_text())
        self.assertFalse((self.root / guard.XML).exists())

if __name__ == "__main__":
    unittest.main()
