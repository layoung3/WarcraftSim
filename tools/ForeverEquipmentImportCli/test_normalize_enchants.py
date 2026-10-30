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
