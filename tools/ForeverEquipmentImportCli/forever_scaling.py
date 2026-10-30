"""Candidate Forever 1.60 equipment math from build-pinned client DB2 tables.

WoWSims Cataclysm GetScaledStat/GetArmorValue/WeaponDmgMin/WeaponDmgMax
is an architectural reference. These are CANDIDATES ONLY: never promote
until an exact-build independent tooltip validation gate passes.
"""
from decimal import Decimal, ROUND_HALF_UP, ROUND_FLOOR

STAT_IDS = {
    3:"agility",4:"strength",5:"intellect",6:"spirit",7:"stamina",
    31:"hit-rating",32:"critical-strike-rating",36:"haste-rating",
    37:"expertise-rating",38:"attack-power",39:"ranged-attack-power",
    41:"healing-power",42:"spell-power",43:"mana-per-5",45:"spell-power",
    47:"spell-penetration",50:"bonus-armor",51:"fire-resistance",
    52:"frost-resistance",54:"shadow-resistance",55:"nature-resistance",
    56:"arcane-resistance"
}
WEAPON_SUBCLASSES = {
    0:"axe",1:"axe",2:"bow",3:"gun",4:"mace",5:"mace",
    6:"polearm",7:"sword",8:"sword",10:"staff",13:"fist",15:"dagger",
    16:"thrown",17:"polearm",18:"crossbow",19:"wand"
}
def dec(v):
    return Decimal(str(v))
def nearest(v):
    return int(dec(v).to_integral_value(rounding=ROUND_HALF_UP))
def below(v):
    return int(dec(v).to_integral_value(rounding=ROUND_FLOOR))
def column(row,stem,index):
    keys=(f"{stem}_{index}",f"{stem}[{index}]",f"{stem}.{index}")
    hits=[k for k in keys if k in row]
    if len(hits)!=1:
        raise ValueError(f"Missing or ambiguous {stem} index {index}")
    return row[hits[0]]
def numeric(row,key):
    if row is None or key not in row or str(row[key]).strip()=="":
        raise ValueError(f"Missing {key}")
    return dec(row[key])
def stat_group(item_class,subclass,inventory):
    if item_class==2:
        if subclass in (1,5,6,8,10,18,2,3):
            return 0
        if subclass==16: return 4
        return 3
    if item_class==4:
        if inventory in (1,5,7,20): return 0
        if inventory in (3,6,8,10,12): return 1
        if inventory in (2,9,11,14,16,23): return 2
    return None
def derive_stats(sparse, main, tables):
    """Returns (known static stat amounts, unresolved reasons)."""
    reasons=[]
    result={}
    q=int(sparse["OverallQualityID"])
    inventory=int(sparse["InventoryType"])
    group=stat_group(int(main["ClassID"]),int(main["SubclassID"]),inventory)
    budget=None
    if q in (2,3,4) and group is not None:
        allocation=tables.get("RandPropPoints",{}).get(int(sparse["ItemLevel"]))
        if allocation:
            prefix={2:"Good",3:"Superior",4:"Epic"}[q]
            try: budget=numeric(allocation,next(k for k in
                (f"{prefix}_{group}",f"{prefix}[{group}]") if k in allocation))
            except (StopIteration,ValueError):
                reasons.append("missing-stat-budget-column")
        else: reasons.append("missing-RandPropPoints-item-level")
    else: reasons.append("unsupported-quality-or-slot-budget")
    for i in range(10):
        try:
            typ=int(column(sparse,"StatModifier_bonusStat",i) or 0)
            weight=dec(column(sparse,"StatPercentEditor",i) or 0)
        except (ValueError,ArithmeticError):
            reasons.append(f"unreadable-stat-slot-{i}")
            continue
        if typ in (-1,0) and not weight: continue
        if typ not in STAT_IDS:
            reasons.append(f"unsupported-stat-id-{typ}")
            continue
        if budget is None:
            reasons.append(f"unresolved-stat-{typ}")
            continue
        if weight<0:
            reasons.append(f"negative-stat-percent-{i}")
            continue
        # Candidate formula from WoWSims, NOT automatically considered verified.
        points=nearest(weight*budget/dec(10000))
        key=STAT_IDS[typ]
        result[key]=result.get(key,0)+points
    return result,sorted(set(reasons))
def derive_armor(sparse,main,tables):
    if int(main["ClassID"])!=4: return None,[]
    subclass=int(main["SubclassID"])
    inventory=int(sparse["InventoryType"])
    level=int(sparse["ItemLevel"]);quality=int(sparse["OverallQualityID"])
    if subclass==6:
        row=tables.get("ItemArmorShield",{}).get(level)
        if not row:return None,["missing-shield-budget"]
        try: return nearest(dec(column(row,"Quality",quality))),[]
        except ValueError:return None,["invalid-shield-budget"]
    if subclass not in (1,2,3,4) or inventory not in (1,3,5,6,7,8,9,10,16,20):
        return None,[]
    total=tables.get("ItemArmorTotal",{}).get(level)
    qual=tables.get("ItemArmorQuality",{}).get(level)
    location=tables.get("ArmorLocation",{}).get(5 if inventory==20 else inventory)
    if not all((total,qual,location)):
        return None,["missing-armor-scaling-table-row:"+
            ",".join(key for key,value in (
                ("ItemArmorTotal",total),("ItemArmorQuality",qual),
                ("ArmorLocation",location)) if not value)]
    try:
        factor=dec(column(qual,"Qualitymod",quality))
        loc_key={1:"Clothmodifier",2:"Leathermodifier",
                 3:"Chainmodifier",4:"Platemodifier"}[subclass]
        share=numeric(location,loc_key)
        amount=numeric(total,{1:"Cloth",2:"Leather",3:"Mail",4:"Plate"}[subclass])
        return nearest(amount*factor*share),[]
    except ValueError as exc:return None,[f"invalid-armor-scaling-column:{exc}"]
def derive_weapon(sparse,main,tables):
    if int(main["ClassID"])!=2:return None,[]
    inventory=int(sparse["InventoryType"]);subclass=int(main["SubclassID"])
    if inventory not in (13,15,17,21,22,25,26):
        return None,["unsupported-weapon-inventory"]
    level=int(sparse["ItemLevel"]);quality=int(sparse["OverallQualityID"])
    if subclass not in WEAPON_SUBCLASSES:
        return None,["unsupported-weapon-subclass"]
    if inventory in (15,25,26):
        if subclass==19: kind="ItemDamageWand"
        elif subclass==16:kind="ItemDamageThrown"
        elif subclass in (2,3,18):kind="ItemDamageRanged"
        else:return None,["ranged-inventory-subclass-mismatch"]
    else:
        # The client flag is 0x200 in Flags_1, not a guess from the
        # weapon's subclass or presence of spell-power stats. WoWSims
        # flags.go/item_damage.go documents this exact selection.
        flags=column(sparse,"Flags",1) if any(
            k in sparse for k in ("Flags_1","Flags[1]","Flags.1")) else None
        if flags is None:
            return None,["missing-caster-weapon-flag"]
        caster=bool(int(flags)&0x200)
        if inventory==17:
            kind="ItemDamageTwoHandCaster" if caster else "ItemDamageTwoHand"
        else:
            kind="ItemDamageOneHandCaster" if caster else "ItemDamageOneHand"
    row=tables.get(kind,{}).get(level)
    if not row:return None,[f"missing-{kind}-item-level"]
    speed=dec(sparse["ItemDelay"])/1000
    if speed<=0:return None,["missing-weapon-speed"]
    try:
        dps=dec(column(row,"Quality",quality))
        spread=dec(sparse["DmgVariance"])
        modifier=dec(sparse["QualityModifier"])
    except (ValueError,KeyError):
        return None,["missing-weapon-scaling-field"]
    if dps<=0 or spread<0 or spread>2:
        return None,["invalid-weapon-budget"]
    damage=dps*speed
    lo=max(dec(1),damage*(1-spread/2)+modifier*speed)
    hi=max(dec(1),damage*(1+spread/2)+modifier*speed)
    hand="ranged" if inventory in (15,25,26) else "two-hand" if inventory==17 else "one-hand"
    return {
        "hand":hand,"type":WEAPON_SUBCLASSES[subclass],
        "minimumDamage":below(lo),"maximumDamage":nearest(hi),
        "speedSeconds":float(speed)
    },[]
def candidate(sparse,main,tables):
    stats,sissues=derive_stats(sparse,main,tables)
    armor,aissues=derive_armor(sparse,main,tables)
    weapon,wissues=derive_weapon(sparse,main,tables)
    if armor is not None:stats["armor"]=armor
    return {"stats":dict(sorted(stats.items())),"weapon":weapon,
            "unresolved":sorted(set(sissues+aissues+wissues))}
