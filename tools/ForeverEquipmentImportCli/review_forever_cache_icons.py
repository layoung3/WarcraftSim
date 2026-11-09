#!/usr/bin/env python3
"""Review-only item icon candidate join using a pinned Forever item cache.

Do not modify runtime combat item stats or auto-approve mappings. Requires an
exact item ID AND name match, a safe icon filename and actual cached JPEG.
"""
import argparse
import hashlib
import json
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from acquisition_cache import acquire, atomic_write, bounded_file, digest, json_bytes, request_bytes, validate_jpeg

SOURCE_COMMIT = "c969b8378b63ada9aecfd35db14b373a8aaa66ea"
BASE = ("https://raw.githubusercontent.com/Napalmsteak/"
        "WoW-Classic-Item-Caches/" + SOURCE_COMMIT + "/")
CACHE_URL = BASE + "Forever%20Cache.json"
# Existing reviewed equipment-icon manifest pins this metadata checksum.
CACHE_SHA256 = "5d9a6461d2a7adbc4e3d765f19fbeb6adb37c4c53161bb89e4d7b770f805ca32"
MAX_CACHE_BYTES = 24_000_000
MAX_ICON_BYTES = 100_000


def read_bytes(url, limit):
    data, _ = request_bytes(url, limit)
    if data is None:
        raise ValueError("HTTP 304 without a verified local cache")
    return data


def cache_icon(name, folder, *, refresh=False, revalidate_legacy=False):
    if not isinstance(name, str) or not re.fullmatch(r"[a-z0-9_]+", name):
        raise ValueError("Unsafe icon key")
    source = {"url": BASE + "icons/" + name + ".jpg", "sourceVersion": SOURCE_COMMIT,
              "kind": "icon", "iconKey": name,
              "sourceStatus": "historical-reference-not-current-build-verified"}
    return acquire(Path(folder) / (name + ".jpg"), source, validate_jpeg, MAX_ICON_BYTES,
                   refresh=refresh, revalidate_legacy=revalidate_legacy,
                   headers={"Accept": "image/jpeg"})[2]


def validate_source_cache(payload):
    if digest(payload) != CACHE_SHA256:
        raise ValueError("Pinned Forever metadata checksum mismatch; historical source cannot be repinned")
    json.loads(payload)


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
    ap.add_argument("--source-cache", type=Path, help="Optional immutable pinned metadata cache")
    ap.add_argument("--refresh", action="store_true", help="Revalidate bytes without repinning changes")
    ap.add_argument("--revalidate-legacy", action="store_true", help="Receipt legacy images only after an exact fresh source match")
    ap.add_argument("--existing-only", action="store_true", help="Never acquire artwork for missing local files")
    ap.add_argument("--max-icons", type=int, default=200)
    args = ap.parse_args()
    catalog = json.loads(args.catalog.read_text(encoding="utf-8"))
    source_cache = args.source_cache or args.report.parent / ("forever-cache-" + SOURCE_COMMIT + ".json")
    payload, _, cache_status = acquire(source_cache,
        {"url": CACHE_URL, "sourceVersion": SOURCE_COMMIT, "kind": "item-icon-metadata"},
        validate_source_cache, MAX_CACHE_BYTES, refresh=args.refresh, revalidate_legacy=args.revalidate_legacy)
    source = json.loads(payload)
    outcome = analyze(catalog, source)
    report = {
        "schema": "cp174-exact-id-and-name-icon-candidates-v1",
        "sourceRepository": "Napalmsteak/WoW-Classic-Item-Caches",
        "sourceCommit": SOURCE_COMMIT,
        "cacheSha256": hashlib.sha256(payload).hexdigest(),
        "sourceStatus": "Forever beta snapshot; not final 70707 independently verified",
        "cacheStatus": cache_status,
        "artworkNotice": "Blizzard artwork; no redistribution license is asserted. Optional local cache only where permitted.",
        **outcome,
    }
    if args.image_dir:
        if len({item["iconKey"] for item in outcome["candidates"]}) > args.max_icons:
            raise ValueError("Too many candidate images for the bounded cache run")
        args.image_dir.mkdir(parents=True, exist_ok=True)
        successes = []
        for item in outcome["candidates"]:
            name = item["iconKey"]
            if args.existing_only and not (args.image_dir / (name + ".jpg")).is_file():
                item["imageStatus"] = "unavailable-use-neutral-fallback"
                continue
            try:
                item["imageCacheStatus"] = cache_icon(name, args.image_dir, refresh=args.refresh,
                                                      revalidate_legacy=args.revalidate_legacy)
            except Exception as err:
                item["imageError"] = str(err)
                item["imageStatus"] = "unavailable-use-neutral-fallback"
                continue
            item["imageStatus"] = "validated-local-cache"
            successes.append(name)
        report["cachedIcons"] = sorted(set(successes))
    args.report.parent.mkdir(parents=True, exist_ok=True)
    if args.report.exists():
        previous = bounded_file(args.report, MAX_CACHE_BYTES)
        archived = args.report.parent / (args.report.name + ".history") / (digest(previous) + ".json")
        if not archived.exists():
            atomic_write(archived, previous)
    atomic_write(args.report, json_bytes(report))
    print("CP174 item icon candidates:", len(outcome["candidates"]),
          "ambiguous:", len(outcome["ambiguous"]),
          "unresolved:", len(outcome["unresolved"]))
    print("Candidate report (no automatic runtime promotion):", args.report)


if __name__ == "__main__":
    main()
