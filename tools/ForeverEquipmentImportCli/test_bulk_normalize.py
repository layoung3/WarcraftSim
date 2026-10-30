#!/usr/bin/env python3
"""Deterministic CP171 bulk normalizer tests. Data constructed here is SYNTHETIC."""
import contextlib
import csv
import hashlib
import io
import json
import pathlib
import tempfile
import unittest
from types import SimpleNamespace

import bulk_normalize as bn

BUILD="1.60.1.70707"
def csv_file(folder,name,headers,rows):
    p=folder/f"{name}.csv"
    with p.open("w",newline="",encoding="utf-8") as f:
        writer=csv.DictWriter(f,fieldnames=headers)
        writer.writeheader()
        writer.writerows(rows)
    return p

class BulkNormalizerv1Tests(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root=pathlib.Path(self.tmp.name)
        self.folder=self.root/BUILD
        self.folder.mkdir()
        self.sources={}
        self.sources["Item"]=csv_file(self.folder,"Item",
            ["ID","ClassID","SubclassID","InventoryType"],
            [{"ID":19102,"ClassID":2,"SubclassID":10,"InventoryType":17},
             {"ID":20368,"ClassID":2,"SubclassID":2,"InventoryType":15},
             {"ID":22504,"ClassID":4,"SubclassID":1,"InventoryType":5}])
        self.sources["ItemSparse"]=csv_file(self.folder,"ItemSparse",
            ["ID","Display_lang","InventoryType","RequiredLevel","OverallQualityID",
             "AllowableClass","StatModifier_bonusStat[0]","ItemSet","ItemDelay","ItemLevel"],
            [{"ID":19102,"Display_lang":"Crackling Staff","InventoryType":17,"RequiredLevel":60,
              "OverallQualityID":3,"AllowableClass":-1,"StatModifier_bonusStat[0]":5,
              "ItemSet":0,"ItemDelay":3100,"ItemLevel":65},
             {"ID":20368,"Display_lang":"Bland Bow of Steadiness","InventoryType":15,"RequiredLevel":60,
              "OverallQualityID":3,"AllowableClass":4,"StatModifier_bonusStat[0]":0,
              "ItemSet":0,"ItemDelay":1500,"ItemLevel":55},
             {"ID":22504,"Display_lang":"Plagueheart Robe","InventoryType":5,"RequiredLevel":60,
              "OverallQualityID":4,"AllowableClass":256,"StatModifier_bonusStat[0]":5,
              "ItemSet":900,"ItemDelay":0,"ItemLevel":88}])
        self.sources["ItemSet"]=csv_file(self.folder,"ItemSet",
            ["ID","Name_lang","ItemID[0]"],
            [{"ID":900,"Name_lang":"Plagueheart Raiment","ItemID[0]":22504}])
        self.sources["ItemSetSpell"]=csv_file(self.folder,"ItemSetSpell",
            ["ID","ItemSetID","SpellID","Threshold"],
            [{"ID":10,"ItemSetID":900,"SpellID":12345,"Threshold":2}])
        self.sources["ItemXItemEffect"]=csv_file(self.folder,"ItemXItemEffect",
            ["ID","ItemID","ItemEffectID"],
            [{"ID":20,"ItemID":22504,"ItemEffectID":33}])
        self.sources["ItemEffect"]=csv_file(self.folder,"ItemEffect",
            ["ID","SpellID","TriggerType"],
            [{"ID":33,"SpellID":18385,"TriggerType":0}])
        self.sources["ItemDamageTwoHand"]=csv_file(self.folder,"ItemDamageTwoHand",
            ["ID","ItemLevel","Quality[0]"],
            [{"ID":700,"ItemLevel":65,"Quality[0]":42}])
        self.sources["ItemArmorTotal"]=csv_file(self.folder,"ItemArmorTotal",
            ["ID","ItemLevel","Cloth","Leather","Mail","Plate"],
            [{"ID":701,"ItemLevel":88,"Cloth":42,"Leather":67,"Mail":73,"Plate":100}])
        self.write_report()
        self.reviewed=self.root/"resolved.json"
        self.reviewed.write_text(json.dumps([
            {"id":19102,"clientBuild":BUILD,"status":"complete",
             "verificationStatus":"client-db2-reviewed",
             "sourceUrl":"https://example.test/client-tooltip/19102",
             "inventoryType":17,"classID":2,"itemLevel":65,
             "stats":{"intellect":25,"spell-power":25},
             "weapon":{"type":"staff","hand":"two-hand","minimumDamage":138,
                       "maximumDamage":207,"speedSeconds":3.1}},
            {"id":22504,"clientBuild":BUILD,"status":"complete",
             "verificationStatus":"client-db2-reviewed",
             "sourceUrl":"https://example.test/client-tooltip/22504",
             "inventoryType":5,"classID":4,"itemLevel":88,
             "stats":{"intellect":22,"spell-power":51},
             "armor":138}
        ],sort_keys=True),encoding="utf-8")
        self.approval=self.root/"approval.json"
        self.approval.write_text(json.dumps({"clientBuild":BUILD,
            "resolvedValuesSha256":bn.sha256(self.reviewed),
            "reviewer":"fixture reviewer only",
            "sourceUrl":"https://example.test/client-tooltip",
            "status":"reviewed"}),encoding="utf-8")
    def write_report(self):
        (self.folder/"acquisition-report.json").write_text(json.dumps({
            "build":BUILD,"provider":"fixture-only",
            "tables":{k:{"sha256":bn.sha256(p),"url":f"https://example.test/{k}"} for k,p in self.sources.items()}
        }),encoding="utf-8")
    def args(self,**kwargs):
        data=dict(build=BUILD,directory=str(self.root),fixture=False,
            source_url="https://example.test/ItemSparse",
            imported_at="2026-10-30T17:00:00Z",
            catalog_version="fixture-70707-1",
            baseline=None,resolved_values=str(self.reviewed),
            approval=str(self.approval),hotfix=None,
            output=str(self.root/"catalog.json"),report=str(self.root/"report.json"))
        data.update(kwargs)
        return SimpleNamespace(**data)
    def run_normalizer(self,**kwargs):
        with contextlib.redirect_stdout(io.StringIO()):
            result=bn.normalize(self.args(**kwargs))
        self.assertEqual(0,result)
        return json.loads((self.root/"catalog.json").read_text()),json.loads((self.root/"report.json").read_text())
    def test_pinned_join_promotes_reviewed_and_retains_unknowns(self):
        manifest,report=self.run_normalizer()
        self.assertEqual(3,report["discoveredIds"])
        self.assertEqual(2,report["approvedCombatRecords"])
        self.assertEqual(1,report["metadataOnly"])
        self.assertEqual(0,len(report["rejected"]))
        self.assertEqual([20368],[x["id"] for x in manifest["metadataOnly"]])
        pending=manifest["metadataOnly"][0]
        self.assertEqual(["ranged"],pending["allowedSlots"])
        self.assertEqual(["hunter"],pending["allowedClasses"])
        self.assertEqual("rare",pending["quality"])
        self.assertEqual(60,pending["requiredLevel"])
        self.assertEqual(10,len(pending["rawStatIdentifiers"]))
        staff=next(x for x in manifest["items"] if x["id"]==19102)
        self.assertEqual(25,staff["stats"]["spell-power"])
        self.assertEqual(3.1,staff["weapon"]["speedSeconds"])
        self.assertEqual(["main-hand"],staff["allowedSlots"])
        robe=next(x for x in manifest["items"] if x["id"]==22504)
        self.assertEqual(138,robe["stats"]["armor"])
        self.assertEqual(["warlock"],robe["allowedClasses"])
        self.assertEqual("client-set-900",robe["setKey"])
        self.assertEqual(18385,robe["effects"][0]["spellId"])
        self.assertEqual("not-modeled",robe["effects"][0]["executionStatus"])
        self.assertEqual(2,len(report["scalingJoins"]))
        self.assertTrue(all(x["status"]=="budget-row-available-not-formula-verified"
                            for x in report["scalingJoins"]))
        self.assertEqual(2,manifest["sets"][0]["requiredPieces"])
        self.assertFalse(manifest["sets"][0]["modeled"])
    def test_input_hash_mismatch_and_schema_drift_fail_closed(self):
        with self.sources["ItemSparse"].open("a") as f:
            f.write("99999,Fake Bow,15,60,3,4,0,0,2200\n")
        with self.assertRaisesRegex(ValueError,"Checksum mismatch"):
            bn.normalize(self.args())
        self.write_report()
        csv_file(self.folder,"Item",["ID","WrongField"],[{"ID":19102,"WrongField":2}])
        self.write_report()
        with self.assertRaisesRegex(ValueError,"schema mismatch"):
            bn.normalize(self.args())
    def test_review_attestation_and_build_must_match(self):
        approval=json.loads(self.approval.read_text())
        approval["clientBuild"]="1.60.1.70685"
        self.approval.write_text(json.dumps(approval))
        with self.assertRaisesRegex(ValueError,"not explicitly reviewed"):
            bn.normalize(self.args())
    def test_hotfix_exact_base_replacement_and_conflict(self):
        overlay=self.root/"hotfix.json"
        overlay.write_text(json.dumps({"clientBuild":BUILD,"baseSha256":bn.sha256(self.reviewed),
            "sourceUrl":"https://example.test/hotfixes",
            "hotfixVersion":"fixture-hotfix-1","status":"reviewed",
            "changes":[{"id":19102,"reason":"checked test update",
                        "sourceUrl":"https://example.test/hotfix/19102",
                        "replace":{"stats":{"intellect":26,"spell-power":26}}}]}))
        first,report=self.run_normalizer(hotfix=str(overlay))
        staff=next(x for x in first["items"] if x["id"]==19102)
        self.assertEqual(26,staff["stats"]["intellect"])
        self.assertEqual(1,report["hotfixApplied"])
        data=json.loads(overlay.read_text())
        data["changes"].append(data["changes"][0])
        overlay.write_text(json.dumps(data))
        with self.assertRaisesRegex(ValueError,"Duplicate or unknown hotfix"):
            bn.normalize(self.args(hotfix=str(overlay)))
    def test_no_review_means_zero_combat_and_deterministic_snapshot(self):
        first,report=self.run_normalizer(resolved_values=None,approval=None)
        self.assertEqual(0,report["approvedCombatRecords"])
        self.assertEqual(3,report["metadataOnly"])
        serialized=(self.root/"catalog.json").read_bytes()
        self.run_normalizer(resolved_values=None,approval=None)
        self.assertEqual(serialized,(self.root/"catalog.json").read_bytes())

    def test_client_slot_conflict_never_promotes_reviewed_stats(self):
        p=self.sources["Item"]
        p.write_text(p.read_text().replace("19102,2,10,17","19102,2,10,14"))
        self.write_report()
        m,r=self.run_normalizer()
        self.assertEqual(1,r["approvedCombatRecords"])
        self.assertEqual(2,r["metadataOnly"])
        self.assertIn("inventory-conflict",next(x for x in m["metadataOnly"] if x["id"]==19102)["notes"])

    def test_conflicting_source_set_membership_rejected(self):
        p=self.sources["ItemSet"]
        with p.open("a",encoding="utf-8") as fh:
            fh.write("901,Other Set,22504\n")
        self.write_report()
        manifest,report=self.run_normalizer()
        self.assertEqual([900,901],report["conflictingSetMemberships"]["22504"])
        self.assertEqual(1,report["approvedCombatRecords"])
        self.assertEqual(2,report["metadataOnly"])
        self.assertIn("conflicting-set-membership",next(x for x in manifest["metadataOnly"] if x["id"]==22504)["notes"])

    def test_reviewed_speed_must_match_direct_client_delay(self):
        values=json.loads(self.reviewed.read_text(encoding="utf-8"))
        values[0]["weapon"]["speedSeconds"]=2.7
        self.reviewed.write_text(json.dumps(values,sort_keys=True),encoding="utf-8")
        approval=json.loads(self.approval.read_text(encoding="utf-8"))
        approval["resolvedValuesSha256"]=bn.sha256(self.reviewed)
        self.approval.write_text(json.dumps(approval),encoding="utf-8")
        m,r=self.run_normalizer()
        self.assertEqual(1,r["approvedCombatRecords"])
        self.assertTrue(any(x["id"]==19102 and
                            "speed conflicts" in x["reason"] for x in r["rejected"]))

    def test_baseline_replacement_requires_explicit_review_record(self):
        base=self.root/"baseline.json"
        base.write_text(json.dumps({"items":[{"id":19102}]}))
        m,r=self.run_normalizer(baseline=str(base))
        self.assertEqual(1,len(m["overrides"]))
        self.assertEqual(19102,m["overrides"][0]["itemId"])

if __name__ == "__main__":
    unittest.main()
