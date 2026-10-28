"""Preview-only PKSM multi-import refusal and exact-source transformation tests."""
import unittest

from scripts.pkforge_disable_bulk_import import adapt_picker, adapt_flow
from scripts.pkforge_official_core_probe import ProbeRefusal

PICKER = """using PKForge.Domain;
public sealed class AndroidDocumentPicker
{
    public ValueTask<IReadOnlyList<PickedDocument>> PickManyAsync(CancellationToken cancellationToken = default)
    {
        var documents = new List<PickedDocument>();
        return new ValueTask<IReadOnlyList<PickedDocument>>(documents);
    }

    private bool HandleMultiResult(int requestCode, Result resultCode, Intent? data)
    {
        return false;
    }
}
"""
FLOW = """public static class PksmTransferFlow
{
    public static async Task<string?> ImportAsync(Grid host, IBankService bank)
    {
        var documents = await picker.PickManyAsync();
        foreach (var document in documents) files.Add(document);
        PksmBankTransfer.Apply(bank, BoxNames, plans, mode);
        return null;
    }

    private static (List<PksmImportPlan>, List<string>) BuildPlans(IEnumerable<(string, byte[])> files)
    {
        return ([], []);
    }
}
"""


class BulkImportRefusalTests(unittest.TestCase):
    def test_multi_picker_never_allocates_or_launches(self):
        result = adapt_picker(PICKER)
        segment = result.split("PickManyAsync(CancellationToken cancellationToken = default)", 1)[1].split(
            "private bool HandleMultiResult", 1)[0]
        self.assertIn("PKSM_MULTI_IMPORT_DISABLED", segment)
        self.assertIn("throw new NotSupportedException", segment)
        self.assertNotIn("new List<PickedDocument>", segment)
        self.assertNotIn("StartActivityForResult", segment)
        self.assertNotIn("_pendingMany =", segment)

    def test_import_flow_has_no_reachable_bank_mutation(self):
        result = adapt_flow(FLOW)
        segment = result.split("ImportAsync(Grid host, IBankService bank)", 1)[1].split(
            "private static (List<PksmImportPlan>", 1)[0]
        self.assertIn("PKSM_MULTI_IMPORT_DISABLED", segment)
        self.assertIn("Task.FromResult<string?>", segment)
        self.assertNotIn("PickManyAsync", segment)
        self.assertNotIn("ReadAsync", segment)
        self.assertNotIn("PksmBankTransfer.Apply", segment)

    def test_repeated_adaptation_refused(self):
        with self.assertRaises(ProbeRefusal):
            adapt_picker(adapt_picker(PICKER))
        with self.assertRaises(ProbeRefusal):
            adapt_flow(adapt_flow(FLOW))

    def test_unexpected_source_refused(self):
        with self.assertRaises(ProbeRefusal):
            adapt_picker(PICKER.replace("PickManyAsync", "PickMultipleAsync"))
        with self.assertRaises(ProbeRefusal):
            adapt_flow(FLOW.replace("ImportAsync(Grid", "ImportFilesAsync(Grid"))

    def test_duplicate_anchors_refused(self):
        with self.assertRaises(ProbeRefusal):
            adapt_picker(PICKER + PICKER)
        with self.assertRaises(ProbeRefusal):
            adapt_flow(FLOW + FLOW)


if __name__ == "__main__":
    unittest.main()
