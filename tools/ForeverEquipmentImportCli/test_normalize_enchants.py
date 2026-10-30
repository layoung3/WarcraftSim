import csv
import hashlib
import json
import tempfile
import unittest
from pathlib import Path

from normalize_enchants import read

BUILD="1.60.1.70707"
def write_csv(folder,name,fields,rows):
    path=folder/(name+".csv")
    with path.open("w",newline="",encoding="utf-8") as output:
        writer=csv.DictWriter(output,fieldnames=fields)
        writer.writeheader()
        writer.writerows(rows)
    return path

class Cp172EnchantSourceIngestion(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root=Path(self.temp.name)
        self.folder=self.root/BUILD
        self.folder.mkdir()
        self.ench=write_csv(self.folder,"SpellItemEnchantment",
            ["ID","Name_lang","Effect_0","EffectArg_0","EffectPointsMin_0",
             "Effect_1","EffectArg_1","EffectPointsMin_1",
             "Effect_2","EffectArg_2","EffectPointsMin_2"],[
            dict(ID=2504,Name_lang="Spell Power",Effect_0=5,EffectArg_0=45,
                 EffectPointsMin_0=30,Effect_1=0,EffectArg_1=0,
                 EffectPointsMin_1=0,Effect_2=0,EffectArg_2=0,EffectPointsMin_2=0),
            dict(ID=803,Name_lang="Fiery Weapon",Effect_0=1,EffectArg_0=13898,
                 EffectPointsMin_0=0,Effect_1=0,EffectArg_1=0,
                 EffectPointsMin_1=0,Effect_2=0,EffectArg_2=0,EffectPointsMin_2=0),
            dict(ID=2628,Name_lang="Brilliant Wizard Oil",Effect_0=5,EffectArg_0=45,
                 EffectPointsMin_0=36,Effect_1=5,EffectArg_1=32,
                 EffectPointsMin_1=14,Effect_2=0,EffectArg_2=0,EffectPointsMin_2=0),
            ])
        self.spells=write_csv(self.folder,"SpellEffect",
            ["ID","SpellID","Effect","EffectMiscValue_0"],[
                {"ID":1,"SpellID":22749,"Effect":53,"EffectMiscValue_0":2504},
                {"ID":2,"SpellID":13898,"Effect":53,"EffectMiscValue_0":803},
                {"ID":3,"SpellID":25122,"Effect":54,"EffectMiscValue_0":2628}
            ])
        self.refresh()
    def refresh(self):
        record={"build":BUILD,"tables":{}}
        for name in ("SpellItemEnchantment","SpellEffect"):
            blob=(self.folder/(name+".csv")).read_bytes()
            record["tables"][name]={"sha256":hashlib.sha256(blob).hexdigest()}
        (self.folder/"acquisition-report.json").write_text(json.dumps(record))
    def test_static_and_nonmodeled_effects_are_separate(self):
        report=read(self.root,BUILD)
        self.assertEqual(3,report["discovered"])
        self.assertEqual(2,report["staticCandidates"])
        self.assertEqual(1,report["withUnmodeledEffects"])
        by_id={x["enchantId"]:x for x in report["records"]}
        self.assertEqual({"spell-power":30},by_id[2504]["stats"])
        self.assertEqual([{"applicationSpellId":22749,"temporary":False}],by_id[2504]["applicationSpells"])
        self.assertEqual([{"applicationSpellId":25122,"temporary":True}],by_id[2628]["applicationSpells"])
        self.assertEqual("combat-spell",by_id[803]["effects"][0]["type"])
        self.assertEqual("not-modeled",by_id[803]["effects"][0]["executionStatus"])
    def test_duplicate_spell_effect_links_are_deduplicated(self):
        with self.spells.open("a",encoding="utf-8") as output:
            output.write("4,22749,53,2504\n")
        self.refresh()
        spells=read(self.root,BUILD)["records"]
        entry=next(x for x in spells if x["enchantId"]==2504)
        self.assertEqual([{"applicationSpellId":22749,"temporary":False}],
                         entry["applicationSpells"])
        self.assertTrue(entry["staticComponentCandidate"])
        self.assertEqual("review-slot",entry["selectionDisposition"])

    def test_mixed_static_and_unknown_dynamic_preserves_both_without_execution(self):
        data=list(csv.DictReader(self.ench.open(encoding="utf-8",newline="")))
        data[0]["Effect_1"]="1"
        data[0]["EffectArg_1"]="91001"
        data[0]["EffectPointsMin_1"]="0"
        write_csv(self.folder,"SpellItemEnchantment",list(data[0]),data)
        self.refresh()
        report=read(self.root,BUILD)
        entry=next(x for x in report["records"] if x["enchantId"]==2504)
        self.assertTrue(entry["staticComponentCandidate"])
        self.assertEqual("review-slot-and-dynamic",entry["selectionDisposition"])
        self.assertEqual(1,report["staticPartialCandidates"])
        self.assertEqual({"spell-power":30},entry["stats"])
        self.assertEqual("not-modeled",entry["effects"][0]["executionStatus"])

    def test_optional_pinned_spell_target_metadata_never_guesses_slots(self):
        write_csv(self.folder,"Spell",
                  ["ID","EquippedItemClass","EquippedItemSubClassMask",
                   "EquippedItemInventoryTypeMask"],
                  [{"ID":22749,"EquippedItemClass":2,
                    "EquippedItemSubClassMask":0,"EquippedItemInventoryTypeMask":1}])
        self.refresh()
        report=json.loads((self.folder/"acquisition-report.json").read_text())
        b=(self.folder/"Spell.csv").read_bytes()
        report["tables"]["Spell"]={"sha256":hashlib.sha256(b).hexdigest()}
        (self.folder/"acquisition-report.json").write_text(json.dumps(report))
        result=read(self.root,BUILD)
        entry=next(x for x in result["records"] if x["enchantId"]==2504)
        self.assertIn("Spell",result["checksums"])
        self.assertEqual("2",entry["applicationSpells"][0]
                         ["rawEquipmentRestrictions"]["EquippedItemClass"])
        self.assertNotIn("allowedSlots",entry)

    def test_unpinned_optional_spell_file_is_rejected(self):
        write_csv(self.folder,"Spell",["ID"],[{"ID":22749}])
        with self.assertRaisesRegex(ValueError,"Unpinned Spell.csv"):
            read(self.root,BUILD)

    def test_unapproved_source_is_never_marked_modeled(self):
        records=read(self.root,BUILD)["records"]
        self.assertTrue(all(x["verificationStatus"]=="client-derived-candidate-not-reviewed" for x in records))
    def test_checksum_tampering_fails_closed(self):
        self.ench.write_bytes(self.ench.read_bytes()+b"\n")
        with self.assertRaisesRegex(ValueError,"Checksum mismatch"):
            read(self.root,BUILD)
    def test_build_mismatch_rejected(self):
        record=json.loads((self.folder/"acquisition-report.json").read_text())
        record["build"]="1.60.1.70685"
        (self.folder/"acquisition-report.json").write_text(json.dumps(record))
        with self.assertRaisesRegex(ValueError,"Mixed or unpinned"):
            read(self.root,BUILD)
    def test_output_stable_for_same_source(self):
        first=read(self.root,BUILD)
        self.assertEqual(json.dumps(first,sort_keys=True),json.dumps(read(self.root,BUILD),sort_keys=True))

if __name__=="__main__":
    unittest.main()
