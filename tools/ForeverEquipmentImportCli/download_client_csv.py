#!/usr/bin/env python3
"""Pinned, local-only Forever DB2 CSV acquisition/staging. Python 3.10+ stdlib.

Fetch is opt-in, uses wago.tools' publicly documented CSV endpoint, and never
uploads assets. Stage never infers combat stats from incomplete schema guesses.
"""
import argparse
import csv
import datetime
import hashlib
import json
import pathlib
import re
import sys
import urllib.request

TABLES = (
    "Item", "ItemSparse", "RandPropPoints", "ItemEffect", "ItemXItemEffect", "SpellEffect",
    "SpellItemEnchantment", "ItemSet", "ItemSetSpell", "ItemDamageOneHand",
    "ItemDamageTwoHand", "ItemDamageRanged", "ItemArmorTotal",
    "ItemArmorQuality", "ItemArmorShield", "ArmorLocation",
)
BUILD = re.compile(r"^1\.60\.\d+\.\d+$")
def url(table, build):
    return f"https://wago.tools/db2/{table}/csv?build={build}&locale=enUS"

def require_build(build):
    if not BUILD.fullmatch(build):
        raise ValueError("Specify an exact 1.60.x.x Forever build, not latest.")

def fetch(args):
    require_build(args.build)
    out = pathlib.Path(args.directory) / args.build
    out.mkdir(parents=True, exist_ok=True)
    report = {"build": args.build, "product": "wow_classic_beta",
              "provider": "wago.tools", "tables": {}, "failures": {}}
    for table in (args.tables or TABLES):
        target = out / f"{table}.csv"
        try:
            if not target.exists() or args.force:
                req = urllib.request.Request(url(table, args.build), headers={
                    "User-Agent": "WarcraftSim/CP171 (local research; attribution wago.tools)",
                    "Accept": "text/csv",
                })
                with urllib.request.urlopen(req, timeout=90) as response:
                    data = response.read()
                if not data or b"," not in data[:2048]:
                    raise ValueError("Response is not a usable CSV export")
                target.write_bytes(data)
            data = target.read_bytes()
            report["tables"][table] = {
                "url": url(table, args.build),
                "sha256": hashlib.sha256(data).hexdigest(),
                "bytes": len(data),
            }
        except Exception as exc:
            report["failures"][table] = str(exc)
            print(f"FAILED {table}: {exc}", file=sys.stderr)
    (out / "acquisition-report.json").write_text(
        json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    if report["failures"]:
        print("One or more DB2 CSV tables could not be downloaded; inspect acquisition-report.json.", file=sys.stderr)
        return 1
    print(f"Downloaded {len(report['tables'])} pinned CSV tables to {out}.")
    return 0

def stage(args):
    require_build(args.build)
    src = pathlib.Path(args.directory) / args.build
    path = src / "ItemSparse.csv"
    if not path.is_file():
        raise FileNotFoundError(f"Missing {path}. Run fetch or place an authorized CSV export there.")
    # All output remains metadata-only. Schema changes must be reviewed before
    # adding combat columns or DB2 scaling rules.
    rows = []
    missing = 0
    seen = set()
    with path.open(newline="", encoding="utf-8-sig") as stream:
        reader = csv.DictReader(stream)
        if reader.fieldnames is None or "ID" not in reader.fieldnames:
            raise ValueError("ItemSparse CSV missing ID; do not guess table layout.")
        candidates = ("Display_lang", "Name_lang", "Name", "Display", "Display_0")
        name_col = next((x for x in candidates if x in reader.fieldnames), None)
        if name_col is None:
            raise ValueError("ItemSparse CSV name column unrecognized; check DBD schema.")
        for row in reader:
            rawid = (row.get("ID") or "").strip()
            name = (row.get(name_col) or "").strip()
            if not rawid.isdecimal() or not name:
                missing += 1
                continue
            item_id = int(rawid)
            if item_id <= 0 or item_id in seen:
                raise ValueError(f"Duplicate or invalid ItemSparse item ID {item_id}")
            seen.add(item_id)
            rows.append({
                "id": item_id, "name": name,
                "sourceUrl": url("ItemSparse", args.build),
                "verificationStatus": "metadata-only"
            })
    rows.sort(key=lambda x: x["id"])
    manifest = {
        "catalogVersion": f"forever-{args.build}-metadata-only",
        "contentVersion": f"item-sparse-csv-{args.build}",
        "clientBuild": args.build,
        "sourceName": "wago.tools-pinned-itemsparse-csv",
        "sourceUrl": url("ItemSparse", args.build),
        "importedAt": args.imported_at,
        "items": [], "metadataOnly": rows, "overrides": [], "sets": []
    }
    pathlib.Path(args.output).parent.mkdir(parents=True, exist_ok=True)
    pathlib.Path(args.output).write_text(
        json.dumps(manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f"Staged {len(rows)} metadata-only items; excluded {missing} missing IDs/names. 0 combat items promoted.")
    return 0

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    f = sub.add_parser("fetch", help="Download pinned public CSV exports (no local client required)")
    f.add_argument("--build", required=True)
    f.add_argument("--directory", required=True)
    f.add_argument("--force", action="store_true")
    f.add_argument("--tables", nargs="+", choices=TABLES, help="Subset to acquire; default is all equipment-related tables")
    s = sub.add_parser("stage", help="Convert an ItemSparse CSV to metadata-only manifest")
    s.add_argument("--build", required=True)
    s.add_argument("--directory", required=True)
    s.add_argument("--output", required=True)
    s.add_argument("--imported-at", required=True, help="Explicit UTC ISO 8601 timestamp for repeatability")
    args = parser.parse_args()
    try:
        if args.command == "stage":
            datetime.datetime.fromisoformat(args.imported_at.replace("Z", "+00:00"))
            return stage(args)
        return fetch(args)
    except (OSError, ValueError) as exc:
        parser.error(str(exc))
        return 2

if __name__ == "__main__":
    raise SystemExit(main())
