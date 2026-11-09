#!/usr/bin/env python3
"""Synthetic DB2 -> reviewed snapshot -> CP170 .NET CLI contract test.

No live client data is bundled, and the fixture's values must never ship.
"""
import argparse
import json
from pathlib import Path
import subprocess
import sys

import bulk_normalize as normalizer
from test_bulk_normalize import BulkNormalizerv1Tests

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--build", action="store_true",
                        help="Explicitly build ForeverEquipmentImportCli -c Release before smoke")
    options=parser.parse_args()
    if options.build:
        subprocess.run(["dotnet","build","tools/ForeverEquipmentImportCli/ForeverEquipmentImportCli.csproj",
                        "--configuration","Release","-warnaserror"],check=True)

    case=BulkNormalizerv1Tests(methodName="test_pinned_join_promotes_reviewed_and_retains_unknowns")
    case.setUp()
    # Deliberately missing synthetic mask must survive as pending metadata.
    case.set_mask("",ids=(20368,))
    baseline=Path("WarcraftSim.Api/Catalog/forever-equipment-v1.json")
    if not baseline.exists():
        raise FileNotFoundError("Run from repository root")
    output=case.root/"reviewed-synthetic.json"
    report=case.root/"source-report.json"
    args=case.args(baseline=str(baseline),output=str(output),report=str(report))
    if normalizer.normalize(args)!=0:
        raise RuntimeError("Bulk-normalizer returned a nonzero exit status")
    data=json.loads(output.read_text(encoding="utf-8"))
    if len(data["items"])!=2 or len(data["metadataOnly"])!=1:
        raise RuntimeError("Bulk normalizer output did not meet smoke contract")
    command=["dotnet","run","--project","tools/ForeverEquipmentImportCli",
             "--configuration","Release","--no-build","--",
             "import",str(baseline),str(output),str(case.root/"resolved.json"),
             str(case.root/"resolved-report.json")]
    result_path=case.root/"resolved.json"
    if result_path.exists():
        result_path.unlink()
    subprocess.run(command,check=True)
    if not result_path.is_file():
        raise RuntimeError(".NET importer did not create the expected catalog")
    result=json.loads((case.root/"resolved.json").read_text(encoding="utf-8"))
    items={x["id"]:x for x in result["items"]}
    if len(items)!=25:
        raise RuntimeError(f"Expected 24 baseline + 1 new synthetic item, got {len(items)}")
    if items[19102]["stats"]["spell-power"]!=25 or items[22504]["stats"]["armor"]!=138 or items[22504]["effects"][0]["spellId"]!=18385:
        raise RuntimeError("Importer produced incorrect stat, armor or linked-effect values")
    source_items={x["id"]:x for x in data["items"]}
    for ident,mask in ((19102,1424),(22504,256)):
        evidence=items[ident].get("classRestrictionEvidence")
        if evidence!=source_items[ident]["classRestrictionEvidence"]:
            raise RuntimeError(f"Importer changed or dropped class-restriction evidence for {ident}")
        if (evidence["rawMask"]!=mask or evidence["status"]!="restricted" or
            evidence["dataConfidence"]!="provisional" or
            evidence["sourceSha256"]!=data["tableSha256"]["ItemSparse"] or
            evidence["sourceAllowedClasses"]!=normalizer.restrict_classes(mask)):
            raise RuntimeError("Synthetic restricted evidence did not retain its reviewed source pin")
    receipt=json.loads((case.root/"resolved-report.json").read_text(encoding="utf-8"))
    metadata={x["id"]:x for x in receipt["metadataOnly"]}
    pending=data["metadataOnly"][0]["classRestrictionEvidence"]
    if (20368 in items or set(metadata)!={20368} or
        metadata[20368].get("classRestrictionEvidence")!=pending or
        pending["status"]!="unknown" or pending["dataConfidence"]!="unknown" or
        pending["rawMask"] is not None or pending["sourceAllowedClasses"]!=[] or
        metadata[20368]["allowedClasses"]!=[]):
        raise RuntimeError("Unknown synthetic eligibility did not stay pending and excluded from combat")
    untouched=[x for ident,x in items.items() if ident not in source_items]
    if not untouched or any(x.get("classRestrictionEvidence") is not None for x in untouched):
        raise RuntimeError("Importer manufactured evidence for untouched legacy baseline records")
    print("Synthetic reviewed receipts round-trip with provisional confidence; unknown eligibility stays metadata-only and legacy records retain no invented evidence. Not live source certification.")
    return 0

if __name__=="__main__":
    sys.exit(main())
