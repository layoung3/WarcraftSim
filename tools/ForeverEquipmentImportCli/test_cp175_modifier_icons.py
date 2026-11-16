import importlib.util
import json
from pathlib import Path
import unittest


ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location(
    "cp175_modifier_icons", Path(__file__).with_name("review_forever_modifier_icons.py"))
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


def entry(key="food", name="Exact Food (Well Fed)", item=10, spell=0):
    return {"key": key, "name": name, "category": "consumable", "itemId": item, "spellId": spell}


class Cp175ModifierIconReviewTests(unittest.TestCase):
    def test_exact_id_name_and_explicit_ui_suffix_only(self):
        source = {"itemIcons": [{"id": 10, "name": "Exact Food", "icon": "inv_food_1"}], "spellIcons": []}
        catalog = [entry(), entry("different-id", item=11), entry("different-name", name="Other Food"),
                   entry("unrecognized-suffix", name="Exact Food (Special)")]
        report = MODULE.analyze(catalog, source)
        self.assertEqual(["food"], [x["key"] for x in report["records"]])
        self.assertEqual(3, len(report["unresolved"]))

    def test_spell_rank_conflict_is_rejected(self):
        source = {"itemIcons": [], "spellIcons": [{"id": 20, "name": "Exact Buff", "icon": "spell_buff", "rank": 5}]}
        good = entry("buff", "Exact Buff (Rank 5)", None, 20)
        wrong = entry("wrong-rank", "Exact Buff (Rank 6)", None, 20)
        report = MODULE.analyze([good, wrong], source)
        self.assertEqual(["buff"], [x["key"] for x in report["records"]])
        self.assertEqual("spell", report["records"][0]["sourceKind"])

    def test_unsafe_and_ambiguous_keys_stay_unresolved(self):
        for icons in (["../unsafe"], ["inv_food_1", "inv_food_2"]):
            source = {"itemIcons": [{"id": 10, "name": "Exact Food", "icon": key} for key in icons], "spellIcons": []}
            report = MODULE.analyze([entry()], source)
            self.assertEqual([], report["records"])
            self.assertEqual(1, len(report["unresolved"]))

    def test_names_alone_never_establish_missing_ids(self):
        source = {"itemIcons": [{"id": 10, "name": "Exact Food", "icon": "inv_food_1"}], "spellIcons": []}
        report = MODULE.analyze([entry(item=None)], source)
        self.assertEqual([], report["records"])
        self.assertEqual("no-existing-item-or-spell-id", report["unresolved"][0]["reason"])

    def test_wrong_schema_and_changed_source_bytes_are_rejected(self):
        with self.assertRaises(ValueError):
            MODULE.analyze([], {"items": []})
        with self.assertRaisesRegex(ValueError, "checksum"):
            MODULE.build_manifest([], b'{"itemIcons":[],"spellIcons":[]}')

    def test_reviewed_manifest_covers_every_catalog_identity_once(self):
        catalog = MODULE.catalog_entries(ROOT / MODULE.DEFAULT_CATALOG)
        manifest = json.loads((ROOT / MODULE.DEFAULT_MANIFEST).read_text(encoding="utf-8"))
        rows = manifest["records"] + manifest["unresolved"]
        identities = lambda xs: sorted((x["key"], x["name"], x["category"], x["itemId"], x["spellId"]) for x in xs)
        self.assertEqual(identities(catalog), identities(rows))
        self.assertEqual(25, len(manifest["records"]))
        self.assertEqual(MODULE.SOURCE_COMMIT, manifest["sourceCommit"])
        self.assertEqual(MODULE.SOURCE_SHA256, manifest["sourceSha256"])
        for row in manifest["records"]:
            self.assertEqual(MODULE.source_name(row["name"]), row["sourceName"])
            self.assertRegex(row["iconKey"], "^[a-z0-9_]+$")
            self.assertEqual(MODULE.STATUS, row["iconSourceStatus"])
        conflicts = {x["key"]: x for x in manifest["unresolved"]}
        self.assertEqual(["Elixir of Giants"], conflicts["elixir-of-greater-strength"]["observedSourceNames"])
        self.assertEqual(["Elixir of Fortitude"], conflicts["elixir-of-lesser-fortitude"]["observedSourceNames"])

    def test_equipment_join_preserves_previous_source_and_requires_exact_identity(self):
        import hashlib
        from unittest.mock import patch
        source = {"itemIcons": [{"id": 10, "name": "Exact Food", "icon": "inv_food_1"},
                                {"id": 11, "name": "Other Item", "icon": "inv_item_2"}], "spellIcons": []}
        payload = json.dumps(source).encode()
        previous = {"candidates": [{"itemId": 10, "itemName": "Exact Food", "iconKey": "inv_food_1"}]}
        items = [{"id": 10, "name": "Exact Food"}, {"id": 11, "name": "Other Item"},
                 {"id": 12, "name": "Unknown Item"}]
        with patch.object(MODULE, "SOURCE_SHA256", hashlib.sha256(payload).hexdigest()):
            result = MODULE.build_equipment_manifest(items, payload, previous)
            self.assertEqual([11], [x["itemId"] for x in result["candidates"]])
            self.assertEqual("gunba/wow-forever-sim", result["sourceRepository"])
            self.assertEqual(MODULE.SOURCE_COMMIT, result["sourceCommit"])
            wrong_name = [{"id": 11, "name": "Renamed Item"}, {"id": 12, "name": "Other Item (Forever)"}]
            self.assertEqual([], MODULE.build_equipment_manifest(wrong_name, payload, previous)["candidates"])
            previous["candidates"][0]["iconKey"] = "conflicting_icon"
            with self.assertRaisesRegex(ValueError, "conflict"):
                MODULE.build_equipment_manifest(items, payload, previous)

    def test_equipment_rejects_duplicate_ids_and_changed_source(self):
        with self.assertRaisesRegex(ValueError, "checksum"):
            MODULE.build_equipment_manifest([], b"{}", {"candidates": []})
        with self.assertRaisesRegex(ValueError, "duplicate"):
            MODULE.equipment_identity_entries([{"id": 1, "name": "First"}, {"id": 1, "name": "Second"}])

    def test_reviewed_equipment_manifest_is_display_only_and_separately_pinned(self):
        manifest = json.loads((ROOT / MODULE.DEFAULT_EQUIPMENT_MANIFEST).read_text())
        self.assertEqual(MODULE.SOURCE_COMMIT, manifest["sourceCommit"])
        self.assertEqual(MODULE.SOURCE_SHA256, manifest["sourceSha256"])
        self.assertEqual([11287, 14152, 19682, 19683, 19684], [x["itemId"] for x in manifest["candidates"]])
        for row in manifest["candidates"]:
            self.assertEqual({"itemId", "itemName", "iconKey", "sourceStatus"}, set(row))
            self.assertEqual(MODULE.STATUS, row["sourceStatus"])


if __name__ == "__main__":
    unittest.main()
