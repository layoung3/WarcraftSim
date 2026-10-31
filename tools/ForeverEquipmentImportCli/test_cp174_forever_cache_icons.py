import importlib.util
from pathlib import Path
import unittest

MODULE_PATH=Path(__file__).resolve().parent/"review_forever_cache_icons.py"
SPEC=importlib.util.spec_from_file_location("cp174_forever_cache_icons",MODULE_PATH)
assert SPEC and SPEC.loader
MODULE=importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class Cp174ForeverItemIconReviewTests(unittest.TestCase):
    def test_exact_forever_item_id_name_and_filename_required(self):
        catalog={"items":[
            {"id":101,"name":"Exact Staff"},
            {"id":102,"name":"Renamed Helm"},
            {"id":103,"name":"Ambiguous Wand"},
            {"id":104,"name":"Missing Icon"},
        ]}
        source=[
            {"itemId":101,"name":"Exact Staff","itemExpansion":"Forever","iconName":"inv_staff_20"},
            {"itemId":101,"name":"Different Staff","itemExpansion":"Forever","iconName":"inv_staff_01"},
            {"itemId":102,"name":"Renamed Helm","itemExpansion":"Classic Era","iconName":"inv_helmet_36"},
            {"itemId":103,"name":"Ambiguous Wand","itemExpansion":"Forever","iconName":"inv_wand_01"},
            {"itemId":103,"name":"Ambiguous Wand","itemExpansion":"Forever","iconName":"inv_wand_02"},
            {"itemId":104,"name":"Missing Icon","itemExpansion":"Forever","iconName":"../unsafe"},
        ]
        report=MODULE.analyze(catalog,source)
        self.assertEqual([101],[x["itemId"] for x in report["candidates"]])
        self.assertEqual([103],[x["itemId"] for x in report["ambiguous"]])
        self.assertEqual([102,104],[x["itemId"] for x in report["unresolved"]])

    def test_wrong_schema_not_accepted(self):
        with self.assertRaises(ValueError):
            MODULE.analyze({"items":[]},42)


if __name__=="__main__":
    unittest.main()
