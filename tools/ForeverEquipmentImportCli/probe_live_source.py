#!/usr/bin/env python3
"""One-request public CSV availability probe. Never persists raw client data."""
import argparse
import csv
import hashlib
import json
import urllib.error
import urllib.request

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument("--build",required=True)
    parser.add_argument("--table",default="ItemSparse")
    args=parser.parse_args()
    if args.build!="1.60.1.70707" or args.table not in ("ItemSparse","Item"):
        parser.error("Probe restricted to pinned 1.60.1.70707 Item/ItemSparse")
    url=f"https://wago.tools/db2/{args.table}/csv?build={args.build}&locale=enUS"
    req=urllib.request.Request(url,headers={
        "Accept":"text/csv",
        "User-Agent":"WarcraftSim/CP171 (single bounded source availability check)"
    })
    try:
        with urllib.request.urlopen(req,timeout=18) as response:
            headers={k.lower():v for k,v in response.headers.items()}
            size=0
            digest=hashlib.sha256()
            first=b""
            while True:
                chunk=response.read(65536)
                if not chunk:
                    break
                if not first:
                    first=chunk.split(b"\n",1)[0][:4096]
                size+=len(chunk)
                if size>32*1024*1024:
                    raise ValueError("Response exceeded 32 MiB probe limit")
                digest.update(chunk)
            fields=next(csv.reader([first.decode("utf-8-sig","replace")])) if first else []
            if response.status!=200 or not "ID" in fields or len(fields)<3:
                raise ValueError(f"Response not a recognized DB2 CSV (status={response.status}, columns={fields[:4]})")
            print(json.dumps({
                "provider":"wago.tools","requestedBuild":args.build,
                "actualBuild":"not attested by response",
                "table":args.table,"url":url,"httpStatus":response.status,
                "contentType":headers.get("content-type"),
                "headers":fields,"sha256":digest.hexdigest(),"bytes":size
            },indent=2))
        return 0
    except (urllib.error.URLError,ValueError,TimeoutError) as exc:
        print(json.dumps({"provider":"wago.tools","requestedBuild":args.build,
                          "table":args.table,"url":url,"status":"unavailable",
                          "errorType":type(exc).__name__,"reason":str(exc)},indent=2))
        return 2

if __name__=="__main__":
    raise SystemExit(main())
