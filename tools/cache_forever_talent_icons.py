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
import urllib.request

DEFAULT_MANIFEST = pathlib.Path("WarcraftSim.Api/Catalog/forever-talent-visual-reference-v1.json")
DEFAULT_DEST = pathlib.Path("WarcraftSim.Api/wwwroot/forever-icons")
MAX_BYTES = 160_000
SOURCE_URL = "https://raw.githubusercontent.com/gunba/wow-forever-sim/1cc5ae1e9abdbf941b7326d2d90421822dce31ba/assets/img/wowhead/icons/large/{key}.jpg"


def icon_keys(manifest):
    document = json.loads(pathlib.Path(manifest).read_text(encoding="utf-8"))
    if document.get("schema") != "warcraftsim-forever-talent-reference-v1":
        raise ValueError("Unsupported visual reference manifest")
    keys = set()
    for entry in document["records"]:
        key = entry.get("iconKey")
        if key and re.fullmatch(r"[a-z0-9_]+", key):
            keys.add(key)
    return sorted(keys)


def download(key, folder, timeout):
    target = folder / (key + ".jpg")
    if target.is_file() and target.stat().st_size > 0:
        return "cached"
    url = SOURCE_URL.format(key=key)
    req = urllib.request.Request(url, headers={"User-Agent": "WarcraftSim-CP174-icon-cache/1.0"})
    with urllib.request.urlopen(req, timeout=timeout) as response:
        if not response.headers.get("Content-Type", "").lower().startswith("image/jpeg"):
            raise ValueError("Unexpected content type for " + key)
        payload = response.read(MAX_BYTES + 1)
    if len(payload) > MAX_BYTES or len(payload) < 100 or not payload.startswith(b"\xff\xd8"):
        raise ValueError("Invalid or oversized JPEG for " + key)
    temp = target.with_suffix(".partial")
    temp.write_bytes(payload)
    temp.replace(target)
    return "downloaded"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", type=pathlib.Path, default=DEFAULT_MANIFEST)
    parser.add_argument("--dest", type=pathlib.Path, default=DEFAULT_DEST)
    parser.add_argument("--timeout", type=float, default=8.0)
    parser.add_argument("--max-icons", type=int, default=200)
    args = parser.parse_args()
    keys = icon_keys(args.manifest)
    if len(keys) > args.max_icons:
        raise ValueError("Unexpectedly many icons: " + str(len(keys)))
    args.dest.mkdir(parents=True, exist_ok=True)
    counts = {"downloaded": 0, "cached": 0, "failed": 0}
    for key in keys:
        try:
            status = download(key, args.dest, args.timeout)
            counts[status] += 1
        except Exception as error:
            counts["failed"] += 1
            print("Unavailable:", key, str(error))
    print("Pinned reference icons:", len(keys), counts)
    if counts["failed"]:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
