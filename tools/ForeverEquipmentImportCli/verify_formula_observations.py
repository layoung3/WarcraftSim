#!/usr/bin/env python3
"""Review *complete* independently sourced item tooltip observations against
pinned current-build formula candidates. Nonmatching/incomplete items stay
metadata-only. Output is a review proposal, NOT an approval/auto-promotion.
"""
import argparse
import hashlib
import json
from pathlib import Path

def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()

def compare(preview, observations):
    build=preview["clientBuild"]
    candidates={x["id"]:x for x in preview["records"]}
    accepted=[]; results=[];seen=set()
    for observation in observations:
        ident=observation["id"]
        if ident in seen: raise ValueError(f"Duplicate independent observation for {ident}")
        seen.add(ident)
        issues=[]
        if observation.get("clientBuild")!=build:issues.append("different-client-build")
        if not str(observation.get("sourceUrl","")).startswith("https://"):
            issues.append("missing-https-tooltip-source")
        if observation.get("verificationStatus")!="independent-tooltip-reviewed":
            issues.append("unreviewed-source")
        candidate=candidates.get(ident)
        if not candidate:
            issues.append("item-absent-from-source")
            results.append({"id":ident,"status":"quarantined","issues":issues})
            continue
        item=candidate["candidate"]
        if item["unresolved"]:issues+=item["unresolved"]
        if observation.get("uniqueEquipped") is not candidate.get("uniqueEquipped"):
            issues.append("unique-equipped-mismatch-or-missing")
        if observation.get("completeStaticStats") is not True:
            issues.append("partial-tooltip-stat-coverage")
        if observation.get("stats")!=item["stats"]:
            issues.append("combat-stat-mismatch")
        if observation.get("weapon")!=item["weapon"]:
            issues.append("weapon-mismatch-or-incomplete")
        if candidate["classID"]==4 and candidate["slot"] in (1,3,5,6,7,8,9,10,16,20):
            if not isinstance(observation.get("armor"),int) or observation["armor"]!=item["stats"].get("armor"):
                issues.append("armor-not-independently-confirmed")
        elif "armor" in observation and observation["armor"]!=item["stats"].get("armor"):
            issues.append("armor-mismatch")
        for k in ("itemLevel","requiredLevel","quality","inventoryType","classID"):
            sourceVal=candidate[{"itemLevel":"itemLevel","requiredLevel":"requiredLevel",
                                 "quality":"quality","inventoryType":"slot","classID":"classID"}[k]]
            if observation.get(k)!=sourceVal:issues.append(f"{k}-mismatch-or-missing")
        if not issues:
            resolved={
                "id":ident,"status":"complete","clientBuild":build,
                "verificationStatus":"client-db2-reviewed",
                "sourceUrl":observation["sourceUrl"],
                "stats":{k:v for k,v in item["stats"].items() if k!="armor"},
                "inventoryType":candidate["slot"],"classID":candidate["classID"],
                "itemLevel":candidate["itemLevel"],
                "uniqueEquipped":candidate["uniqueEquipped"]
            }
            if "armor" in item["stats"]:resolved["armor"]=item["stats"]["armor"]
            if item["weapon"] is not None:resolved["weapon"]=item["weapon"]
            accepted.append(resolved)
        results.append({"id":ident,"status":"candidate-for-separate-approval" if not issues else "quarantined",
                        "issues":issues,"sourceUrl":observation.get("sourceUrl")})
    return sorted(accepted,key=lambda x:x["id"]),results

def main():
    p=argparse.ArgumentParser()
    p.add_argument("--preview",required=True)
    p.add_argument("--observations",required=True)
    p.add_argument("--resolved-output",required=True)
    p.add_argument("--report",required=True)
    a=p.parse_args()
    preview=json.loads(Path(a.preview).read_text(encoding="utf-8"))
    if preview.get("verificationStatus")!="candidate-unverified-not-for-combat":
        p.error("Expected a non-executable formula preview")
    observations=json.loads(Path(a.observations).read_text(encoding="utf-8"))
    if not isinstance(observations,list):p.error("Observations must be array")
    accepted,results=compare(preview,observations)
    out=Path(a.resolved_output);out.parent.mkdir(parents=True,exist_ok=True)
    out.write_text(json.dumps(accepted,indent=2,sort_keys=True)+"\n",encoding="utf-8")
    report={"clientBuild":preview["clientBuild"],
            "sourceTableSha256":preview["inputTableSha256"],
            "observationSha256":digest(a.observations),
            "resolvedValuesSha256":digest(a.resolved_output),
            "items":results,"matched":len(accepted),
            "quarantined":len(results)-len(accepted),
            "status":"review-proposal-only-not-approved"}
    Path(a.report).write_text(json.dumps(report,indent=2,sort_keys=True)+"\n",encoding="utf-8")
    print(f"Reviewed proposal: {len(accepted)} fully matched, "
          f"{report['quarantined']} quarantined; separate approval still required")
    return 0

if __name__=="__main__":
    raise SystemExit(main())
