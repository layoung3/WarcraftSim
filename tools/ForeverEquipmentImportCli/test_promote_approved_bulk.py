import copy
import unittest
import promote_approved_bulk as pub


class BulkPromotionTests(unittest.TestCase):
    def setUp(self):
        self.base = {"items": [{"id": 40, "name": "Old"}]}
        self.effects = {"items": [{"id": 99, "name": "Executable Trinket"}]}
        self.reviewed = {"catalogVersion": "old", "contentVersion": "old",
                         "clientBuild": "mixed-reviewed", "sourceName": "old",
                         "sourceUrl": "https://example.org/old", "items": [],
                         "sets": [], "overrides": []}
        self.record = {"id": 111, "name": "Verified Sword",
                       "allowedSlots": ["main-hand"], "sourceBuild": "1.60.1.70707",
                       "sourceStatus": "forever-client-db2-reviewed",
                       "verificationStatus": "client-db2-reviewed",
                       "sourceUrl": "https://example.org/item/111",
                       "stats": {"strength": 5},
                       "weapon": {"hand": "one-hand", "type": "sword",
                                  "minimumDamage": 60, "maximumDamage": 95,
                                  "speedSeconds": 2.4}}
        self.batch = {"clientBuild": "1.60.1.70707",
                      "sourceUrl": "https://example.org/items",
                      "items": [self.record], "overrides": [], "sets": []}
        self.report = {"approvedCombatRecords": 1,
                       "reviewedValuesSha256": "a" * 64,
                       "reviewApproval": {"status": "reviewed",
                                          "clientBuild": "1.60.1.70707",
                                          "resolvedValuesSha256": "a" * 64,
                                          "reviewer": "fixture",
                                          "sourceUrl": "https://example.org/items"}}

    def run_promotion(self, batch=None, reviewed=None):
        return pub.promote(batch or self.batch, self.report,
                           reviewed or self.reviewed, self.base, self.effects)

    def test_reproducible_and_idempotent(self):
        first, info = self.run_promotion()
        self.assertEqual(1, info["accepted"])
        second, info2 = self.run_promotion(reviewed=first)
        self.assertEqual(0, info2["accepted"])
        self.assertEqual(1, info2["alreadyPresent"])
        self.assertTrue(info2["unchanged"])
        self.assertEqual(pub.canonical(first), pub.canonical(second))
        self.assertEqual(pub.canonical(first),
                         pub.canonical(self.run_promotion()[0]))

    def test_protects_cp172_owned_item(self):
        bad = copy.deepcopy(self.batch)
        bad["items"][0]["id"] = 99
        _, info = self.run_promotion(batch=bad)
        self.assertEqual(0, info["accepted"])
        self.assertIn("cp172-reserved-executable-or-effect-owned-item",
                      info["rejected"][0]["reasons"])

    def test_rejects_missing_weapon_data(self):
        bad = copy.deepcopy(self.batch)
        del bad["items"][0]["weapon"]
        _, info = self.run_promotion(batch=bad)
        self.assertIn("missing-reviewed-weapon-damage-or-speed",
                      info["rejected"][0]["reasons"])

    def test_requires_checksum_approved_values(self):
        bad = copy.deepcopy(self.report)
        bad["reviewApproval"]["resolvedValuesSha256"] = "wrong"
        with self.assertRaisesRegex(ValueError, "Approval"):
            pub.promote(self.batch, bad, self.reviewed, self.base, self.effects)

    def test_rejects_unapproved_baseline_overwrite(self):
        bad = copy.deepcopy(self.batch)
        bad["items"][0]["id"] = 40
        _, info = self.run_promotion(batch=bad)
        self.assertIn("existing-item-no-explicit-approved-override",
                      info["rejected"][0]["reasons"])


if __name__ == "__main__":
    unittest.main()
