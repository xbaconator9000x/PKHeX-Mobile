"""Pinned-source regression tests for preview private-data containment."""
import tempfile
import unittest
from pathlib import Path
from scripts import pkforge_private_storage_refusal as guard
from scripts import pkforge_residual_cache_refusal as residual

class PrivateStorageTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        fixtures = {
            guard.PROJECT: "<Project>\n" + guard.PROJECT_ANCHOR + "\n</Project>\n",
            guard.BANK: "class Bank {\n" + guard.BANK_ANCHOR + "\n}\n",
            guard.BACKUP: ("class Backup {\npublic async Task CreateAsync()\n" +
                           guard.BACKUP_START + "        // do not write in preview\n" +
                           guard.BACKUP_END + "}\n"),
            guard.MAUI: "class Maui {\n" + guard.GIFT_REG + "\n" + guard.IDENTITY_REG + "\n}\n",
            guard.HOME: ("class Home {\n" + guard.LOG_MENU + guard.LOG_CASE +
                         guard.LOG_START + "    private async Task ShareLogsAsync()\n" +
                         "    { AppLog.BuildShareArchive(null); await Share.Default.RequestAsync(null); }\n" +
                         guard.LOG_END + "\n" + guard.CRASH_START +
                         "    private async Task OfferCrashReportAsync()\n" +
                         "    { /* Share report */ await ShareLogsAsync(); }\n" +
                         guard.CRASH_END + "    private async Task DownloadSpritePackAsync() {}\n}\n"),
            guard.BOX: (guard.BOX_BULK_EXPORT + "                var directory = \"export\";\n" +
                        "                _viewModel.BulkExport(directory);\n" +
                        "                var share = new ShareMultipleFilesRequest();\n            }\n" +
                        guard.BOX_SLOT_EXPORT + "        session.ExportSlot(_viewModel.BoxIndex, slot);\n" +
                        "        File.WriteAllBytesAsync(path, export.Data);\n    }\n"),
            guard.BANK_PAGE: (guard.BANK_ENTITY_EXPORT + "        _bank.GetData(entry.Id);\n" +
                              "        File.WriteAllBytesAsync(path, bytes);\n    }\n"),
            guard.EVENT_GALLERY: (guard.EVENT_ENTITY_EXPORT +
                                  "            _ctx.Service.ExportEntity(null);\n" +
                                  "            _ctx.Service.ExportCard(null);\n" +
                                  "            File.WriteAllBytesAsync(path, export.Data);\n        }\n"),
        }
        for name, content in fixtures.items():
            p = self.root / name
            p.parent.mkdir(parents=True, exist_ok=True)
            p.write_text(content)

        # Source-pinned residual output routes. These are synthetic only, never private saves.
        box = self.root / guard.BOX
        box.write_text(box.read_text() + residual.SHOWDOWN_START +
            '        var text = "SYNTHETIC";\n' +
            '        await Clipboard.Default.SetTextAsync(text);\n' +
            '        case "Share as file":\n' +
            '        await File.WriteAllTextAsync(path, text);\n' +
            '        await Share.Default.RequestAsync(null);\n    }\n' +
            residual.SHOWDOWN_END + '\n' +
            residual.AUDIT_START +
            '        await File.WriteAllTextAsync(path, text);\n' +
            '        await Share.Default.RequestAsync(null);\n    }\n' +
            residual.AUDIT_END + '\n')
        qr = self.root / residual.QR
        qr.parent.mkdir(parents=True, exist_ok=True)
        qr.write_text(
            residual.BINARY_START +
            '        var latin1 = Encoding.Latin1.GetString(payload);\n' +
            '        var matrix = new QRCodeWriter().encode(latin1);\n' +
            '        return ShowAsync(host, title, matrix);\n    }\n' +
            residual.BINARY_END + '\n    {\n' +
            residual.QR_DISK_START +
            '        using (var bitmap = new SKBitmap(matrix.Width, matrix.Height))\n' +
            '        {\n            using var image = SKImage.FromBitmap(bitmap);\n' +
            '            using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);\n' +
            '            using var file = File.Create(path);\n' +
            '            encoded.SaveTo(file);\n        }\n' +
            residual.QR_DISK_END + '\n' +
            residual.QR_CLOSE_START + residual.QR_CLOSE + '        }\n' +
            '        var content = new VerticalStackLayout\n' +
            '        {\n            Children =\n            {\n                ' +
            residual.QR_IMAGE_BLOCK + '\n            }\n        };\n    }\n')
        park = self.root / residual.PARK
        park.parent.mkdir(parents=True, exist_ok=True)
        park.write_text(
            residual.PARK_START +
            '        if (_sharing || _photos.Count == 0) return;\n' +
            '        File.Copy(photo.Path, copy, overwrite: true);\n' +
            '        await Share.Default.RequestAsync(null);\n    }\n' +
            residual.PARK_END + '\n')

    def test_compile_time_diagnostic_guards(self):
        out = guard.transform(self.root)
        self.assertIn("DIAGNOSTIC", out[self.root / guard.PROJECT])
        self.assertIn("#if DIAGNOSTIC", out[self.root / guard.BANK])
        self.assertIn("bank persistence disabled", out[self.root / guard.BANK])
        self.assertIn("raw-save backup persistence disabled", out[self.root / guard.BACKUP])
        self.assertIn("#else", out[self.root / guard.BACKUP])
        maui = out[self.root / guard.MAUI]
        self.assertIn("new InjectedGiftHistory(null)", maui)
        self.assertIn("new JsonSaveIdentityStore(null)", maui)
        self.assertNotIn("injected-gifts.json", maui)
        self.assertNotIn("save-identities.json", maui)
        home = out[self.root / guard.HOME]
        self.assertNotIn("AppLog.BuildShareArchive", home)
        self.assertNotIn("Share.Default.RequestAsync", home)
        self.assertNotIn("new PadOption(\"Share logs\"", home)
        self.assertNotIn("case \"Share logs\"", home)
        self.assertNotIn("Share report", home)
        self.assertIn("log sharing disabled", home)
        self.assertIn("private Task OfferCrashReportAsync()", home)
        box = out[self.root / guard.BOX]
        self.assertEqual(box.count("Diagnostic preview: Pokémon export disabled."), 2)
        self.assertLess(box.index(guard.BOX_BULK_GUARD), box.index("_viewModel.BulkExport(directory)"))
        self.assertLess(box.index(guard.BOX_SLOT_GUARD), box.index("session.ExportSlot(_viewModel.BoxIndex, slot)"))
        bank = out[self.root / guard.BANK_PAGE]
        self.assertLess(bank.index(guard.BANK_ENTITY_GUARD), bank.index("_bank.GetData(entry.Id)"))
        event = out[self.root / guard.EVENT_GALLERY]
        self.assertLess(event.index(guard.EVENT_ENTITY_GUARD), event.index("_ctx.Service.ExportEntity"))

    def test_fail_closed_on_drift_before_writes(self):
        p = self.root / guard.BANK
        p.write_text(p.read_text().replace("EnsureWritable", "Unknown"))
        with self.assertRaises(guard.OfflineRefusal):
            guard.transform(self.root)
        self.assertNotIn("DIAGNOSTIC", (self.root / guard.PROJECT).read_text())

    def test_fail_closed_on_identity_registration_drift(self):
        p = self.root / guard.MAUI
        p.write_text(p.read_text().replace("save-identities.json", "other-identity-file.json"))
        with self.assertRaises(guard.OfflineRefusal):
            guard.transform(self.root)
        self.assertIn("injected-gifts.json", p.read_text())

    def test_fail_closed_on_log_share_method_drift(self):
        p = self.root / guard.HOME
        p.write_text(p.read_text().replace("AppLog.BuildShareArchive", "DifferentArchive"))
        with self.assertRaises(guard.OfflineRefusal):
            guard.transform(self.root)
        self.assertIn("Share.Default.RequestAsync", p.read_text())

    def test_fail_closed_on_entity_export_drift(self):
        p = self.root / guard.BOX
        p.write_text(p.read_text().replace("_viewModel.BulkExport(directory)", "UnknownBulkExport()"))
        with self.assertRaises(guard.OfflineRefusal):
            guard.transform(self.root)
        self.assertNotIn("Diagnostic preview: Pokémon export disabled.", p.read_text())

    def test_residual_showdown_and_audit_export_denied_before_cache(self):
        out = guard.transform(self.root)
        box = out[self.root / guard.BOX]
        self.assertLess(box.index(residual.SHOWDOWN_GUARD),
                        box.index('Clipboard.Default.SetTextAsync(text)'))
        self.assertLess(box.index(residual.SHOWDOWN_GUARD),
                        box.index('File.WriteAllTextAsync(path, text)'))
        self.assertLess(box.index(residual.AUDIT_GUARD, box.index(residual.AUDIT_START)),
                        box.rindex('File.WriteAllTextAsync(path, text)'))
        self.assertIn('Showdown sharing and clipboard export disabled', box)
        self.assertIn('Diagnostic preview: Pokémon export disabled.', box)

    def test_qr_is_per_overlay_memory_only_and_binary_export_denied(self):
        out = guard.transform(self.root)
        qr = out[self.root / residual.QR]
        self.assertNotIn('qr-latest.png', qr)
        self.assertNotIn('File.Create(path)', qr)
        self.assertNotIn('ImageSource.FromFile(path)', qr)
        self.assertIn(residual.QR_MEMORY_IMAGE, qr)
        self.assertIn('png = encoded.ToArray();', qr)
        self.assertNotIn('Array.Clear(png);', qr)
        self.assertIn('qrImage.Source = null;', qr)
        self.assertIn('if (closed) return;', qr)
        self.assertIn(residual.QR_IMAGE_CALL, qr)
        self.assertLess(qr.index('if (closed) return;'), qr.index('qrImage.Source = null;'))
        self.assertLess(qr.index('qrImage.Source = null;'), qr.index('done.TrySetResult();'))
        # Late MAUI stream callbacks must see unmodified PNG bytes.
        self.assertEqual(qr.count('png = encoded.ToArray();'), 1)
        self.assertNotIn('png.AsSpan().Clear()', qr)
        self.assertLess(qr.index(residual.BINARY_GUARD),
                        qr.index('Encoding.Latin1.GetString(payload)'))

    def test_qr_close_and_image_drift_refuse_before_any_staged_write(self):
        qr = self.root / residual.QR
        original = qr.read_text()
        for before, after in (
            (residual.QR_CLOSE_START, "        void Close(bool force)\\n        {\\n"),
            (residual.QR_IMAGE_BLOCK, residual.QR_IMAGE_BLOCK.replace("240", "256")),
        ):
            qr.write_text(original.replace(before, after))
            try:
                with self.assertRaises(guard.OfflineRefusal):
                    guard.transform(self.root)
                self.assertNotIn("DIAGNOSTIC", (self.root / guard.PROJECT).read_text())
            finally:
                qr.write_text(original)

    def test_park_photo_share_is_refused_without_deleting_legacy_media(self):
        out = guard.transform(self.root)
        park = out[self.root / residual.PARK]
        self.assertLess(park.index(residual.PARK_GUARD),
                        park.index('File.Copy(photo.Path, copy, overwrite: true)'))
        self.assertNotIn('File.Delete(', park)

    def test_residual_drift_fails_before_any_staged_write(self):
        qr = self.root / residual.QR
        qr.write_text(qr.read_text().replace('qr-latest.png', 'renamed.png'))
        with self.assertRaises(guard.OfflineRefusal):
            guard.transform(self.root)
        self.assertNotIn('DIAGNOSTIC', (self.root / guard.PROJECT).read_text())
        self.assertNotIn('Showdown sharing and clipboard export disabled',
                         (self.root / guard.BOX).read_text())

    def test_residual_repeat_patch_rejected(self):
        staged = guard.transform(self.root)
        for path, content in staged.items():
            path.write_text(content)
        with self.assertRaises(guard.OfflineRefusal):
            guard.transform(self.root)

    def test_each_residual_entry_point_fails_closed_on_source_drift(self):
        changes = [
            (guard.BOX, 'Clipboard.Default.SetTextAsync(text)', 'UnknownClipboard()'),
            (guard.BOX, 'Share.Default.RequestAsync(null)', 'UnknownShare()'),
            (residual.QR, 'Encoding.Latin1.GetString(payload)', 'UnknownEncoding()'),
            (residual.PARK, 'File.Copy(photo.Path, copy, overwrite: true)', 'UnknownCopy()'),
        ]
        for relative, before, after in changes:
            path = self.root / relative
            original = path.read_text()
            path.write_text(original.replace(before, after))
            try:
                with self.assertRaises(guard.OfflineRefusal):
                    guard.transform(self.root)
            finally:
                path.write_text(original)

    def test_refuse_repeat_patch(self):
        out = guard.transform(self.root)
        for p, content in out.items():
            p.write_text(content)
        with self.assertRaises(guard.OfflineRefusal):
            guard.transform(self.root)

if __name__ == "__main__":
    unittest.main()
