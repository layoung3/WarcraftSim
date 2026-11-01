#!/usr/bin/env python3
"""Derive review-only candidate item icons from pinned Forever client CSV tables.

No item name matching, historical guessing, or automatic runtime promotion.
Requires ItemModifiedAppearance and ItemAppearance CSVs from one documented
client build. Ambiguous appearances are quarantined.
"""
import argparse
import csv
import hashlib
import json
from pathlib import Path


def read_csv(path, columns):
    with Path(path).open(newline="", encoding="utf-8-sig") as handle:
        reader = csv.DictReader(handle)
        if reader.fieldnames is None or not set(columns).issubset(reader.fieldnames):
            raise ValueError(f"{path}: expected columns {columns}")
        return list(reader)


def sha256(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def candidates(modified_path, appearance_path, item_ids, build):
    if not build.startswith("1.60.1."):
        raise ValueError("The source build must be an explicit Forever 1.60.1 build")
    modified = read_csv(modified_path, ["ItemID", "ItemAppearanceID"])
    appearances = read_csv(appearance_path, ["ID", "DefaultIconFileDataID"])
    appearance_by_id = {}
    for row in appearances:
        appearance_id = int(row["ID"])
        icon_id = int(row["DefaultIconFileDataID"] or "0")
        if appearance_id > 0 and icon_id > 0:
            if appearance_id in appearance_by_id and appearance_by_id[appearance_id] != icon_id:
                raise ValueError("Contradictory appearance ID: " + str(appearance_id))
            appearance_by_id[appearance_id] = icon_id
    icon_candidates = {int(item_id): set() for item_id in item_ids}
    for row in modified:
        item_id = int(row["ItemID"] or "0")
        if item_id not in icon_candidates:
            continue
        appearance_id = int(row["ItemAppearanceID"] or "0")
        icon = appearance_by_id.get(appearance_id)
        if icon:
            icon_candidates[item_id].add(icon)
    approved_candidates = [
        {"itemId": item_id, "iconFileDataId": next(iter(values)),
         "status": "unapproved-client-join-candidate"}
        for item_id, values in sorted(icon_candidates.items())
        if len(values) == 1
    ]
    ambiguous = [
        {"itemId": item_id, "iconFileDataIds": sorted(values),
         "reason": "multiple-client-appearances"}
        for item_id, values in sorted(icon_candidates.items())
        if len(values) > 1
    ]
    unresolved = sorted(item_id for item_id, values in icon_candidates.items() if not values)
    return {
        "schema": "warcraftsim-item-icon-candidates-v1",
        "build": build,
        "source": {
            "ItemModifiedAppearanceSha256": sha256(modified_path),
            "ItemAppearanceSha256": sha256(appearance_path),
        },
        "reviewedRuntimeItemsOnly": True,
        "automaticApproval": False,
        "candidates": approved_candidates,
        "ambiguous": ambiguous,
        "unresolvedItemIds": unresolved,
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--modified", type=Path, required=True)
    parser.add_argument("--appearances", type=Path, required=True)
    parser.add_argument("--catalog", type=Path, required=True)
    parser.add_argument("--build", required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    catalog = json.loads(args.catalog.read_text(encoding="utf-8"))
    items = catalog.get("items")
    if not isinstance(items, list):
        raise ValueError("Catalog must include a verified items array")
    ids = [int(item["id"]) for item in items]
    if len(set(ids)) != len(ids):
        raise ValueError("Duplicate catalog item IDs")
    report = candidates(args.modified, args.appearances, ids, args.build)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n",
                           encoding="utf-8")
    print(f"{len(report['candidates'])} unique candidates; "
          f"{len(report['ambiguous'])} ambiguous; "
          f"{len(report['unresolvedItemIds'])} unresolved. No runtime promotion.")


if __name__ == "__main__":
    main()
