#!/usr/bin/env python3
"""CP172 offline, pinned Forever SpellItemEnchantment + SpellEffect normalizer.

Build-specific 1.60.1 CSVs. Any unidentified effect or application link
remains unmodeled, never guessed into bonus stats or a proc chance.
"""
import argparse
import collections
import csv
import hashlib
import json
from pathlib import Path

# CP171 shared stat identifier contract; raw ratings remain ratings.
STAT={0:"mana",1:"health",3:"agility",4:"strength",5:"intellect",6:"spirit",
      7:"stamina",16:"hit-rating",17:"hit-rating",18:"hit-rating",
      19:"critical-strike-rating",20:"critical-strike-rating",
      21:"critical-strike-rating",31:"hit-rating",32:"critical-strike-rating",
      36:"haste-rating",37:"expertise-rating",38:"attack-power",
      39:"ranged-attack-power",41:"healing-power",42:"spell-power",
      43:"mana-per-5",45:"spell-power",47:"spell-penetration",
      51:"fire-resistance",52:"frost-resistance",54:"shadow-resistance",
      55:"nature-resistance",56:"arcane-resistance"}
TYPES={0:"none",1:"combat-spell",2:"weapon-damage",3:"equip-spell",
       4:"resistance",5:"stat",6:"totem",7:"use-spell",8:"socket",
       9:"relic",11:"item-bonus"}
def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()
def csv_table(folder,table,manifest):
    path=Path(folder)/f"{table}.csv"
    if not path.is_file():raise ValueError(f"Missing {table}.csv")
    expected=manifest["tables"].get(table,{})
    if digest(path)!=expected.get("sha256"):
        raise ValueError(f"Checksum mismatch: {table}")
    with path.open(encoding="utf-8-sig",newline="") as stream:
        reader=csv.DictReader(stream)
        fields=reader.fieldnames or []
        if len(fields)!=len(set(fields)) or "ID" not in fields:
            raise ValueError(f"Malformed CSV headers: {table}")
        rows={}
        for row in reader:
            try: id=int(row["ID"])
            except (ValueError,TypeError):raise ValueError(f"Invalid ID in {table}")
            if id<=0 or id in rows:raise ValueError(f"Duplicate ID in {table}: {id}")
            rows[id]=row
    return rows,fields
def indexed(row,key,i):
    aliases=(f"{key}_{i}",f"{key}[{i}]",f"{key}.{i}")
    values=[row[x] for x in aliases if x in row]
    if len(values)!=1:raise ValueError(f"Unrecognized or ambiguous {key}[{i}] columns")
    return values[0]
def read(folder,build):
    root=Path(folder)/build
    report=json.loads((root/"acquisition-report.json").read_text(encoding="utf-8"))
    if report.get("build")!=build:raise ValueError("Mixed or unpinned client build")
    ench,cols=csv_table(root,"SpellItemEnchantment",report)
    spells,spellcols=csv_table(root,"SpellEffect",report)
    # Spell.csv optionally supplies checksum-pinned raw equipment restrictions.
    # Do not guess inventory masks into usable slots.
    spell_info={}
    if "Spell" in report.get("tables",{}):
        spell_info,_=csv_table(root,"Spell",report)
    elif (root/"Spell.csv").exists():
        raise ValueError("Unpinned Spell.csv supplied without acquisition checksum")
    needed=["Name_lang"]
    for i in range(3):
        needed+= [f"Effect_{i}",f"EffectArg_{i}",f"EffectPointsMin_{i}"]
    if any(k not in cols for k in needed):
        raise ValueError("Unsupported 1.60 enchant schema: missing "+str(set(needed)-set(cols)))
    if not {"SpellID","Effect","EffectMiscValue_0"}.issubset(spellcols):
        raise ValueError("Unsupported 1.60 spell-effect link schema")
    # Spell effect 53 = permanent enchant item; 54 = temporary enchant item.
    links=collections.defaultdict(set)
    for row in spells.values():
        if row["Effect"] not in ("53","54"):continue
        try:target=int(row["EffectMiscValue_0"]);app=int(row["SpellID"])
        except (ValueError,TypeError):continue
        if target>0 and app>0:links[target].add((app,row["Effect"]=="54"))
    def restrictions(application_spell_id):
        source=spell_info.get(application_spell_id)
        if source is None:return None
        fields=("EquippedItemClass","EquippedItemSubClassMask",
                "EquippedItemInventoryTypeMask")
        raw={k:source[k] for k in fields if k in source and source[k] not in ("",None)}
        return raw or None
    output=[]
    for id,row in sorted(ench.items()):
        bonuses={};effects=[];issues=[]
        for index in range(3):
            try:
                typ=int(indexed(row,"Effect",index))
                arg=int(indexed(row,"EffectArg",index))
                points=int(indexed(row,"EffectPointsMin",index))
            except (ValueError,TypeError) as exc:
                issues.append(f"unreadable-effect-{index}:{exc}");continue
            if typ==0:continue
            if typ==5 and arg in STAT:
                key=STAT[arg]
                bonuses[key]=bonuses.get(key,0)+points
            else:
                effects.append({"type":TYPES.get(typ,f"unknown-{typ}"),
                                "typeId":typ,"argument":arg,"points":points,
                                "executionStatus":"not-modeled"})
                if typ==5:issues.append(f"unsupported-stat-{arg}")
        apps=[{"applicationSpellId":app,"temporary":temporary}
              for app,temporary in sorted(links.get(id,set()))]
        for app in apps:
            raw=restrictions(app["applicationSpellId"])
            if raw is not None:app["rawEquipmentRestrictions"]=raw
        # A supported static part can be reviewed even when another effect
        # remains unknown. Runtime execution still needs explicit approval.
        static_usable=bool(bonuses) and not issues and bool(apps)
        disposition=("review-slot-and-dynamic" if effects else "review-slot") if static_usable else "not-selectable"
        output.append({"enchantId":id,"name":row.get("Name_lang",""),
                       "applicationSpells":apps,
                       "stats":bonuses,"effects":effects,"unresolved":issues,
                       "staticComponentCandidate":static_usable,
                       "selectionDisposition":disposition,"sourceBuild":build,
                       "verificationStatus":"client-derived-candidate-not-reviewed"})
    checksums={k:report["tables"][k]["sha256"]
               for k in ("SpellItemEnchantment","SpellEffect")}
    if spell_info:checksums["Spell"]=report["tables"]["Spell"]["sha256"]
    return {"build":build,"version":"cp172-source-enchants-v2",
            "source":"https://wago.tools/db2/SpellItemEnchantment/csv?build="+build,
            "checksums":checksums,
            "discovered":len(output),
            "staticCandidates":sum(bool(x["stats"]) and not x["effects"] and not x["unresolved"] for x in output),
            "withUnmodeledEffects":sum(bool(x["effects"]) for x in output),
            "staticPartialCandidates":sum(x["staticComponentCandidate"] and bool(x["effects"]) for x in output),
            "staticLinkedCandidates":sum(x["staticComponentCandidate"] for x in output),
            "notSelectable":sum(x["selectionDisposition"]=="not-selectable" for x in output),
            "unlinked":sum(not x["applicationSpells"] for x in output),
            "records":output}
def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument("--directory",required=True)
    p.add_argument("--build",required=True)
    p.add_argument("--output",required=True)
    args=p.parse_args()
    if not args.build.startswith("1.60.1."):
        p.error("Exact 1.60.1.x Forever build required")
    try: result=read(args.directory,args.build)
    except (OSError,ValueError,KeyError,json.JSONDecodeError) as ex:
        p.error(str(ex))
    output=Path(args.output);output.parent.mkdir(parents=True,exist_ok=True)
    output.write_text(json.dumps(result,indent=2,sort_keys=True,ensure_ascii=False)+"\n",
                      encoding="utf-8")
    print(json.dumps({k:result[k] for k in ("build","discovered","staticCandidates",
          "withUnmodeledEffects","unlinked")},sort_keys=True))
if __name__=="__main__":main()
