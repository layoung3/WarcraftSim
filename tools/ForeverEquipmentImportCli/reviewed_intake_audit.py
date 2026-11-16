#!/usr/bin/env python3
"""CP175-F1B offline reviewed-equipment intake audit (no network or invented stats).

Run from repository root:
  python tools/ForeverEquipmentImportCli/reviewed_intake_audit.py

For new client candidates, use CP171's download_client_csv.py and bulk_normalize.py
with checksum-bound resolved values and a reviewer approval, then stage only
independently verified numeric values in the reviewed catalog. This audit
checks catalog ownership and provenance before allowing a new PR commit.
"""
import collections
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CAT = ROOT / "WarcraftSim.Api" / "Catalog"
BASE = CAT / "forever-equipment-v1.json"
REVIEWED = CAT / "forever-equipment-cp171-reviewed.json"
EFFECTS = CAT / "forever-equipment-cp172-effects.json"

def audit():
    sets = [(p.name, json.loads(p.read_text(encoding="utf-8")).get("items", []))
            for p in (BASE, REVIEWED, EFFECTS)]
    reviewed_doc = json.loads(REVIEWED.read_text(encoding="utf-8"))
    approved_overrides = {row["itemId"] for row in reviewed_doc.get("overrides", [])
                          if row.get("reason") and str(row.get("sourceUrl", "")).startswith("https://")}
    seen = {}
    rejected = []
    count_by_slot = collections.Counter()
    for source, items in sets:
        for item in items:
            ident = item.get("id")
            reasons = []
            if not isinstance(ident, int) or ident <= 0:
                reasons.append("missing-or-invalid-id")
            if ident in seen and not (source == REVIEWED.name and
                                       seen[ident] == BASE.name and ident in approved_overrides):
                reasons.append("duplicate-id-with-" + seen[ident])
            if not item.get("name") or not item.get("allowedSlots"):
                reasons.append("missing-name-or-slot")
            if not item.get("stats") and not item.get("weapon") and not item.get("effects"):
                reasons.append("no-combat-data")
            if source == REVIEWED.name:
                if not item.get("sourceUrl", "").startswith("https://"):
                    reasons.append("unattributed-source")
                if not item.get("sourceBuild") or not item.get("verificationStatus"):
                    reasons.append("missing-build-or-verification")
                if item.get("weapon"):
                    w = item["weapon"]
                    if not (w.get("minimumDamage") is not None and
                            w.get("maximumDamage") is not None and
                            (w.get("speedSeconds") or 0) > 0):
                        reasons.append("unverified-weapon-values")
            if reasons:
                rejected.append({"id": ident, "source": source, "reasons": reasons})
            else:
                seen[ident] = source
                for slot in item["allowedSlots"]:
                    count_by_slot[slot] += 1
    print(json.dumps({"totalRegisteredIds": len(seen), "bySlot": dict(sorted(count_by_slot.items())),
                      "rejected": rejected}, indent=2))
    return 1 if rejected else 0

if __name__ == "__main__":
    raise SystemExit(audit())
