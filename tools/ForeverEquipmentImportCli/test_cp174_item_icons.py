import importlib.util
import json
from pathlib import Path
import tempfile
import unittest


MODULE_PATH = Path(__file__).resolve().parent / "extract_item_icons.py"
SPEC = importlib.util.spec_from_file_location("cp174_item_icons", MODULE_PATH)
assert SPEC and SPEC.loader
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class Cp174ItemIconJoinTests(unittest.TestCase):
    def fixtures(self, folder):
        root = Path(folder)
        modified = root / "ItemModifiedAppearance.csv"
        appearances = root / "ItemAppearance.csv"
        modified.write_text(
            "ItemID,ItemAppearanceID\n"
            "111,1001\n"
            "222,1002\n"
            "222,1003\n"
            "333,0\n",
            encoding="utf-8")
        appearances.write_text(
            "ID,DefaultIconFileDataID\n"
            "1001,5001\n"
            "1002,5002\n"
            "1003,5003\n",
            encoding="utf-8")
        return modified, appearances

    def test_exact_unique_join_only_and_quarantines_ambiguity(self):
        with tempfile.TemporaryDirectory() as folder:
            modified, appearances = self.fixtures(folder)
            report = MODULE.candidates(modified, appearances, [111,222,333],
                                       "1.60.1.70707")
            self.assertEqual([{"itemId":111,"iconFileDataId":5001,
                               "status":"unapproved-client-join-candidate"}],
                             report["candidates"])
            self.assertEqual([222], [x["itemId"] for x in report["ambiguous"]])
            self.assertEqual([333], report["unresolvedItemIds"])
            self.assertFalse(report["automaticApproval"])
            self.assertEqual(64, len(report["source"]["ItemAppearanceSha256"]))

    def test_invalid_build_and_missing_column_fail_closed(self):
        with tempfile.TemporaryDirectory() as folder:
            modified, appearances = self.fixtures(folder)
            with self.assertRaisesRegex(ValueError, "explicit Forever"):
                MODULE.candidates(modified, appearances, [111], "classic")
            appearances.write_text("ID,Icon\n1001,5001\n", encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "expected columns"):
                MODULE.candidates(modified, appearances, [111], "1.60.1.70707")


if __name__ == "__main__":
    unittest.main()
