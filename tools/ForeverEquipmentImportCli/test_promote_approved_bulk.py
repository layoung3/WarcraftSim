"""Synthetic fixtures only; no item values or receipts here certify real catalog data."""
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
                       "itemClass":"weapon","itemSubclass":"sword","allowedClasses":["warrior"],
                       "classRestrictionEvidence":{
                           "itemId":111,"sourceKind":"client-item-sparse","rawMask":1,
                           "status":"restricted","dataConfidence":"provisional",
                           "sourceAllowedClasses":["warrior"],
                           "sourceBuild":"1.60.1.70707","sourceUrl":"https://example.test/ItemSparse",
                           "sourceSha256":"b"*64},
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

        hashes={"Item":"c"*64,"ItemSparse":"b"*64}
        self.report["tableSha256"]=hashes
        self.report["reviewApproval"]["sourceTableSha256"]=copy.deepcopy(hashes)
        self.batch.update({key:copy.deepcopy(self.report[key]) for key in
                           ("tableSha256","reviewApproval","reviewedValuesSha256")})

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

    def test_duplicate_identical_replays_are_rejected(self):
        existing,_=self.run_promotion()
        batch=copy.deepcopy(self.batch)
        batch["items"].append(copy.deepcopy(batch["items"][0]))
        report=copy.deepcopy(self.report)
        report["approvedCombatRecords"]=2
        unchanged,info=pub.promote(batch,report,existing,self.base,self.effects)
        self.assertEqual(existing,unchanged)
        self.assertEqual(0,info["accepted"])
        self.assertIn("invalid-or-duplicate-batch-id",info["rejected"][0]["reasons"])

    def test_boolean_item_id_is_not_integer_identity(self):
        batch=copy.deepcopy(self.batch)
        batch["items"][0]["id"]=True
        batch["items"][0]["classRestrictionEvidence"]["itemId"]=1
        _,info=self.run_promotion(batch=batch)
        self.assertIn("invalid-or-duplicate-batch-id",info["rejected"][0]["reasons"])

    def test_all_source_urls_require_https_hostname_and_string_type(self):
        for bad in (None,17,[],"http://example.test/x","https://","https:///x",
                    "https://:443/x","https://[broken/x","https://example.test:bad/x",
                    "https://bad host/x"):
            for field in ("batch","approval","row","evidence"):
                with self.subTest(field=field,url=bad):
                    batch=copy.deepcopy(self.batch)
                    report=copy.deepcopy(self.report)
                    if field=="batch":batch["sourceUrl"]=bad
                    elif field=="approval":
                        report["reviewApproval"]["sourceUrl"]=bad
                        batch["reviewApproval"]["sourceUrl"]=bad
                    elif field=="row":batch["items"][0]["sourceUrl"]=bad
                    else:batch["items"][0]["classRestrictionEvidence"]["sourceUrl"]=bad
                    if field in ("batch","approval"):
                        with self.assertRaises(ValueError):
                            pub.promote(batch,report,self.reviewed,self.base,self.effects)
                    else:
                        _,info=pub.promote(batch,report,self.reviewed,self.base,self.effects)
                        self.assertEqual(0,info["accepted"])
                        self.assertTrue(info["rejected"])

    def test_https_scheme_and_approval_digest_are_case_insensitive(self):
        batch=copy.deepcopy(self.batch)
        report=copy.deepcopy(self.report)
        batch["sourceUrl"]="HTTPS://example.test/source"
        batch["items"][0]["sourceUrl"]="HTTPS://example.test/item/111"
        batch["items"][0]["classRestrictionEvidence"]["sourceUrl"]="HTTPS://example.test/ItemSparse"
        report["reviewApproval"]["sourceUrl"]="HTTPS://example.test/review"
        report["reviewApproval"]["resolvedValuesSha256"]="A"*64
        batch["reviewApproval"]=copy.deepcopy(report["reviewApproval"])
        _,info=pub.promote(batch,report,self.reviewed,self.base,self.effects)
        self.assertEqual(1,info["accepted"])

    def test_unpinned_build_is_rejected(self):
        batch=copy.deepcopy(self.batch)
        report=copy.deepcopy(self.report)
        for target in (batch,batch["reviewApproval"],report["reviewApproval"]):
            target["clientBuild"]="made-up-build"
        batch["items"][0]["sourceBuild"]="made-up-build"
        batch["items"][0]["classRestrictionEvidence"]["sourceBuild"]="made-up-build"
        with self.assertRaisesRegex(ValueError,"pinned"):
            pub.promote(batch,report,self.reviewed,self.base,self.effects)

    def test_unknown_can_promote_only_after_valid_evidence_is_supplied(self):
        unknown=copy.deepcopy(self.batch)
        unknown["items"][0]["classRestrictionEvidence"].update(status="unknown",rawMask=None,
                                                             sourceAllowedClasses=[])
        before=copy.deepcopy(self.reviewed)
        unchanged,info=self.run_promotion(batch=unknown)
        self.assertEqual(0,info["accepted"])
        self.assertEqual(before,unchanged)
        self.assertIn("unknown-class-restriction-evidence",info["rejected"][0]["reasons"][0])
        updated,info=self.run_promotion()
        self.assertEqual(1,info["accepted"])
        self.assertEqual("restricted",updated["items"][0]["classRestrictionEvidence"]["status"])

    def test_missing_receipt_cannot_bypass_validation_as_identical_legacy_row(self):
        batch=copy.deepcopy(self.batch)
        del batch["items"][0]["classRestrictionEvidence"]
        existing=copy.deepcopy(self.reviewed)
        existing["items"]=copy.deepcopy(batch["items"])
        unchanged,info=self.run_promotion(batch=batch,reviewed=existing)
        self.assertEqual(0,info["accepted"])
        self.assertTrue(info["rejected"])
        self.assertEqual(existing,unchanged)

    def test_untouched_legacy_rows_stay_unchanged_when_new_evidence_is_valid(self):
        existing=copy.deepcopy(self.reviewed)
        legacy={"id":500,"name":"Synthetic legacy record","allowedClasses":[]}
        existing["items"]=[legacy]
        updated,info=self.run_promotion(reviewed=existing)
        self.assertEqual(1,info["accepted"])
        self.assertEqual(legacy,next(x for x in updated["items"] if x["id"]==500))
        self.assertEqual([legacy],existing["items"])

    def test_unknown_sentinel_and_invalid_mask_conventions_cannot_promote(self):
        for mask in (None,0,-1,65535,4294967295,-2,32,True):
            with self.subTest(mask=mask):
                batch=copy.deepcopy(self.batch)
                batch["items"][0]["classRestrictionEvidence"]["rawMask"]=mask
                _,info=self.run_promotion(batch=batch)
                self.assertEqual(0,info["accepted"])
                self.assertTrue(info["rejected"])
        batch=copy.deepcopy(self.batch)
        batch["items"][0]["classRestrictionEvidence"]["status"]="unrestricted"
        self.assertTrue(self.run_promotion(batch=batch)[1]["rejected"])

    def test_receipt_identity_build_source_and_decode_are_checked(self):
        for field,value in (("itemId",999),("sourceBuild","1.60.1.70532"),
                            ("sourceKind","inferred-proficiency"),("sourceSha256","d"*64),
                            ("dataConfidence","unknown"),("dataConfidence","verified"),
                            ("sourceUrl",17),("sourceUrl","http://example.test/source"),
                            ("sourceAllowedClasses",["mage"]),
                            ("sourceAllowedClasses",["warrior","WARRIOR"])):
            with self.subTest(field=field,value=value):
                batch=copy.deepcopy(self.batch)
                batch["items"][0]["classRestrictionEvidence"][field]=value
                _,info=self.run_promotion(batch=batch)
                self.assertTrue(info["rejected"])

    def test_effective_classes_must_equal_source_mask_with_armor_proficiency(self):
        batch=copy.deepcopy(self.batch)
        row=batch["items"][0]
        row.update(itemClass="armor",itemSubclass="mail",allowedSlots=["chest"],
                   allowedClasses=["warrior","paladin","hunter","shaman"])
        row["classRestrictionEvidence"].update(rawMask=1503,
            sourceAllowedClasses=["warrior","paladin","hunter","rogue","priest",
                                  "shaman","mage","warlock","druid"])
        self.assertEqual(1,self.run_promotion(batch=batch)[1]["accepted"])
        for allowed in ([],["hunter"],["warrior","paladin","hunter","shaman","mage"],
                        ["warrior","paladin","hunter","shaman","SHAMAN"]):
            with self.subTest(allowed=allowed):
                row["allowedClasses"]=allowed
                self.assertTrue(self.run_promotion(batch=batch)[1]["rejected"])

    def test_both_source_table_hashes_are_bound_to_approval(self):
        for table in ("Item","ItemSparse"):
            with self.subTest(table=table):
                batch=copy.deepcopy(self.batch)
                report=copy.deepcopy(self.report)
                report["reviewApproval"]["sourceTableSha256"][table]="d"*64
                batch["reviewApproval"]=copy.deepcopy(report["reviewApproval"])
                with self.assertRaisesRegex(ValueError,"source checksums"):
                    pub.promote(batch,report,self.reviewed,self.base,self.effects)

    def test_batch_cannot_detach_itself_from_report_approval(self):
        batch=copy.deepcopy(self.batch)
        batch["reviewedValuesSha256"]="d"*64
        with self.assertRaisesRegex(ValueError,"Batch/report"):
            self.run_promotion(batch=batch)

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
        with self.assertRaisesRegex(ValueError, "[Aa]pproval"):
            pub.promote(self.batch, bad, self.reviewed, self.base, self.effects)

    def test_rejects_unapproved_baseline_overwrite(self):
        bad = copy.deepcopy(self.batch)
        bad["items"][0]["id"] = 40
        _, info = self.run_promotion(batch=bad)
        self.assertIn("existing-item-no-explicit-approved-override",
                      info["rejected"][0]["reasons"])


if __name__ == "__main__":
    unittest.main()
