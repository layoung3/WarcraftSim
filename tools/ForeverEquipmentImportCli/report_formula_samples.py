#!/usr/bin/env python3
"""Compare unverified current-build DB2 formula previews to prior published
Forever tooltip observations. NEVER convert matches into approval.
"""
import argparse
import json

def main():
    p=argparse.ArgumentParser()
    p.add_argument("--preview",required=True)
    p.add_argument("--reviewed",required=True)
    p.add_argument("--ids",nargs="*",type=int,default=[
        19102,22504,22496,22436,20368,16622,14152,19682,23062,22511,22498])
    a=p.parse_args()
    current=json.load(open(a.preview,encoding="utf-8"))
    historic=json.load(open(a.reviewed,encoding="utf-8"))
    known={x["id"]:x for x in historic["items"]}
    previews={x["id"]:x for x in current["records"]}
    for id in a.ids:
        preview=previews.get(id)
        old=known.get(id)
        if preview is None:
            print(json.dumps({"id":id,"status":"absent-from-client-output"}))
            continue
        calc=preview["candidate"]
        if old:
            stat_diff={key:{"candidate":calc["stats"].get(key),"historical":value}
                       for key,value in old["stats"].items()
                       if calc["stats"].get(key)!=value}
            weapon_diff=None
            if old.get("weapon"):
                weapon_diff={k:{"candidate":(calc["weapon"] or {}).get(k),
                                "historical":v}
                             for k,v in old["weapon"].items()
                             if (calc["weapon"] or {}).get(k)!=v}
            result={"id":id,"name":old["name"],"requestedBuild":current["clientBuild"],
                    "historicalSourceBuild":old["sourceBuild"],
                    "candidateStats":calc["stats"],"statDiscrepancies":stat_diff,
                    "candidateWeapon":calc["weapon"],"weaponDiscrepancies":weapon_diff,
                    "unresolved":calc["unresolved"],
                    "status":"HISTORICAL_COMPARISON_ONLY_NOT_CURRENT_BUILD_VERIFIED"}
        else:
            result={"id":id,"candidate":calc,"status":"NO_PINNED_TOOLTIP_REFERENCE"}
        print(json.dumps(result,sort_keys=True))
    return 0

if __name__=="__main__":
    raise SystemExit(main())
