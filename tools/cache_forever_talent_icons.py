#!/usr/bin/env python3
"""Populate a local cache of exact source-keyed WoW talent icons.

Only visual reference icon keys from the pinned CP174 manifest are accepted.
Art belongs to Blizzard; the application itself makes no external requests.
Run explicitly in a development environment that is allowed to access the CDN.
"""
import argparse
import json
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from acquisition_cache import acquire, validate_jpeg

DEFAULT_MANIFEST = pathlib.Path("WarcraftSim.Api/Catalog/forever-talent-visual-reference-v1.json")
DEFAULT_DEST = pathlib.Path("WarcraftSim.Api/wwwroot/forever-icons")
MAX_BYTES = 160_000
SOURCE_COMMIT = "1cc5ae1e9abdbf941b7326d2d90421822dce31ba"
SOURCE_URL = "https://raw.githubusercontent.com/gunba/wow-forever-sim/" + SOURCE_COMMIT + "/assets/img/wowhead/icons/large/{key}.jpg"
ITEM_SOURCE_COMMIT = "c969b8378b63ada9aecfd35db14b373a8aaa66ea"
ITEM_SOURCE_URL = "https://raw.githubusercontent.com/Napalmsteak/WoW-Classic-Item-Caches/" + ITEM_SOURCE_COMMIT + "/icons/{key}.jpg"


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError("Duplicate visual reference property: " + key)
        result[key] = value
    return result


def artwork_source(key, source=None):
    pinned = [{"url": url.format(key=key), "sourceVersion": commit}
              for commit, url in ((SOURCE_COMMIT, SOURCE_URL), (ITEM_SOURCE_COMMIT, ITEM_SOURCE_URL))]
    if source is None:
        return pinned[0]
    if not isinstance(source, dict) or source not in pinned:
        raise ValueError("Unknown or conflicting pinned artwork source for " + key)
    return source


def icon_sources(manifest):
    document = json.loads(pathlib.Path(manifest).read_text(encoding="utf-8"), object_pairs_hook=unique_object)
    if document.get("schema") != "warcraftsim-forever-talent-reference-v1":
        raise ValueError("Unsupported visual reference manifest")
    if document.get("sourceCommit", SOURCE_COMMIT) != SOURCE_COMMIT:
        raise ValueError("Visual reference source version does not match the pinned artwork source")
    sources = {}
    for entry in document["records"]:
        key = entry.get("iconKey")
        if key and re.fullmatch(r"[a-z0-9_]+", key):
            if "artworkSource" in entry and entry["artworkSource"] is None:
                raise ValueError("Unknown pinned artwork source for " + key)
            source = artwork_source(key, entry.get("artworkSource"))
            if key in sources and sources[key] != source:
                raise ValueError("Conflicting artwork sources for icon " + key)
            sources[key] = source
    return dict(sorted(sources.items()))


def icon_keys(manifest):
    return list(icon_sources(manifest))


def download(key, folder, timeout, *, refresh=False, revalidate_legacy=False, attempts=3,
             source=None):
    if not isinstance(key, str) or not re.fullmatch(r"[a-z0-9_]+", key):
        raise ValueError("Unsafe icon key")
    provenance = {**artwork_source(key, source),
                  "kind": "icon", "iconKey": key,
                  "sourceStatus": "historical-reference-not-current-build-verified"}
    _, _, status = acquire(pathlib.Path(folder) / (key + ".jpg"), provenance, validate_jpeg,
        MAX_BYTES, timeout=timeout, refresh=refresh, revalidate_legacy=revalidate_legacy,
        attempts=attempts, headers={"Accept": "image/jpeg"})
    # Preserve the callers' original two-success-status contract.
    return "downloaded" if status in ("downloaded", "revalidated") else "cached"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", type=pathlib.Path, default=DEFAULT_MANIFEST)
    parser.add_argument("--dest", type=pathlib.Path, default=DEFAULT_DEST)
    parser.add_argument("--timeout", type=float, default=8.0)
    parser.add_argument("--max-icons", type=int, default=200)
    parser.add_argument("--attempts", type=int, default=3)
    parser.add_argument("--refresh", action="store_true", help="Revalidate immutable cached bytes; changed bytes fail")
    parser.add_argument("--revalidate-legacy", action="store_true",
                        help="Receipt an unreceipted file only after an exact fresh pinned-source byte match")
    parser.add_argument("--existing-only", action="store_true", help="Never acquire artwork for missing local files")
    args = parser.parse_args()
    sources = icon_sources(args.manifest)
    keys = list(sources)
    if len(keys) > args.max_icons:
        raise ValueError("Unexpectedly many icons: " + str(len(keys)))
    if args.existing_only:
        keys = [key for key in keys if (args.dest / (key + ".jpg")).is_file()]
    args.dest.mkdir(parents=True, exist_ok=True)
    counts = {"downloaded": 0, "cached": 0, "failed": 0}
    for key in keys:
        try:
            status = download(key, args.dest, args.timeout, refresh=args.refresh,
                              revalidate_legacy=args.revalidate_legacy, attempts=args.attempts,
                              source=sources[key])
            counts[status] += 1
        except Exception as error:
            counts["failed"] += 1
            print("Unavailable:", key, str(error))
    print("Pinned reference icons:", len(keys), counts)
    if counts["failed"]:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
