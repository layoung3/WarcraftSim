import unittest
from verify_formula_observations import compare

class FormulaObservationGateTests(unittest.TestCase):
    def setup_sample(self):
        preview={"clientBuild":"1.60.1.70707","records":[{
            "id":272504,"itemLevel":65,"requiredLevel":60,"quality":4,
            "slot":8,"classID":4,
            "candidate":{"stats":{"intellect":15,"stamina":26,"spell-power":14,"armor":69},
                         "weapon":None,"unresolved":[]}
        }]}
        full={"id":272504,"clientBuild":"1.60.1.70707",
              "verificationStatus":"independent-tooltip-reviewed",
              "sourceUrl":"https://example.test/forever/client-70707/item/272504",
              "completeStaticStats":True,
              "itemLevel":65,"requiredLevel":60,"quality":4,"inventoryType":8,
              "classID":4,"stats":{"intellect":15,"stamina":26,"spell-power":14,"armor":69},
              "armor":69,"weapon":None}
        return preview,full
    def test_complete_independently_matched_item_becomes_review_proposal(self):
        preview,full=self.setup_sample()
        matched,decisions=compare(preview,[full])
        self.assertEqual(1,len(matched))
        self.assertEqual(0,len(decisions[0]["issues"]))
        self.assertEqual("client-db2-reviewed",matched[0]["verificationStatus"])
        self.assertEqual(69,matched[0]["armor"])
        self.assertNotIn("armor",matched[0]["stats"])
    def test_missing_armor_or_partial_stat_coverage_cannot_approve(self):
        preview,full=self.setup_sample()
        del full["armor"]
        full["completeStaticStats"]=False
        matched,decisions=compare(preview,[full])
        self.assertEqual([],matched)
        self.assertIn("armor-not-independently-confirmed",decisions[0]["issues"])
        self.assertIn("partial-tooltip-stat-coverage",decisions[0]["issues"])
    def test_source_build_mismatch_cannot_approve_even_when_stats_match(self):
        preview,full=self.setup_sample()
        full["clientBuild"]="1.60.1.70532"
        matched,decisions=compare(preview,[full])
        self.assertEqual([],matched)
        self.assertIn("different-client-build",decisions[0]["issues"])
    def test_unresolved_effect_stat_type_and_missing_source_cannot_approve(self):
        preview,full=self.setup_sample()
        preview["records"][0]["candidate"]["unresolved"]=["unsupported-stat-id-47"]
        full["sourceUrl"]="http://not-verified.example"
        matched,decisions=compare(preview,[full])
        self.assertEqual([],matched)
        self.assertIn("unsupported-stat-id-47",decisions[0]["issues"])
        self.assertIn("missing-https-tooltip-source",decisions[0]["issues"])
    def test_two_identical_observations_are_rejected(self):
        preview,full=self.setup_sample()
        with self.assertRaisesRegex(ValueError,"Duplicate"):
            compare(preview,[full,full])
    def test_observed_stat_discrepancy_is_quarantined(self):
        preview,full=self.setup_sample()
        full["stats"]["intellect"]=16
        matched,decisions=compare(preview,[full])
        self.assertEqual([],matched)
        self.assertIn("combat-stat-mismatch",decisions[0]["issues"])

if __name__=="__main__":
    unittest.main()
