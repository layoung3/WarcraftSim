import csv
import hashlib
import json
import tempfile
import unittest
from pathlib import Path

from normalize_modifiers import read

BUILD="1.60.1.70707"

class Cp173ModifierSourceCensus(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root=Path(self.tmp.name)
        self.folder=self.root/BUILD
        self.folder.mkdir(parents=True)
        self.write("Spell",[{"ID":10157,"Name_lang":"Arcane Intellect"},
                            {"ID":77777,"Name_lang":"Unknown Aura"}])
        self.write("SpellEffect",[
            {"ID":1,"SpellID":10157,"Effect":6,"EffectAura":29,"EffectMiscValue_0":3,"EffectBasePoints":31},
            {"ID":2,"SpellID":10157,"Effect":6,"EffectAura":42,"EffectMiscValue_0":0,"EffectBasePoints":1},
            {"ID":3,"SpellID":77777,"Effect":2,"EffectAura":0,"EffectMiscValue_0":0,"EffectBasePoints":99}])
        self.refresh()

    def write(self,table,rows):
        with (self.folder/(table+".csv")).open("w",encoding="utf-8",newline="") as f:
            writer=csv.DictWriter(f,fieldnames=list(rows[0]))
            writer.writeheader()
            writer.writerows(rows)

    def refresh(self):
        tables={}
        for table in ("Spell","SpellEffect"):
            b=(self.folder/(table+".csv")).read_bytes()
            tables[table]={"sha256":hashlib.sha256(b).hexdigest()}
        (self.folder/"acquisition-report.json").write_text(
            json.dumps({"build":BUILD,"tables":tables}),encoding="utf-8")

    def test_pinned_raw_aura_references_never_become_executable(self):
        result=read(self.root,BUILD)
        self.assertEqual(2,result["discoveredSpells"])
        self.assertEqual(2,result["auraEffectRows"])
        self.assertEqual(1,result["candidateSpells"])
        self.assertEqual(0,result["executableEffects"])
        candidate=result["candidates"][0]
        self.assertEqual(10157,candidate["spellId"])
        self.assertEqual("31",candidate["effects"][0]["raw"]["EffectBasePoints"])
        self.assertEqual("not-modeled",candidate["executionStatus"])
        self.assertEqual(sorted(["PlayerExpectedStat","ChrRaces","ChrClasses"]),
                         sorted(result["missingOptionalTables"]))

    def test_tamper_or_mixed_build_fails_closed(self):
        self.folder.joinpath("Spell.csv").write_bytes(
            self.folder.joinpath("Spell.csv").read_bytes()+b"\n")
        with self.assertRaisesRegex(ValueError,"Checksum mismatch"):
            read(self.root,BUILD)
        self.refresh()
        report=json.loads((self.folder/"acquisition-report.json").read_text())
        report["build"]="1.60.1.70685"
        (self.folder/"acquisition-report.json").write_text(json.dumps(report))
        with self.assertRaisesRegex(ValueError,"Mixed or unpinned"):
            read(self.root,BUILD)

    def test_deterministic_order_and_repeated_reads(self):
        a=read(self.root,BUILD)
        b=read(self.root,BUILD)
        self.assertEqual(json.dumps(a,sort_keys=True),json.dumps(b,sort_keys=True))

if __name__=="__main__":
    unittest.main()
