#!/usr/bin/env python3
"""CP171 pinned-build DB2 join and reviewed combat-value bulk normalizer.

This intentionally does NOT reconstruct server/DB2 item-budget formulas.
ItemSparse 1.60.1.70707 contains stat IDs and percentage editors, but not
final numeric stat amounts or item armor and min/max damage. Numeric combat
values therefore require an explicitly attested and checksum-verified
resolved-values export, and cannot be inferred from Classic coefficients.
"""
import argparse
import collections
import csv
import datetime as dt
import hashlib
import json
from pathlib import Path
import re
import sys
import forever_scaling

BUILD = re.compile(r"^1\.60\.1\.\d+$")
SLOTS = {
    1: ["head"], 2: ["neck"], 3: ["shoulder"], 5: ["chest"],
    6: ["waist"], 7: ["legs"], 8: ["feet"], 9: ["wrist"], 10: ["hands"],
    11: ["finger1", "finger2"], 12: ["trinket1", "trinket2"],
    13: ["main-hand", "off-hand"], 14: ["off-hand"], 15: ["ranged"],
    16: ["back"], 17: ["main-hand"], 20: ["chest"], 21: ["main-hand"],
    22: ["off-hand"], 23: ["off-hand"], 25: ["ranged"], 26: ["ranged"]
}
CLASSES = {
    1: "warrior", 2: "paladin", 3: "hunter", 4: "rogue", 5: "priest",
    7: "shaman", 8: "mage", 9: "warlock", 11: "druid"
}
QUALITY = {0:"poor",1:"common",2:"uncommon",3:"rare",4:"epic",5:"legendary",6:"artifact",7:"heirloom"}
ARMOR_PROFICIENCY = {
    1: set(CLASSES.values()),
    2: {"warrior","paladin","hunter","rogue","shaman","druid"},
    3: {"warrior","paladin","hunter","shaman"},
    4: {"warrior","paladin"},
    6: {"warrior","paladin","shaman"},
}
ARMOR_NAMES = {0:"miscellaneous",1:"cloth",2:"leather",3:"mail",
               4:"plate",5:"cosmetic",6:"shield"}

STAT_KEYS = {
    "strength", "agility", "stamina", "intellect", "spirit", "armor",
    "attack-power", "melee-attack-power", "ranged-attack-power",
    "spell-power", "healing-power", "hit-rating", "critical-strike-rating",
    "haste-rating", "expertise-rating", "defense", "defense-rating",
    "hit-chance-percent", "attack-critical-chance-percent",
    "spell-critical-chance-percent", "spell-hit-chance-percent",
    "expertise-percent", "mana", "health", "mana-per-5",
    "dodge-rating", "parry-rating", "resilience-rating", "mastery-rating",
    "spell-penetration", "bonus-armor", "fire-resistance",
    "frost-resistance", "nature-resistance", "shadow-resistance",
    "arcane-resistance"
}
BUILD_SCHEMA = "wowdev/WoWDBDefs ItemSparse+Item+ItemXItemEffect+ItemEffect build 1.60.1.70707"
REQUIRED = {
    "Item": {"ID", "ClassID", "SubclassID", "InventoryType"},
    "ItemSparse": {"ID", "Display_lang", "InventoryType", "RequiredLevel",
                   "OverallQualityID", "AllowableClass", "ItemLevel"},
}
OPTIONAL = {
    "ItemXItemEffect": {"ItemID","ItemEffectID"},
    "ItemEffect": {"ID","SpellID","TriggerType"},
    "ItemSet": {"ID","Name_lang"},
    "ItemSetSpell": {"ID","ItemSetID","SpellID","Threshold"},
    "SpellEffect": {"ID"},
    "SpellItemEnchantment": {"ID"}
}
def sha256(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()

def field(row, base, idx):
    """Only exact, explicitly known array conventions; never guess a stat offset."""
    options = (f"{base}[{idx}]", f"{base}_{idx}", f"{base}.{idx}")
    found = [x for x in options if x in row]
    if len(found) != 1:
        if len(found) > 1:
            raise ValueError(f"Ambiguous column for {base}[{idx}]: {found}")
        return None
    return row[found[0]]

def number(s, what, default=None):
    if s is None or str(s).strip() == "":
        if default is not None:
            return default
        raise ValueError(f"Missing numeric {what}")
    try:
        return int(s)
    except ValueError as exc:
        raise ValueError(f"Invalid integer {what}={s}") from exc

def load_csv(directory, table, required, expected_hashes, strict):
    p = Path(directory) / f"{table}.csv"
    if not p.exists():
        if strict:
            raise ValueError(f"Missing required table {table}.csv")
        return {}, [], None
    digest = sha256(p)
    if expected_hashes is not None:
        record = expected_hashes.get(table)
        if not record or record.get("sha256") != digest:
            raise ValueError(f"Checksum mismatch or unpinned file: {table}")
    with p.open(encoding="utf-8-sig", newline="") as fh:
        reader = csv.DictReader(fh)
        columns = reader.fieldnames or []
        if len(set(columns)) != len(columns):
            raise ValueError(f"Duplicate CSV headers in {table}")
        missing = sorted(required - set(columns))
        if missing:
            raise ValueError(f"Build-specific schema mismatch: {table} missing {missing}")
        rows = {}
        for row in reader:
            key = number(row.get("ID"), f"{table}.ID")
            if key <= 0 or key in rows:
                raise ValueError(f"Duplicate/invalid {table}.ID={key}")
            rows[key] = row
    return rows, columns, digest

def parsed_time(s):
    d = dt.datetime.fromisoformat(s.replace("Z","+00:00"))
    if d.tzinfo is None:
        raise ValueError("Timestamp must include timezone")
    return d

def restrict_classes(mask):
    if mask in (-1,0,4294967295,65535):
        return []
    if mask < 0:
        raise ValueError("Unsupported class mask")
    supported = sum(1 << (n-1) for n in CLASSES)
    if mask & ~supported:
        raise ValueError(f"Unexpected class bits 0x{mask & ~supported:x}")
    return [name for n,name in CLASSES.items() if mask & (1 << (n-1))]

def load_review(approval_path, values_path, build, source_hashes):
    """A separate approval binds content digest, exact build and source URL."""
    if not values_path and not approval_path:
        return {}, {}
    if not values_path or not approval_path:
        raise ValueError("Resolved values require both --resolved-values and --approval")
    approval = json.loads(Path(approval_path).read_text(encoding="utf-8"))
    digest = sha256(values_path)
    if (approval.get("clientBuild") != build or
        approval.get("resolvedValuesSha256") != digest or
        not str(approval.get("reviewer","")).strip() or
        not str(approval.get("sourceUrl","")).startswith("https://") or
        approval.get("status") != "reviewed"):
        raise ValueError("Resolved source is not explicitly reviewed for this build and checksum")
    pinned=approval.get("sourceTableSha256")
    if not isinstance(pinned,dict) or any(
        pinned.get(t) != source_hashes.get(t) for t in ("Item","ItemSparse")):
        raise ValueError("Review approval not bound to exact Item and ItemSparse CSV checksums")
    raw = json.loads(Path(values_path).read_text(encoding="utf-8"))
    if not isinstance(raw, list):
        raise ValueError("Resolved values file must be array")
    rows={}
    for item in raw:
        ident=number(item.get("id"), "resolved.id")
        if ident in rows:
            raise ValueError(f"Duplicate resolved item {ident}")
        if (item.get("clientBuild") != build or
            item.get("verificationStatus") != "client-db2-reviewed" or
            item.get("status") != "complete" or
            not str(item.get("sourceUrl","")).startswith("https://")):
            raise ValueError(f"Unreviewed or mixed-build resolved item {ident}")
        rows[ident]=item
    return rows, approval

def apply_hotfix(values, overlay_path, build, approved_values_path):
    if not overlay_path:
        return 0
    if not approved_values_path:
        raise ValueError("Hotfixes require reviewed base resolved values")
    overlay=json.loads(Path(overlay_path).read_text(encoding="utf-8"))
    if (overlay.get("clientBuild") != build or
        overlay.get("baseSha256") != sha256(approved_values_path) or
        not str(overlay.get("sourceUrl","")).startswith("https://") or
        overlay.get("status") != "reviewed" or
        not overlay.get("hotfixVersion")):
        raise ValueError("Hotfix mismatched build, source checksum or review status")
    seen=set()
    for change in overlay.get("changes",[]):
        ident=number(change.get("id"),"hotfix item")
        if ident in seen or ident not in values:
            raise ValueError(f"Duplicate or unknown hotfix id {ident}")
        seen.add(ident)
        allowed={"stats","weapon"}
        if not set(change.get("replace",{})).issubset(allowed) or not change.get("replace"):
            raise ValueError("Hotfix may replace reviewed stats/weapon only")
        if not change.get("reason") or not str(change.get("sourceUrl","")).startswith("https://"):
            raise ValueError("Unattributed hotfix change")
        values[ident]={**values[ident],**change["replace"],
                       "sourceUrl": change["sourceUrl"],
                       "hotfixVersion": overlay["hotfixVersion"]}
    return len(seen)

def normalize(args):
    build = args.build
    if not BUILD.fullmatch(build):
        raise ValueError("A pinned Forever build 1.60.1.x is required")
    parsed_time(args.imported_at)
    folder = Path(args.directory) / build
    provenance = folder / "acquisition-report.json"
    expected = None
    if provenance.is_file():
        report=json.loads(provenance.read_text(encoding="utf-8"))
        if report.get("build") != build:
            raise ValueError("Acquisition manifest build mismatch")
        expected=report.get("tables",{})
    else:
        if not args.fixture:
            raise ValueError("Missing acquisition-report.json with per-table checksums. --fixture is test-only.")
    tables={}; schemas={}; hashes={}
    for table,fields in {**REQUIRED,**OPTIONAL}.items():
        rows, columns, digest=load_csv(folder,table,fields,expected,table in REQUIRED)
        tables[table]=rows
        if digest:
            schemas[table]=columns
            hashes[table]=digest
    # Derived scaling tables are joined only in the diagnostic report. Their
    # quality/level indexes cannot be equated to final armor/damage without
    # a build-tested formula, so NEVER turn them into item stats here.
    for table in ("RandPropPoints","ItemDamageOneHand","ItemDamageTwoHand","ItemDamageRanged",
                  "ItemDamageOneHandCaster","ItemDamageTwoHandCaster","ItemDamageThrown","ItemDamageWand",
                  "ItemArmorTotal","ItemArmorQuality","ItemArmorShield","ArmorLocation"):
        rows, cols, digest=load_csv(folder,table,{"ID"},expected,False)
        tables[table]=rows
        if digest:
            schemas[table]=cols
            hashes[table]=digest
    if not tables["Item"] or not tables["ItemSparse"]:
        raise ValueError("Item + ItemSparse both required")
    reviewed, approval = load_review(args.approval,args.resolved_values,build,hashes)
    changes=apply_hotfix(reviewed,args.hotfix,build,args.resolved_values)
    baseline=set()
    if args.baseline:
        old=json.loads(Path(args.baseline).read_text(encoding="utf-8"))
        baseline={x["id"] for x in old.get("items",[])}
    by_item=collections.defaultdict(list)
    for relation in tables["ItemXItemEffect"].values():
        item_id=number(relation.get("ItemID"),"relation.ItemID")
        effect_id=number(relation.get("ItemEffectID"),"relation.ItemEffectID")
        if effect_id not in tables["ItemEffect"]:
            by_item[item_id].append({"kind":"unsupported","spellId":None,"executionStatus":"not-modeled",
                                      "notes":f"Unresolved ItemEffectID {effect_id}"})
            continue
        effect=tables["ItemEffect"][effect_id]
        spell_id=number(effect.get("SpellID"),"effect.SpellID",0)
        # TriggerType 0/5 use, 1 on-equip, 2 chance-on-hit. Keep
        # all effects nonexecuting until their mechanics are certified.
        trigger=number(effect.get("TriggerType"),"ItemEffect.TriggerType",-1)
        kind={0:"on-use",1:"equip",2:"proc",5:"on-use"}.get(trigger,"unsupported")
        if spell_id<=0:kind="unsupported"
        by_item[item_id].append({
            "kind":kind,"spellId":spell_id if spell_id>0 else None,
            "executionStatus":"not-modeled",
            "notes":f"ItemEffectID {effect_id}; TriggerType {trigger}; mechanics not executed"
        })
    item_sets={}; set_bonus={}; set_conflicts=collections.defaultdict(set)
    for set_id, setrow in tables["ItemSet"].items():
        name=setrow.get("Name_lang","").strip() or f"Client set {set_id}"
        item_sets[set_id]=name
        for i in range(17):
            val=field(setrow,"ItemID",i)
            if val and val.strip() and val.strip()!="0":
                member=number(val,"ItemSet.ItemID")
                if member in set_bonus and set_bonus[member] != set_id:
                    set_conflicts[member].update((set_bonus[member],set_id))
                else:
                    set_bonus[member]=set_id
    # A set threshold can legitimately link multiple spell rows (class/spec
    # variants). Preserve all IDs as unmodeled notes, without duplicate
    # executable bonuses or fake stat assignments.
    set_links=collections.defaultdict(set)
    for spell in tables["ItemSetSpell"].values():
        sid=number(spell.get("ItemSetID"),"ItemSetSpell.ItemSetID",0)
        if sid not in item_sets:
            continue
        threshold=number(spell.get("Threshold"),"ItemSetSpell.Threshold",0)
        link=number(spell.get("SpellID"),"ItemSetSpell.SpellID",0)
        if threshold < 1 or link <= 0:
            continue
        set_links[(sid,threshold)].add(link)
    set_defs=[]
    for (sid,threshold),links in sorted(set_links.items()):
        set_defs.append({"key":f"client-set-{sid}","name":item_sets[sid],
                         "requiredPieces":threshold,"modeled":False,
                         "notes":f"Linked SpellIDs {','.join(map(str,sorted(links)))}; not executed without mechanic review",
                         "stats":{},"sourceUrl":args.source_url})
    # The importer requires a set definition for any SetKey even when no
    # client threshold is visible.
    by_set={x["key"] for x in set_defs}
    for sid,name in item_sets.items():
        if f"client-set-{sid}" not in by_set and sid in set_bonus.values():
            set_defs.append({"key":f"client-set-{sid}","name":name,
                             "requiredPieces":9,"modeled":False,"stats":{},
                             "notes":"Set threshold unavailable; placeholder disabled",
                             "sourceUrl":args.source_url})
    candidate=[]; completed=[]; overrides=[]; rejected=[]
    formula_preview=[]
    scaling_diagnostics=[]
    # Index table budget rows using a reviewed item-level value. These do
    # not represent final item armor or weapon damage without a tested formula.
    scaling_by_level={}
    for table in ("ItemDamageOneHand","ItemDamageTwoHand","ItemDamageRanged",
                  "ItemArmorTotal","ItemArmorQuality","ItemArmorShield","ArmorLocation"):
        group=collections.defaultdict(list)
        for scale_row in tables[table].values():
            level=number(scale_row.get("ItemLevel"),f"{table}.ItemLevel",0)
            if level > 0:
                group[level].append(scale_row)
        scaling_by_level[table]=group
    # DB2 records have their own ID and a separate ItemLevel join key.
    # Never assume record ID equals item level for armor/weapon quality rows.
    formula_tables=dict(tables)
    for table in ("ItemDamageOneHand","ItemDamageTwoHand","ItemDamageRanged",
                  "ItemDamageOneHandCaster","ItemDamageTwoHandCaster",
                  "ItemDamageThrown","ItemDamageWand","ItemArmorTotal",
                  "ItemArmorQuality","ItemArmorShield"):
        lookup={}
        for record in tables[table].values():
            ilvl=number(record.get("ID") if table=="ItemArmorQuality"
                        else record.get("ItemLevel"),f"{table}.ItemLevel",0)
            if ilvl<=0: continue
            if ilvl in lookup:
                raise ValueError(f"Ambiguous {table} item level {ilvl}")
            lookup[ilvl]=record
        formula_tables[table]=lookup
    for ident, row in sorted(tables["ItemSparse"].items()):
        item=tables["Item"].get(ident)
        if item is None:
            rejected.append({"id":ident,"reason":"missing Item join"})
            continue
        name=(row.get("Display_lang") or "").strip()
        if not name:
            rejected.append({"id":ident,"reason":"missing localized name"})
            continue
        inventory=number(row.get("InventoryType"),f"item {ident}.InventoryType",0)
        if not inventory:
            inventory=number(item.get("InventoryType"),f"Item {ident}.InventoryType",0)
        raw_inventory=number(item.get("InventoryType"),f"Item {ident}.InventoryType",0)
        class_id=number(item.get("ClassID"),f"Item {ident}.ClassID",0)
        # Record item IDs from all classes as discoverable metadata; never
        # promote consumables, non-equipment inventory or mismatched item types.
        slots=SLOTS.get(inventory)
        diagnostics=[]
        if ident in set_conflicts:
            diagnostics.append("conflicting-set-membership:"+
                               ",".join(map(str,sorted(set_conflicts[ident]))))
        unique_flag_missing="Flags_0" not in row or not str(row.get("Flags_0","")).strip()
        unique_equipped=bool(number(row.get("Flags_0"),f"item {ident}.Flags_0",0)&0x00080000)
        if unique_flag_missing:
            diagnostics.append("missing-unique-equipped-flag")
        sparse_set=number(row.get("ItemSet"),f"item {ident}.ItemSet",0)
        if sparse_set and ident in set_bonus and sparse_set != set_bonus[ident]:
            diagnostics.append(f"conflicting-sparse-set:{sparse_set}-vs-{set_bonus[ident]}")
        if raw_inventory and inventory and raw_inventory != inventory:
            diagnostics.append(f"inventory-conflict:{inventory}-vs-{raw_inventory}")
        if not slots:
            diagnostics.append(f"unsupported-inventory-type:{inventory}")
        mask=number(row.get("AllowableClass"),f"item {ident}.AllowableClass",-1)
        try:
            allowed=restrict_classes(mask)
            if class_id==4 and number(item.get("SubclassID"),"Item.SubclassID",0) in ARMOR_PROFICIENCY:
                subclass_id=number(item.get("SubclassID"),"Item.SubclassID",0)
                eligible=ARMOR_PROFICIENCY[subclass_id]
                source_allowlist=set(allowed) if allowed else set(CLASSES.values())
                allowed=[name for name in CLASSES.values() if name in source_allowlist and name in eligible]
                if not allowed:
                    diagnostics.append("no-proficient-class-after-source-mask")
        except ValueError as ex:
            allowed=[];diagnostics.append(str(ex))
        quality_id=number(row.get("OverallQualityID"),f"item {ident}.OverallQualityID",-1)
        if quality_id not in QUALITY:
            diagnostics.append(f"unknown-quality:{quality_id}")
        sparse_code=[number(field(row,"StatModifier_bonusStat",i),
                            f"item {ident}.statCode[{i}]",0)
                     for i in range(10)]
        if any(v != 0 for v in sparse_code):
            diagnostics.append("raw-stat-ids-have-no-final-amounts")
        if class_id not in (2,4):
            diagnostics.append(f"non-equipment-item-class:{class_id}")
        if not by_item.get(ident) and not tables["ItemXItemEffect"]:
            diagnostics.append("ItemXItemEffect-unavailable")
        if getattr(args,"formula_preview",None) and slots and class_id in (2,4):
            try:
                trial=forever_scaling.candidate(row,item,formula_tables)
            except (ValueError,KeyError,ArithmeticError) as exc:
                trial={"stats":{},"weapon":None,
                       "unresolved":[f"candidate-source-incompatible:{exc}"]}
            formula_preview.append({"id":ident,"itemLevel":number(row.get("ItemLevel"),"ItemLevel",0),
                                    "quality":quality_id,"slot":inventory,
                                    "classID":class_id,"requiredLevel":number(row.get("RequiredLevel"),"RequiredLevel",0),
                                    "uniqueEquipped":unique_equipped,
                                    "sourceBuild":build,"candidate":trial})
        entry={"id":ident,"name":name,"sourceUrl":args.source_url,
               "sourceBuild":build,"verificationStatus":"metadata-only",
               "requiredLevel":number(row.get("RequiredLevel"),f"item {ident}.RequiredLevel",0),
               "quality":QUALITY.get(quality_id,""),
               "inventoryType":inventory,"allowedSlots":slots or [],
               "allowedClasses":allowed,
               "itemClass":"weapon" if class_id==2 else "armor" if class_id==4 else "",
               "itemSubclass":(ARMOR_NAMES.get(number(item.get("SubclassID"),"Item.SubclassID",0),str(item.get("SubclassID",""))) if class_id==4 else forever_scaling.WEAPON_SUBCLASSES.get(number(item.get("SubclassID"),"Item.SubclassID",0),str(item.get("SubclassID","")))),
               "itemSetId":number(row.get("ItemSet"),f"item {ident}.ItemSet",0),
               "uniqueEquipped":unique_equipped,
               "rawStatIdentifiers":sparse_code,
               "linkedSpellIds":sorted({e["spellId"] for e in by_item[ident] if e["spellId"] is not None})}
        resolved=reviewed.get(ident)
        if not resolved or diagnostics and any(x.startswith(("unknown-quality","unsupported-inventory",
                 "inventory-conflict","conflicting-set-membership","conflicting-sparse-set","missing-unique-equipped-flag","non-equipment-item-class","no-proficient-class-after-source-mask","Unexpected class")) for x in diagnostics):
            entry["notes"]="; ".join(diagnostics)
            candidate.append(entry)
            continue
        if not slots:
            candidate.append(entry);continue
        level=number(row.get("ItemLevel"),f"item {ident}.ItemLevel",0)
        if resolved.get("itemLevel") is not None and resolved["itemLevel"] != level:
            rejected.append({"id":ident,"reason":"reviewed item level conflicts with source ItemSparse"})
            continue
        if level > 0:
            scaling_table = ("ItemArmorTotal" if class_id==4 else
                             "ItemDamageRanged" if inventory in (15,25,26) else
                             "ItemDamageTwoHand" if inventory==17 else
                             "ItemDamageOneHand")
            matches = scaling_by_level[scaling_table].get(level,[])
            scaling_diagnostics.append({
                "id":ident,"itemLevel":level,"table":scaling_table,
                "matchingRows":len(matches),
                "status":"budget-row-available-not-formula-verified" if len(matches)==1 else
                         "ambiguous-budget-level" if len(matches)>1 else "missing-budget-row"})
            if len(matches)>1:
                rejected.append({"id":ident,"reason":"ambiguous level scaling row"})
                continue
        # Only reviewed numeric source values may become combat stats.
        stats=resolved.get("stats")
        if not isinstance(stats,dict) or not stats:
            candidate.append(entry);continue
        if any(k not in STAT_KEYS or not isinstance(v,(int,float)) or v<0 for k,v in stats.items()):
            rejected.append({"id":ident,"reason":"unmodeled or invalid combat stat key/value"})
            continue
        armor=resolved.get("armor")
        if armor is not None:
            if not isinstance(armor,(int,float)) or armor<0 or "armor" in stats:
                rejected.append({"id":ident,"reason":"invalid or duplicate resolved armor"})
                continue
            stats={**stats,"armor":armor}
        weapon=resolved.get("weapon")
        if "main-hand" in slots or "ranged" in slots:
            if not weapon:
                candidate.append(entry);continue
        if weapon:
            # ItemDelay is a directly encoded weapon delay in milliseconds.
            # Never calculate damage here, but verify or fill attack interval.
            raw_delay=number(row.get("ItemDelay"),f"item {ident}.ItemDelay",0)
            if raw_delay > 0:
                observed_speed=raw_delay/1000
                supplied_speed=weapon.get("speedSeconds")
                if supplied_speed is not None and abs(float(supplied_speed)-observed_speed)>0.000001:
                    rejected.append({"id":ident,"reason":"reviewed speed conflicts with client ItemDelay"})
                    continue
                weapon={**weapon,"speedSeconds":observed_speed}
            try:
                if (weapon["hand"] not in ("one-hand","two-hand","ranged") or
                    float(weapon["minimumDamage"])<0 or
                    float(weapon["maximumDamage"])<float(weapon["minimumDamage"]) or
                    float(weapon["speedSeconds"])<=0):
                    raise ValueError("bad weapon")
            except (KeyError,TypeError,ValueError):
                rejected.append({"id":ident,"reason":"invalid reviewed weapon values"})
                continue
        if resolved.get("classID") is not None and resolved["classID"] != class_id:
            rejected.append({"id":ident,"reason":"class mismatch with Item table"})
            continue
        if resolved.get("inventoryType") is not None and resolved["inventoryType"] != inventory:
            rejected.append({"id":ident,"reason":"inventory mismatch with ItemSparse"})
            continue
        x={"id":ident,"name":name,"requiredLevel":number(row.get("RequiredLevel"),"RequiredLevel",0),
           "allowedSlots":slots,"allowedClasses":allowed,
           "uniqueEquipped":unique_equipped,
           "quality":QUALITY[quality_id], "itemClass":"weapon" if class_id==2 else "armor",
           "itemSubclass":str(item.get("SubclassID","")),
           "stats":dict(sorted(stats.items())), "sourceUrl":resolved["sourceUrl"],
           "sourceBuild":build,"sourceStatus":"forever-client-db2-reviewed",
           "verificationStatus":"client-db2-reviewed","snapshotDate":args.imported_at[:10],
           "notes":["Review-attested resolved combat values, not assumed from ItemSparse stat IDs",
                    f"Input ItemSparse SHA256 {hashes['ItemSparse']}",
                    f"ItemSparse stat identifiers: {sparse_code}"],
           "effects":sorted(by_item[ident],key=lambda a:(a.get("spellId") or 0,a["notes"]))}
        if weapon:
            x["weapon"]=weapon
        if ident in set_bonus:
            x["setKey"]=f"client-set-{set_bonus[ident]}"
        completed.append(x)
        if ident in baseline:
            overrides.append({"itemId":ident,"reason":"Reviewed bulk replacement; see attested resolved values and pinned DB2 hashes",
                              "sourceUrl":resolved["sourceUrl"]})
    uncovered=sorted(set(reviewed)-set(tables["ItemSparse"]))
    rejected.extend({"id":id,"reason":"resolved ID absent from ItemSparse"} for id in uncovered)
    completed.sort(key=lambda x:x["id"])
    candidate.sort(key=lambda x:x["id"])
    set_defs.sort(key=lambda x:(x["key"],x["requiredPieces"]))
    # Duplicate set thresholds indicate incompatible source rows and must fail.
    set_keys=[(x["key"],x["requiredPieces"]) for x in set_defs]
    if len(set_keys)!=len(set(set_keys)):
        raise ValueError("Conflicting ItemSetSpell threshold records")
    manifest={"catalogVersion":args.catalog_version,
              "contentVersion":f"client-join-{build}",
              "clientBuild":build,"sourceName":"reviewed-pinned-forever-db2-joined",
              "sourceUrl":args.source_url,"importedAt":args.imported_at,
              "items":completed,"metadataOnly":candidate,"overrides":overrides,"sets":set_defs}
    report={"requestedBuild":build,"actualBuild":"unverified-runtime-source",
            "schemaReference":BUILD_SCHEMA,"tableHeaders":schemas,
            "scalingJoins":scaling_diagnostics,
            "conflictingSetMemberships":{str(k):sorted(v) for k,v in sorted(set_conflicts.items())},
            "tableSha256":hashes,"discoveredIds":len(tables["ItemSparse"]),
            "joinedIds":len(tables["ItemSparse"])-sum(x["reason"]=="missing Item join" for x in rejected),
            "approvedCombatRecords":len(completed),"metadataOnly":len(candidate),
            "equippableCandidates":sum(1 for x in candidate if x["allowedSlots"] and x["itemClass"] in ("weapon","armor"))+len(completed),
            "rejectedCount":len(rejected),
            "rejected":rejected,"unresolvedEffectReferences":sum(len(by_item.get(x,[])) for x in tables["ItemSparse"]),
            "itemsWithUnresolvedEffects":sum(bool(by_item.get(x)) for x in tables["ItemSparse"]),
            "reviewedValuesSha256":sha256(args.resolved_values) if args.resolved_values else None,
            "reviewApproval":approval,"hotfixApplied":changes,
            "scalingState":"NOT CALCULATED: source requires verified 1.60 armor/weapon/stat-budget formulas",
            "missingOptionalTables":sorted(x for x in OPTIONAL if x not in hashes),
            "missingScalingTables":sorted(x for x in
                ("ItemDamageOneHand","ItemDamageTwoHand","ItemDamageRanged",
                 "ItemArmorTotal","ItemArmorQuality","ItemArmorShield","ArmorLocation")
                 if x not in hashes)}
    if getattr(args,"formula_preview",None):
        preview_path=Path(args.formula_preview)
        preview_path.parent.mkdir(parents=True,exist_ok=True)
        preview_path.write_text(json.dumps({
            "clientBuild":build,"verificationStatus":"candidate-unverified-not-for-combat",
            "inputTableSha256":hashes,"records":formula_preview
        },sort_keys=True,indent=2,ensure_ascii=False)+"\n",encoding="utf-8")
    out=Path(args.output)
    out.parent.mkdir(parents=True,exist_ok=True)
    out.write_text(json.dumps(manifest,sort_keys=True,indent=2,ensure_ascii=False)+"\n",encoding="utf-8")
    Path(args.report).parent.mkdir(parents=True,exist_ok=True)
    Path(args.report).write_text(json.dumps(report,sort_keys=True,indent=2,ensure_ascii=False)+"\n",encoding="utf-8")
    print(f"Discovered {report['discoveredIds']} ItemSparse rows; {len(completed)} attested combat records; "
          f"{len(candidate)} metadata-only; {len(rejected)} rejected; {changes} hotfix records.")
    return 0

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--build",required=True)
    parser.add_argument("--directory",required=True)
    parser.add_argument("--source-url",required=True)
    parser.add_argument("--imported-at",required=True)
    parser.add_argument("--catalog-version",required=True)
    parser.add_argument("--baseline")
    parser.add_argument("--resolved-values")
    parser.add_argument("--approval")
    parser.add_argument("--hotfix")
    parser.add_argument("--formula-preview",help="Write non-executable candidate stat/armor/damage values; never automatically promote")
    parser.add_argument("--fixture",action="store_true",help="ONLY synthetic CI/local test source; do not use as live proof")
    parser.add_argument("--output",required=True)
    parser.add_argument("--report",required=True)
    args=parser.parse_args()
    try:
        if not args.source_url.startswith("https://"):
            raise ValueError("HTTPS source attribution required")
        return normalize(args)
    except (OSError,ValueError,KeyError,TypeError,json.JSONDecodeError) as exc:
        print(f"Source or schema rejected: {exc}",file=sys.stderr)
        return 2

if __name__=="__main__":
    raise SystemExit(main())
