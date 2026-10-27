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

    def test_binary_files_must_not_be_committed(self):
        self.assertTrue({".gba", ".sav", ".apk", ".jks"}.issubset(FORBIDDEN_SUFFIXES))

if __name__ == "__main__":
    unittest.main()
