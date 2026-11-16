#!/usr/bin/env python3
"""Pinned, local-only Forever DB2 CSV acquisition/staging. Python 3.10+ stdlib.

Fetch is opt-in, uses wago.tools' publicly documented CSV endpoint, and never
uploads assets. Stage never infers combat stats from incomplete schema guesses.
"""
import argparse
import csv
import datetime
import hashlib
import io
import json
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))
from acquisition_cache import (acquire, atomic_write, bounded_file, digest, json_bytes,
                               receipt_path, validate_cached)

MAX_CSV_BYTES = 64_000_000

TABLES = (
    "Item", "ItemSparse", "RandPropPoints", "ItemEffect", "ItemXItemEffect", "SpellEffect",
    "Spell", "SpellItemEnchantment", "PlayerExpectedStat", "ChrRaces", "ChrClasses", "ItemSet", "ItemSetSpell", "ItemDamageOneHand",
    "ItemDamageTwoHand", "ItemDamageRanged", "ItemArmorTotal",
    "ItemArmorQuality", "ItemArmorShield", "ArmorLocation",
)
BUILD = re.compile(r"^1\.60\.\d+\.\d+$")
def url(table, build):
    require_build(build)
    if table not in TABLES:
        raise ValueError("Unsupported CSV table")
    return f"https://wago.tools/db2/{table}/csv?build={build}&locale=enUS"

def require_build(build):
    if not BUILD.fullmatch(build):
        raise ValueError("Specify an exact 1.60.x.x Forever build, not latest.")


def provenance(table, build, product):
    if not isinstance(product, str) or not re.fullmatch(r"[a-z0-9_]+", product):
        raise ValueError("Product must be an explicit product key or unspecified")
    return {"url": url(table, build), "kind": "db2-csv", "table": table,
            "requestedBuild": build, "requestedProduct": product,
            "buildProductAttestation": "requested-only-not-attested-by-response"}


def validate_csv(data):
    if b"\0" in data:
        raise ValueError("CSV contains NUL bytes")
    reader = csv.reader(io.StringIO(data.decode("utf-8-sig"), newline=""), strict=True)
    fields = next(reader, None)
    if not fields or len(fields) < 2 or "ID" not in fields or len(fields) != len(set(fields)):
        raise ValueError("CSV requires unique headers including ID; do not guess schema")
    for row in reader:
        if row and len(row) != len(fields):
            raise ValueError("CSV row width mismatch (possibly truncated)")


def write_report(out, report):
    # Retain every previous report, including pre-receipt legacy evidence.
    path = out / "acquisition-report.json"
    if path.exists():
        previous = bounded_file(path, MAX_CSV_BYTES)
        archived = out / "acquisition-reports" / (digest(previous) + ".json")
        if not archived.exists():
            atomic_write(archived, previous)
    data = json_bytes(report)
    archived = out / "acquisition-reports" / (digest(data) + ".json")
    if not archived.exists():
        atomic_write(archived, data)
    atomic_write(path, data)


def check_legacy_evidence(out, table, source):
    """A fresh exact match cannot override an older recorded hash or identity."""
    target = out / (table + ".csv")
    if not target.is_file() or receipt_path(target).exists():
        return
    payload = bounded_file(target, MAX_CSV_BYTES)
    reports = [out / "acquisition-report.json", *sorted((out / "acquisition-reports").glob("*.json"))]
    for path in reports:
        if not path.exists():
            continue
        old = json.loads(bounded_file(path, MAX_CSV_BYTES))
        record = old.get("tables", {}).get(table)
        if record is None:
            continue
        if (old.get("build") != source["requestedBuild"] or old.get("product") != source["requestedProduct"] or
                record.get("url") != source["url"] or record.get("sha256") != digest(payload) or
                record.get("bytes") != len(payload)):
            raise ValueError("Legacy acquisition evidence mismatch; preserve prior receipts and use a separate reviewed destination")

def fetch(args):
    require_build(args.build)
    out = pathlib.Path(args.directory) / args.build
    out.mkdir(parents=True, exist_ok=True)
    product = getattr(args, "product", "unspecified")
    report = {"build": args.build, "product": product,
              "buildProductAttestation": "requested-only-not-attested-by-response",
              "provider": "wago.tools", "tables": {}, "failures": {}}
    for table in (args.tables or TABLES):
        target = out / f"{table}.csv"
        try:
            source = provenance(table, args.build, product)
            if getattr(args, "revalidate_legacy", False):
                check_legacy_evidence(out, table, source)
            data, receipt, status = acquire(target, source,
                validate_csv, MAX_CSV_BYTES, refresh=args.force,
                revalidate_legacy=getattr(args, "revalidate_legacy", False),
                timeout=getattr(args, "timeout", 30), attempts=getattr(args, "attempts", 3),
                headers={"Accept": "text/csv"})
            report["tables"][table] = {
                "url": url(table, args.build),
                "sha256": hashlib.sha256(data).hexdigest(),
                "bytes": len(data),
                "status": status, "receipt": receipt_path(target).name,
                "requestedBuild": args.build, "requestedProduct": product,
            }
        except Exception as exc:
            report["failures"][table] = str(exc)
            print(f"FAILED {table}: {exc}", file=sys.stderr)
    write_report(out, report)
    if report["failures"]:
        print("One or more DB2 CSV tables could not be downloaded; inspect acquisition-report.json.", file=sys.stderr)
        return 1
    print(f"Validated {len(report['tables'])} pinned CSV tables in {out}.")
    return 0

def stage(args):
    require_build(args.build)
    src = pathlib.Path(args.directory) / args.build
    path = src / "ItemSparse.csv"
    if not path.is_file():
        raise FileNotFoundError(f"Missing {path}. Run fetch or place an authorized CSV export there.")
    data = bounded_file(path, MAX_CSV_BYTES)
    validate_csv(data)
    product = getattr(args, "product", "unspecified")
    source_status = "unreceipted-local-input-not-source-verified"
    if receipt_path(path).exists():
        if product == "unspecified":
            saved = json.loads(bounded_file(receipt_path(path), 16_384))
            product = saved.get("provenance", {}).get("requestedProduct", "unspecified")
        validate_cached(path, provenance("ItemSparse", args.build, product), validate_csv, MAX_CSV_BYTES)
        source_status = "checksum-verified-requested-build-not-response-attested"
    # All output remains metadata-only. Schema changes must be reviewed before
    # adding combat columns or DB2 scaling rules.
    rows = []
    missing = 0
    seen = set()
    with io.StringIO(data.decode("utf-8-sig"), newline="") as stream:
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
        "requestedProduct": product, "sourceSha256": digest(data), "acquisitionStatus": source_status,
        "sourceName": "wago.tools-pinned-itemsparse-csv",
        "sourceUrl": url("ItemSparse", args.build),
        "importedAt": args.imported_at,
        "items": [], "metadataOnly": rows, "overrides": [], "sets": []
    }
    pathlib.Path(args.output).parent.mkdir(parents=True, exist_ok=True)
    atomic_write(args.output, (json.dumps(manifest, indent=2, ensure_ascii=False) + "\n").encode("utf-8"))
    print(f"Staged {len(rows)} metadata-only items; excluded {missing} missing IDs/names. 0 combat items promoted.")
    return 0

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    f = sub.add_parser("fetch", help="Download pinned public CSV exports (no local client required)")
    f.add_argument("--build", required=True)
    f.add_argument("--directory", required=True)
    f.add_argument("--product", default="unspecified", help="Requested product key, not provider-attested")
    f.add_argument("--force", action="store_true", help="Revalidate only; changed pinned bytes are never overwritten")
    f.add_argument("--revalidate-legacy", action="store_true", help="Receipt legacy bytes only after an exact fresh source match")
    f.add_argument("--timeout", type=float, default=30)
    f.add_argument("--attempts", type=int, default=3)
    f.add_argument("--tables", nargs="+", choices=TABLES, help="Subset to acquire; default is all equipment-related tables")
    s = sub.add_parser("stage", help="Convert an ItemSparse CSV to metadata-only manifest")
    s.add_argument("--build", required=True)
    s.add_argument("--directory", required=True)
    s.add_argument("--product", default="unspecified", help="Require this product if checking a receipt")
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
