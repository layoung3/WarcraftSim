#!/usr/bin/env python3
"""Reproduce compact Paladin facts from retained, hash-pinned source inputs.

This is deliberately offline. Supply the capture directory listed in the
ledger. Changed bytes fail before extraction; a newer web resource needs a
separate review and ledger revision. No scraped text controls execution.
"""
import argparse
import hashlib
import html
import json
import re
from pathlib import Path


def key(name):
    return re.sub(r"[^a-z0-9]+", "-", name.lower().replace("'", "").replace("’", "")).strip("-")


def clean(fragment):
    # Crossed-out comparison values are historical, never current inputs.
    fragment = re.sub(r"<(?:s|del)\b[^>]*>.*?</(?:s|del)>", "", fragment, flags=re.S)
    fragment = re.sub(r'<span\b[^>]*class="[^"]*\bdel\b[^"]*"[^>]*>.*?</span>', "", fragment, flags=re.S)
    return re.sub(r"\s+", " ", html.unescape(re.sub(r"<[^>]*>", " ", fragment))).strip()


def digest(data):
    return hashlib.sha256(data).hexdigest()


def facts(inputs, ledger):
    verified = {}
    for source in ledger["sources"]:
        data = (inputs / source["file"]).read_bytes()
        if len(data) != source["sizeBytes"] or digest(data) != source["sha256"]:
            raise ValueError("Source bytes differ from reviewed input: " + source["key"])
        verified[source["key"]] = data.decode("utf-8")

    raw = verified["talents-page"]
    match = re.search(r'<script\b[^>]*data-tc-data[^>]*>(.*?)</script>', raw, re.S)
    if not match:
        raise ValueError("Current talent JSON not found")
    topology = json.loads(match.group(1))
    modern = json.loads(verified["current-ui-sim-talents-trees-paladin"])
    spell_ids = {key(t["fancyName"]): t["spellId"] for tree in modern for t in tree["talents"]}
    by_id = {t["id"]: key(t["name"]) for tree in topology["trees"] for t in tree["talents"]}
    talents = []
    for tree in topology["trees"]:
        for node in tree["talents"]:
            talent_key = key(node["name"])
            talents.append({
                "key": talent_key, "talentId": node["id"], "spellId": spell_ids[talent_key],
                "tree": key(tree["name"]), "row": node["row"] + 1, "column": node["col"] + 1,
                "maxRank": node["maxRank"], "pointsInEarlierRows": node["points"],
                "prerequisites": [{"key": by_id[r["talent"]], "rank": r["rank"]} for r in node["req"]],
                "iconKey": node["icon"],
                "rankTextSha256": [digest(x.encode("utf-8")) for x in node["desc"]],
                "rankNumericTokens": [re.findall(r"-?\d+(?:\.\d+)?%?", x) for x in node["desc"]],
            })
    if len(talents) != 51 or set(spell_ids) != {x["key"] for x in talents}:
        raise ValueError("The two independent current talent sources disagree on node identity")
    for tree in ("holy", "protection", "retribution"):
        if sum(x["tree"] == tree for x in talents) != 17:
            raise ValueError("Current tree does not contain its reviewed 17 nodes: " + tree)

    abilities = []
    for attributes, body in re.findall(r"<article\b([^>]*)>(.*?)</article>", verified["spells-page"], re.S):
        if 'class="sb-ab' not in attributes:
            continue
        ability_key = re.search(r'\bid="([^"]+)"', attributes).group(1)
        status = re.search(r'\bdata-st="([^"]+)"', attributes).group(1)
        ranks = []
        table = re.search(r'<table class="sb-ranks">(.*?)</table>', body, re.S)
        if table:
            for rank, level, row in re.findall(r'<tr data-r="(\d+)" data-lvl="(\d+)"[^>]*>(.*?)</tr>', table.group(1), re.S):
                cells = [clean(x) for x in re.findall(r"<td[^>]*>(.*?)</td>", row, re.S)]
                if len(cells) != 4 or cells[1] != level:
                    raise ValueError("Current rank parse includes a historical value: " + ability_key)
                ranks.append({"rank": int(rank), "level": int(level), "cost": cells[2], "cooldown": cells[3]})
        abilities.append({"key": ability_key, "status": status, "ranks": ranks})
    ability_by_key = {x["key"]: x for x in abilities}
    if ability_by_key["divine-favor"]["ranks"][0]["level"] != 20:
        raise ValueError("Divine Favor's historical level leaked into current extraction")
    if ability_by_key["righteous-fury"]["ranks"][0]["level"] != 10:
        raise ValueError("Righteous Fury's historical level leaked into current extraction")

    # Numeric effect facts only. No bulk client rows or copied tooltips are
    # committed. Raw bytes stay in the capture directory and their hash is pinned.
    effect_ids = set(spell_ids.values()) | {
        25292,19943,20930,25902,25903,1311595,1311596,1311597,10333,
        20924,1280349,1280353,10314,10318,24239,1239570,1020,5573,10310,
        10308,10278,1044,1152,4987,10326,25780,20293,20286,20423,20418,
        20414,20349,20343,20357,20353,20355,20346,20308,20303,20164,20170,
        20184,20920,20966,20424,1326084,1328551,1311703,1311704,1311705,
        1311701,1328198,1328206,10293,10301,19746,19896,19898,19899,
        25291,25290,20217,6940,20729,19752,429147,440667,25997,20271,
    }
    spell_rows = []
    raw_spells = verified["current-spell-data"]
    for spell_id, block in re.findall(r"\n\t\{ID: (\d+),(.*?)(?=\n\t\{ID: |\Z)", raw_spells, re.S):
        if int(spell_id) not in effect_ids:
            continue
        header = block.split("\n")[0]
        record = {"id": int(spell_id), "name": re.search(r'Name: "([^"]+)"', header).group(1)}
        for field in ("SpellLevel", "BaseLevel", "MaxLevel", "DurationMs", "CastTimeMs", "CategoryCooldownMs", "CooldownMs", "GCDMs", "MinRange", "MaxRange", "MaxTargets", "ProcChance", "ProcCharges", "ICDMs", "MaxStack", "EquipClass", "EquipSubclass"):
            match = re.search(r"\b" + field + r": (-?\d+(?:\.\d+)?)", header)
            if match:
                record[field] = float(match.group(1)) if "." in match.group(1) else int(match.group(1))
        effects = []
        for effect_line in re.findall(r"\n\t\t\t\{(.*?)\},", block):
            effect = {}
            for field in ("ID", "Index", "BasePoints", "PPL", "Variance", "BaseLevel", "MaxLevel", "SPCoef", "APCoef", "PeriodMs", "TriggerID", "Misc", "RadiusMax"):
                match = re.search(r"\b" + field + r": (-?\d+(?:\.\d+)?)", effect_line)
                if match:
                    effect[field] = float(match.group(1)) if "." in match.group(1) else int(match.group(1))
            for field in ("Type", "Aura"):
                match = re.search(r"\b" + field + r": dbcenums\.(\w+)", effect_line)
                if match:
                    effect[field] = match.group(1)
            effects.append(effect)
        record["effects"] = effects
        record["powers"] = [{k: float(v) if "." in v else int(v) for k, v in re.findall(r"(Cost|CostPct): (-?\d+(?:\.\d+)?)", power)} for power in re.findall(r"Powers: \[\]Power\{(.*?)\}\},", block)]
        spell_rows.append(record)
    return {"schema": "cp175-paladin-source-facts-v1", "talents": talents, "spellBook": abilities,
            "spellEffects": spell_rows, "sourceInputCount": len(verified)}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--inputs", required=True, type=Path)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    root = Path(__file__).resolve().parent
    ledger = json.loads((root / "CP175-paladin-source-ledger.json").read_text())
    actual = facts(args.inputs, ledger)
    output = root / "CP175-paladin-source-facts.json"
    if args.check:
        if actual != json.loads(output.read_text()):
            raise SystemExit("Extracted source facts differ from the reviewed snapshot")
    else:
        output.write_text(json.dumps(actual, ensure_ascii=False, separators=(",", ":")) + "\n")
    print(json.dumps({"verifiedSourceInputs": actual["sourceInputCount"], "talentNodes": len(actual["talents"]),
                      "abilityGroups": len(actual["spellBook"]), "effectRecords": len(actual["spellEffects"]),
                      "mode": "check" if args.check else "extract"}, sort_keys=True))


if __name__ == "__main__":
    main()
