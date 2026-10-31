#!/usr/bin/env python3
"""CP173 reproducible source-only modifier/racial/base-table census.

Read checksum-pinned SpellEffect/Spell and optional character base tables.
Do not infer combat coefficients, spell ranks, race growth or inventory masks
from unrecognized DB2 fields; this produces candidates, not executable records.
"""
import argparse
import collections
import json
from pathlib import Path

from normalize_enchants import csv_table

REQUIRED = ("SpellEffect", "Spell")
OPTIONAL = ("PlayerExpectedStat", "ChrRaces", "ChrClasses")

def read(directory, build):
    root = Path(directory) / build
    report=json.loads((root/"acquisition-report.json").read_text(encoding="utf-8"))
    if report.get("build")!=build:
        raise ValueError("Mixed or unpinned CP173 client build")
    entries={}
    checksums={}
    for table in REQUIRED:
        rows,columns=csv_table(root,table,report)
        entries[table]=(rows,columns)
        checksums[table]=report["tables"][table]["sha256"]
    for table in OPTIONAL:
        if table not in report.get("tables",{}):
            continue
        rows,columns=csv_table(root,table,report)
        entries[table]=(rows,columns)
        checksums[table]=report["tables"][table]["sha256"]

    effects,cols=entries["SpellEffect"]
    spells,spellcols=entries["Spell"]
    needed={"SpellID","Effect"}
    if not needed.issubset(cols):
        raise ValueError("SpellEffect missing required indexed spell reference or type")
    refs=collections.defaultdict(list)
    skipped=0
    aurarows=0
    for row in effects.values():
        try:
            spell=int(row.get("SpellID") or 0)
            code=int(row.get("Effect") or 0)
        except ValueError:
            skipped+=1
            continue
        if spell<=0 or code<=0:
            skipped+=1
            continue
        if code!=6:
            continue
        aurarows+=1
        # Preserve numeric raw values, but NEVER infer aura/stat semantics.
        raw={key:row.get(key) for key in (
            "EffectAura","EffectMiscValue_0","EffectMiscValue_1",
            "EffectBasePoints","EffectPointsMin","EffectPointsMax",
            "EffectTriggerSpell","EffectAmplitude"
        ) if row.get(key) not in (None,"")}
        refs[spell].append({"effectRecordId":int(row["ID"]),
                            "effectCode":code,"raw":raw,
                            "executionStatus":"not-modeled"})
    records=[]
    for spell,components in sorted(refs.items()):
        info=spells.get(spell,{})
        records.append({
            "spellId":spell,
            "name":info.get("Name_lang") or info.get("Name") or "",
            "effects":sorted(components,key=lambda e:e["effectRecordId"]),
            "sourceBuild":build,"verificationStatus":"client-derived-unreviewed",
            "executionStatus":"not-modeled"
        })
    # Direct, build-pinned character class/level data. The table contains
    # BaseMana and conversion ratios but NO race-specific Strength/Agility/
    # Stamina/Intellect/Spirit totals. Preserve ContentSetID; do not select
    # a content set by guesswork or claim complete base stats.
    base_records=[]
    base_errors=[]
    if "PlayerExpectedStat" in entries:
        rows,columns=entries["PlayerExpectedStat"]
        fields=("Level","ClassID","ContentSetID","BaseMana","HPPerStamina",
                "CritPerAgility","SpellCritPerIntellect")
        missing=[key for key in fields if key not in columns]
        if missing:
            base_errors.append("PlayerExpectedStat unrecognized columns: "+",".join(missing))
        else:
            for row in rows.values():
                try:
                    level=int(row["Level"]);class_id=int(row["ClassID"])
                    content_set=int(row["ContentSetID"])
                except (ValueError,TypeError,KeyError):
                    base_errors.append("unparseable level/class/content-set row ID "+row.get("ID",""))
                    continue
                if not 1<=level<=60 or class_id<=0:
                    continue
                base_records.append({
                    "recordId":int(row["ID"]),
                    "level":level,"classId":class_id,
                    "contentSetId":content_set,
                    "baseMana":row["BaseMana"],
                    "hpPerStamina":row["HPPerStamina"],
                    "critPerAgility":row["CritPerAgility"],
                    "spellCritPerIntellect":row["SpellCritPerIntellect"],
                    "sourceBuild":build,
                    "verificationStatus":"direct-client-fields-content-set-unresolved",
                    "hasRacePrimaryAttributes":False
                })
            base_records.sort(key=lambda x:(x["classId"],x["contentSetId"],
                                              x["level"],x["recordId"]))
    return {
        "build":build,"version":"cp173-unreviewed-spell-aura-census-v2",
        "source":"https://wago.tools/db2/SpellEffect/csv?build="+build,
        "checksums":checksums,
        "tables":{k:{"recordCount":len(rows),"columns":cols}
            for k,(rows,cols) in sorted(entries.items())},
        "discoveredSpells":len(spells),
        "auraEffectRows":aurarows,
        "candidateSpells":len(records),
        "skippedOrUnsupportedEffects":len(effects)-aurarows,
        "unparseableEffectRows":skipped,
        "executableEffects":0,
        "baseStatRecords":len(base_records),
        "baseStatContentSets":sorted(set(x["contentSetId"] for x in base_records)),
        "baseStatErrors":base_errors,
        "missingOptionalTables":[x for x in OPTIONAL if x not in entries],
        "baseProgressionCandidates":base_records,
        "candidates":records
    }

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--directory",required=True)
    parser.add_argument("--build",required=True)
    parser.add_argument("--output",required=True)
    args=parser.parse_args()
    if not args.build.startswith("1.60.1."):
        parser.error("Exact Forever 1.60.1.x client build required")
    try:
        result=read(args.directory,args.build)
    except (OSError,ValueError,KeyError,json.JSONDecodeError) as exc:
        parser.error(str(exc))
    path=Path(args.output)
    path.parent.mkdir(parents=True,exist_ok=True)
    path.write_text(json.dumps(result,indent=2,sort_keys=True,ensure_ascii=False)+"\n",
                    encoding="utf-8")
    print(json.dumps({k:result[k] for k in (
        "build","discoveredSpells","auraEffectRows","candidateSpells",
        "skippedOrUnsupportedEffects","executableEffects","baseStatRecords","baseStatContentSets","missingOptionalTables"
    )},sort_keys=True))

if __name__=="__main__":
    main()
