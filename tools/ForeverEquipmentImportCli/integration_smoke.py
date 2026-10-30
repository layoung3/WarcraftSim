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
    print("Synthetic joins and two reviewed values passed the CP170 .NET catalog importer; not a live extraction.")
    return 0

if __name__=="__main__":
    sys.exit(main())
