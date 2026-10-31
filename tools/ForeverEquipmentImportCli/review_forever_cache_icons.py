#!/usr/bin/env python3
"""Review-only item icon candidate join using a pinned Forever item cache.

Do not modify runtime combat item stats or auto-approve mappings. Requires an
exact item ID AND name match, a safe icon filename and actual cached JPEG.
"""
import argparse
import hashlib
import json
import re
import urllib.request
from pathlib import Path

SOURCE_COMMIT = "c969b8378b63ada9aecfd35db14b373a8aaa66ea"
BASE = ("https://raw.githubusercontent.com/Napalmsteak/"
        "WoW-Classic-Item-Caches/" + SOURCE_COMMIT + "/")
CACHE_URL = BASE + "Forever%20Cache.json"
MAX_CACHE_BYTES = 24_000_000
MAX_ICON_BYTES = 100_000


def read_bytes(url, limit):
    request = urllib.request.Request(url, headers={"User-Agent": "WarcraftSim-CP174-source-census/1.0"})
    with urllib.request.urlopen(request, timeout=45) as response:
        data = response.read(limit + 1)
    if not data or len(data) > limit:
        raise ValueError("Source outside approved byte bounds")
    return data


def analyze(catalog, source):
    entries = source if isinstance(source, list) else source.get("items", []) if isinstance(source, dict) else None
    if not isinstance(entries, list):
        raise ValueError("Expected Forever cache item array")
    selected = {int(x["id"]): x for x in catalog["items"]}
    by_id = {}
    for item in entries:
        if not isinstance(item, dict):
            continue
        item_id = item.get("itemId")
        if not isinstance(item_id, int) or item_id not in selected:
            continue
        by_id.setdefault(item_id, []).append(item)
    matches, unresolved, ambiguous = [], [], []
    for item_id, reviewed in sorted(selected.items()):
        found = [x for x in by_id.get(item_id, []) if
                 x.get("name", "").strip().casefold() == reviewed["name"].strip().casefold() and
                 x.get("itemExpansion") == "Forever" and
                 isinstance(x.get("iconName"), str) and
                 re.fullmatch(r"[a-z0-9_]+", x["iconName"])]
        keys = sorted(set(x["iconName"] for x in found))
        if len(keys) == 1:
            matches.append({"itemId": item_id, "itemName": reviewed["name"],
                            "iconKey": keys[0], "status": "source-backed-forever-beta-provisional"})
        elif len(keys) > 1:
            ambiguous.append({"itemId": item_id, "iconKeys": keys})
        else:
            unresolved.append({"itemId": item_id, "name": reviewed["name"]})
    return {"candidates": matches, "ambiguous": ambiguous, "unresolved": unresolved}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--catalog", type=Path, default=Path("WarcraftSim.Api/Catalog/forever-equipment-v1.json"))
    ap.add_argument("--report", type=Path, default=Path("/tmp/cp174-forever-item-icons.json"))
    ap.add_argument("--image-dir", type=Path)
    args = ap.parse_args()
    catalog = json.loads(args.catalog.read_text(encoding="utf-8"))
    payload = read_bytes(CACHE_URL, MAX_CACHE_BYTES)
    source = json.loads(payload)
    outcome = analyze(catalog, source)
    report = {
        "schema": "cp174-exact-id-and-name-icon-candidates-v1",
        "sourceRepository": "Napalmsteak/WoW-Classic-Item-Caches",
        "sourceCommit": SOURCE_COMMIT,
        "cacheSha256": hashlib.sha256(payload).hexdigest(),
        "sourceStatus": "Forever beta snapshot; not final 70707 independently verified",
        **outcome,
    }
    if args.image_dir:
        args.image_dir.mkdir(parents=True, exist_ok=True)
        successes = []
        for item in outcome["candidates"]:
            name = item["iconKey"]
            target = args.image_dir / (name + ".jpg")
            if not target.exists():
                try:
                    raw = read_bytes(BASE + "icons/" + name + ".jpg", MAX_ICON_BYTES)
                    if not raw.startswith(b"\xff\xd8"):
                        raise ValueError("Not a JPEG")
                    target.write_bytes(raw)
                except Exception as err:
                    item["imageError"] = str(err)
                    continue
            successes.append(name)
        report["cachedIcons"] = sorted(set(successes))
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print("CP174 item icon candidates:", len(outcome["candidates"]),
          "ambiguous:", len(outcome["ambiguous"]),
          "unresolved:", len(outcome["unresolved"]))
    print("Candidate report (no automatic runtime promotion):", args.report)


if __name__ == "__main__":
    main()
