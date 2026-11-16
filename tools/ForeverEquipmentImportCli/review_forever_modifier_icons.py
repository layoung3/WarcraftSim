#!/usr/bin/env python3
"""Reproduce display-only modifier icon joins from the pinned talent-art source.

Only exact existing item/spell IDs and names are eligible. Documented UI suffixes
are removed for matching; absent IDs, renamed items and ambiguous icons stay
unresolved. No source stats or mechanics are imported. Blizzard artwork is an
optional ignored local cache, never a redistributable asset or verified live data.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from cache_forever_talent_icons import download
from acquisition_cache import request_bytes

SOURCE_COMMIT = "1cc5ae1e9abdbf941b7326d2d90421822dce31ba"
SOURCE_URL = ("https://raw.githubusercontent.com/gunba/wow-forever-sim/" +
              SOURCE_COMMIT + "/assets/database/db.json")
SOURCE_SHA256 = "e342a7ce2dbc9ebc3e3beb2b0c23ed379b665f549ff64c97f4adec2ed61eb8de"
SCHEMA = "warcraftsim-forever-modifier-icons-v1"
STATUS = "historical-reference-not-current-70707-verified"
DEFAULT_CATALOG = Path("WarcraftSim.Api/Simulation/ForeverExternalModifierCatalog.cs")
DEFAULT_MANIFEST = Path("WarcraftSim.Api/Catalog/forever-modifier-icons-cp175-provisional.json")
DEFAULT_EQUIPMENT_MANIFEST = Path("WarcraftSim.Api/Catalog/forever-equipment-icons-cp175-provisional.json")
EQUIPMENT_SCHEMA = "warcraftsim-forever-equipment-icons-v1"


def catalog_entries(path):
    """Read only the static constructor identity fields, never execute C#."""
    pattern = r'new\("([^"]+)","([^"]+)","([^"]+)",(\d+),(\d+|null),'
    rows = [{"key": key, "name": name, "category": category,
             "spellId": int(spell), "itemId": None if item == "null" else int(item)}
            for key, name, category, spell, item in
            re.findall(pattern, Path(path).read_text(encoding="utf-8"))]
    if not rows or len({x["key"] for x in rows}) != len(rows):
        raise ValueError("Expected unique static modifier constructor identities")
    return rows


def source_name(name):
    return re.sub(r" \((?:Well Fed|Forever|Rank [1-9][0-9]*)\)$", "", name)


def analyze(catalog, source):
    if not isinstance(source, dict) or any(
            not isinstance(source.get(key), list) for key in ("itemIcons", "spellIcons")):
        raise ValueError("Expected pinned itemIcons and spellIcons arrays")
    records, unresolved = [], []
    for entry in sorted(catalog, key=lambda x: x["key"]):
        kind = "item" if entry.get("itemId") else "spell"
        source_id = entry.get("itemId") if kind == "item" else entry.get("spellId")
        reason = "no-existing-item-or-spell-id"
        rows = []
        if source_id:
            rows = [x for x in source[kind + "Icons"] if isinstance(x, dict) and
                    x.get("id") == source_id]
            reason = "id-absent-from-pinned-source" if not rows else "name-rank-or-icon-mismatch"
        rank = re.search(r" \(Rank ([1-9][0-9]*)\)$", entry["name"])
        matches = [x for x in rows if
                   x.get("name") == source_name(entry["name"]) and
                   (kind != "spell" or not rank or "rank" not in x or
                    x["rank"] == int(rank.group(1))) and
                   isinstance(x.get("icon"), str) and
                   re.fullmatch(r"[a-z0-9_]+", x["icon"])]
        keys = {x["icon"] for x in matches}
        if len(keys) == 1:
            records.append({**entry, "sourceKind": kind, "sourceId": source_id,
                            "sourceName": source_name(entry["name"]),
                            "iconKey": keys.pop(), "iconSourceStatus": STATUS})
        else:
            unresolved.append({**entry, "reason": "ambiguous-icon" if len(keys) > 1 else reason,
                               "observedSourceNames": sorted({str(x.get("name", "")) for x in rows})})
    return {"records": records, "unresolved": unresolved}


def build_manifest(catalog, payload):
    if hashlib.sha256(payload).hexdigest() != SOURCE_SHA256:
        raise ValueError("Pinned database checksum mismatch")
    return {"schema": SCHEMA, "sourceRepository": "gunba/wow-forever-sim",
            "sourceCommit": SOURCE_COMMIT, "sourceUrl": SOURCE_URL,
            "sourceLicense": "MIT (repository metadata; not a Blizzard artwork license)",
            "sourceLicenseUrl": "https://github.com/gunba/wow-forever-sim/blob/" + SOURCE_COMMIT + "/LICENSE",
            "sourceSha256": SOURCE_SHA256, "sourceStatus": STATUS,
            "artworkNotice": "Blizzard artwork; optional ignored local cache only. No redistribution license is asserted.",
            **analyze(catalog, json.loads(payload))}


def equipment_identity_entries(items):
    ids = [x["id"] for x in items]
    if len(set(ids)) != len(ids):
        raise ValueError("Equipment identity list contains duplicate IDs")
    return [{"key": "item-" + str(x["id"]), "name": x["name"],
             "category": "equipment", "itemId": x["id"], "spellId": 0} for x in items]


def registered_equipment(catalog_directory):
    # Match the existing additive catalog ownership order; import identities only.
    items = {}
    for filename in ("forever-equipment-v1.json", "forever-equipment-cp171-reviewed.json",
                     "forever-equipment-cp172-effects.json"):
        document = json.loads((catalog_directory / filename).read_text(encoding="utf-8"))
        rows = document["items"]
        equipment_identity_entries(rows)  # Never silently collapse duplicate rows in one source.
        items.update((x["id"], {"id": x["id"], "name": x["name"]}) for x in rows)
    return list(items.values())


def build_equipment_manifest(items, payload, previous):
    # Reuse the exact-ID/name/safe-key matcher, but preserve the old manifest's
    # separate source identity. Never claim the old Napalmsteak mappings as gunba.
    manifest = build_manifest(equipment_identity_entries(items), payload)
    old = previous["candidates"]
    if len({x["itemId"] for x in old}) != len(old):
        raise ValueError("Previous equipment artwork has duplicate IDs")
    old_by_id = {x["itemId"]: x for x in old}
    candidates = []
    for row in sorted(manifest["records"], key=lambda x: x["itemId"]):
        if row["name"] != row["sourceName"]:
            continue  # Equipment names do not have modifier-specific UI suffixes.
        existing = old_by_id.get(row["itemId"])
        if existing:
            if existing["itemName"] != row["name"] or existing["iconKey"] != row["iconKey"]:
                raise ValueError("Equipment artwork source conflict for " + str(row["itemId"]))
            continue
        candidates.append({"itemId": row["itemId"], "itemName": row["name"],
                           "iconKey": row["iconKey"], "sourceStatus": STATUS})
    return {key: value for key, value in manifest.items() if key not in ("records", "unresolved", "schema")} | {
        "schema": EQUIPMENT_SCHEMA, "candidates": candidates}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--catalog", type=Path, default=DEFAULT_CATALOG)
    parser.add_argument("--manifest", type=Path, default=DEFAULT_MANIFEST)
    parser.add_argument("--equipment-manifest", type=Path,
                        help="Also reproduce/check/cache the separate additive equipment artwork manifest")
    parser.add_argument("--source-file", type=Path, help="Optional previously fetched pinned database")
    parser.add_argument("--check", action="store_true", help="Verify rather than rewrite the reviewed manifest")
    parser.add_argument("--image-dir", type=Path, help="Optional ignored local JPEG cache")
    parser.add_argument("--refresh", action="store_true", help="Revalidate cached art without repinning changed bytes")
    parser.add_argument("--revalidate-legacy", action="store_true", help="Receipt old art only after an exact fresh pinned-source match")
    parser.add_argument("--existing-only", action="store_true", help="Never acquire artwork for missing local files")
    args = parser.parse_args()
    if args.source_file:
        payload = args.source_file.read_bytes()
    else:
        payload, _ = request_bytes(SOURCE_URL, 3_000_000)
        if payload is None:
            raise ValueError("HTTP 304 without a verified local database")
    manifest = build_manifest(catalog_entries(args.catalog), payload)
    if args.check:
        if json.loads(args.manifest.read_text(encoding="utf-8")) != manifest:
            raise ValueError("Reviewed modifier icon manifest differs from the pinned source join")
    else:
        args.manifest.parent.mkdir(parents=True, exist_ok=True)
        args.manifest.write_text(json.dumps(manifest, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    equipment = None
    if args.equipment_manifest:
        directory = args.equipment_manifest.parent
        previous = json.loads((directory / "forever-equipment-icons-cp174-provisional.json").read_text(encoding="utf-8"))
        equipment = build_equipment_manifest(registered_equipment(directory), payload, previous)
        if args.check:
            if json.loads(args.equipment_manifest.read_text(encoding="utf-8")) != equipment:
                raise ValueError("Reviewed equipment icon manifest differs from the pinned source join")
        else:
            args.equipment_manifest.write_text(json.dumps(equipment, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    counts = {"downloaded": 0, "cached": 0, "failed": 0}
    if args.image_dir:
        args.image_dir.mkdir(parents=True, exist_ok=True)
        keys = {x["iconKey"] for x in manifest["records"]}
        if equipment:
            keys.update(x["iconKey"] for x in equipment["candidates"])
        for key in sorted(keys):
            if args.existing_only and not (args.image_dir / (key + ".jpg")).is_file():
                continue
            try:
                counts[download(key, args.image_dir, 15, refresh=args.refresh,
                                revalidate_legacy=args.revalidate_legacy)] += 1
            except Exception as error:
                counts["failed"] += 1
                print("Unavailable:", key, str(error))
    print("Pinned modifier icons:", len(manifest["records"]), "unresolved:", len(manifest["unresolved"]), counts)
    if equipment:
        print("Additional pinned equipment icons:", len(equipment["candidates"]))
    if counts["failed"]:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
