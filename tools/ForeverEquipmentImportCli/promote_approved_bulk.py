#!/usr/bin/env python3
"""Promote CP171 approved DB2 bulk output into the existing embedded reviewed manifest.

Input must be produced by bulk_normalize.py using checksum-bound --resolved-values
and --approval. Metadata-only rows remain quarantined. No network calls.
Dry-run is default; --write updates the reviewed manifest deterministically.
"""
import argparse
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CAT = ROOT / "WarcraftSim.Api" / "Catalog"
REVIEWED = CAT / "forever-equipment-cp171-reviewed.json"
BASE = CAT / "forever-equipment-v1.json"
EFFECTS = CAT / "forever-equipment-cp172-effects.json"


def load(path):
    return json.loads(Path(path).read_text(encoding="utf-8"))


def canonical(data):
    return (json.dumps(data, ensure_ascii=False, sort_keys=True, indent=2) + "\n").encode("utf-8")


def promote(batch, report, reviewed, base, effects, *, allow_overrides=False):
    """Return (reviewed manifest, report); reject malformed/unauthorized source."""
    if not report or report.get("approvedCombatRecords") != len(batch.get("items", [])):
        raise ValueError("Batch lacks matching approvedCombatRecords provenance report")
    if report.get("reviewedValuesSha256") is None or not isinstance(report.get("reviewApproval"), dict):
        raise ValueError("Batch lacks checksum-bound resolved values and review approval")
    approval = report["reviewApproval"]
    if (approval.get("status") != "reviewed" or
        approval.get("clientBuild") != batch.get("clientBuild") or
        approval.get("resolvedValuesSha256") != report["reviewedValuesSha256"] or
        not approval.get("reviewer") or
        not str(approval.get("sourceUrl", "")).startswith("https://")):
        raise ValueError("Approval/build/digest mismatch")
    if batch.get("clientBuild") == "mixed-reviewed":
        raise ValueError("New bulk batch must be pinned to one real client build")
    if not str(batch.get("sourceUrl", "")).startswith("https://"):
        raise ValueError("Batch requires HTTPS source")
    original = {x["id"]: x for x in reviewed["items"]}
    baseline = {x["id"]: x for x in base["items"]}
    reserved = {x["id"]: x for x in effects["items"]}
    if len(original) != len(reviewed["items"]):
        raise ValueError("Existing reviewed catalog contains duplicate IDs")
    imported = {}
    already_present = 0
    rejected = []
    existing_overrides = {x["itemId"]: x for x in reviewed.get("overrides", [])}
    new_overrides = {}
    for row in sorted(batch["items"], key=lambda x: x.get("id", 0)):
        ident = row.get("id")
        reasons = []
        if not isinstance(ident, int) or ident <= 0 or ident in imported:
            reasons.append("invalid-or-duplicate-batch-id")
        if ident in reserved:
            reasons.append("cp172-reserved-executable-or-effect-owned-item")
        if not row.get("stats") and not row.get("weapon"):
            reasons.append("missing-reviewed-combat-stats-or-weapon")
        if (row.get("sourceBuild") != batch["clientBuild"] or
            row.get("verificationStatus") != "client-db2-reviewed" or
            row.get("sourceStatus") != "forever-client-db2-reviewed" or
            not str(row.get("sourceUrl", "")).startswith("https://")):
            reasons.append("invalid-reviewed-source-build-or-attribution")
        if not row.get("allowedSlots") or not row.get("name"):
            reasons.append("missing-identity-or-slot")
        if "main-hand" in row.get("allowedSlots", []) or "ranged" in row.get("allowedSlots", []):
            w = row.get("weapon") or {}
            if not (w.get("speedSeconds", 0) > 0 and
                    w.get("maximumDamage", -1) >= w.get("minimumDamage", 0) >= 0):
                reasons.append("missing-reviewed-weapon-damage-or-speed")
        if ident in original and original[ident] == row:
            already_present += 1
            continue
        if ident in original or ident in baseline:
            if not allow_overrides or ident not in {x.get("itemId") for x in batch.get("overrides", [])}:
                reasons.append("existing-item-no-explicit-approved-override")
            else:
                new_overrides[ident] = {
                    "itemId": ident,
                    "reason": "Approved pinned DB2 reviewed-values replacement; see acquisition report",
                    "sourceUrl": row["sourceUrl"],
                }
        if reasons:
            rejected.append({"id": ident, "reasons": reasons, "sourceUrl": row.get("sourceUrl")})
            continue
        imported[ident] = row
    if rejected:
        # Fail closed rather than silently publish an incomplete approved input.
        return reviewed, {"accepted": 0, "rejected": rejected, "unchanged": True}
    updated = dict(reviewed)
    updated["items"] = sorted(
        [x for x in reviewed["items"] if x["id"] not in imported] + list(imported.values()),
        key=lambda x: x["id"])
    updated["overrides"] = sorted(
        {**existing_overrides, **new_overrides}.values(), key=lambda x: x["itemId"])
    existing_sets = {(x["key"], x["requiredPieces"]): x for x in reviewed.get("sets", [])}
    for item_set in batch.get("sets", []):
        key = (item_set["key"], item_set["requiredPieces"])
        if key in existing_sets and existing_sets[key] != item_set:
            raise ValueError(f"Conflicting set bonus {key}")
        existing_sets[key] = item_set
    updated["sets"] = sorted(existing_sets.values(), key=lambda x: (x["key"], x["requiredPieces"]))
    digest = hashlib.sha256(canonical(batch)).hexdigest()[:16]
    updated["catalogVersion"] = f"forever-cp175-bulk-{batch['clientBuild']}-{digest}"
    updated["contentVersion"] = f"cp175-approved-db2-{digest}"
    updated["clientBuild"] = "mixed-reviewed"
    updated["sourceName"] = "approved-forever-client-and-published-tooltips"
    updated["sourceUrl"] = batch["sourceUrl"]
    return updated, {"accepted": len(imported), "alreadyPresent": already_present, "rejected": [],
                     "unchanged": canonical(updated) == canonical(reviewed),
                     "batchSha256": hashlib.sha256(canonical(batch)).hexdigest(),
                     "reviewedValuesSha256": report["reviewedValuesSha256"],
                     "clientBuild": batch["clientBuild"]}


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--batch", required=True, help="bulk_normalize.py catalog JSON")
    p.add_argument("--report", required=True, help="matching bulk_normalize.py report JSON")
    p.add_argument("--reviewed", type=Path, default=REVIEWED)
    p.add_argument("--baseline", type=Path, default=BASE)
    p.add_argument("--effects", type=Path, default=EFFECTS)
    p.add_argument("--allow-overrides", action="store_true")
    p.add_argument("--write", action="store_true")
    p.add_argument("--output-report", type=Path)
    args = p.parse_args()
    updated, receipt = promote(load(args.batch), load(args.report),
        load(args.reviewed), load(args.baseline), load(args.effects),
        allow_overrides=args.allow_overrides)
    receipt["catalogIdsBefore"] = len(load(args.reviewed)["items"])
    receipt["catalogIdsAfter"] = len(updated["items"])
    receipt["sha256"] = hashlib.sha256(canonical(updated)).hexdigest()
    if args.output_report:
        args.output_report.write_bytes(canonical(receipt))
    print(json.dumps(receipt, sort_keys=True, indent=2))
    if receipt["rejected"]:
        raise SystemExit(2)
    if args.write:
        args.reviewed.write_bytes(canonical(updated))


if __name__ == "__main__":
    main()
