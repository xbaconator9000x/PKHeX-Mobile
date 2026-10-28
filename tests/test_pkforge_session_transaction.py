"""Fail-closed activation-transaction adaptation tests on pinned service shape."""
import unittest

from scripts.pkforge_official_core_probe import ProbeRefusal
from scripts.pkforge_provider_origin_binding import adapt_service
from scripts.pkforge_session_transaction import adapt_transaction
from test_pkforge_provider_origin_binding import PINNED_SERVICE


class SessionTransactionTests(unittest.TestCase):
    def setUp(self):
        self.bound = adapt_service(PINNED_SERVICE)

    def test_atomic_activation_and_invalidation(self):
        result = adapt_transaction(self.bound)
        self.assertIn("lock (_activationGate)", result)
        self.assertIn("_activationTestHook?.Invoke()", result)
        self.assertIn("var binding = new ProviderOriginBinding", result)
        self.assertLess(result.index("var binding = new ProviderOriginBinding"),
                        result.index("engineSession.MakeActive();"))
        self.assertIn("cancellationToken.ThrowIfCancellationRequested();", result)
        self.assertIn("try { engineSession.Dispose(); } catch", result)
        self.assertIn("CurrentSession = null;", result)
        self.assertIn("Current = null;", result)
        self.assertEqual(result.count("Interlocked.Increment(ref _openingGeneration)"), 4)
        self.assertGreaterEqual(result.count("lock (_activationGate)"), 5)

    def test_repeated_adaptation_refused(self):
        with self.assertRaises(ProbeRefusal):
            adapt_transaction(adapt_transaction(self.bound))

    def test_missing_provider_origin_refused(self):
        with self.assertRaises(ProbeRefusal):
            adapt_transaction(PINNED_SERVICE)

    def test_unexpected_commit_shape_refused(self):
        with self.assertRaises(ProbeRefusal):
            adapt_transaction(self.bound.replace("engineSession.MakeActive();",
                                                 "engineSession.Activate();"))

    def test_duplicate_methods_refused(self):
        with self.assertRaises(ProbeRefusal):
            adapt_transaction(self.bound.replace(
                "    public void Close()",
                "    public void Close()\n    public void Close()"))


if __name__ == "__main__":
    unittest.main()
