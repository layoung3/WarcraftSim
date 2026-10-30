#!/usr/bin/env python3
"""Synthetic DB2 -> reviewed snapshot -> CP170 .NET CLI contract test.

No live client data is bundled, and the fixture's values must never ship.
"""
import json
from pathlib import Path
import subprocess
import sys

import bulk_normalize as normalizer
from test_bulk_normalize import BulkNormalizerv1Tests

def main():
    case=BulkNormalizerv1Tests(methodName="test_pinned_join_promotes_reviewed_and_retains_unknowns")
    case.setUp()
    baseline=Path("WarcraftSim.Api/Catalog/forever-equipment-v1.json")
    assert baseline.exists(), "Run from repository root"
    output=case.root/"reviewed-synthetic.json"
    report=case.root/"source-report.json"
    args=case.args(baseline=str(baseline),output=str(output),report=str(report))
    normalizer.normalize(args)
    data=json.loads(output.read_text(encoding="utf-8"))
    assert len(data["items"])==2 and len(data["metadataOnly"])==1
    command=["dotnet","run","--project","tools/ForeverEquipmentImportCli",
             "--configuration","Release","--no-build","--",
             "import",str(baseline),str(output),str(case.root/"resolved.json"),
             str(case.root/"resolved-report.json")]
    subprocess.run(command,check=True)
    result=json.loads((case.root/"resolved.json").read_text(encoding="utf-8"))
    items={x["id"]:x for x in result["items"]}
    assert len(items)==25, f"Expected 24 baseline + 1 new synthetic item, got {len(items)}"
    assert items[19102]["stats"]["spell-power"]==25
    assert items[22504]["stats"]["armor"]==138
    assert items[22504]["effects"][0]["spellId"]==18385
    print("Synthetic joins and two reviewed values passed the CP170 .NET catalog importer; not a live extraction.")
    return 0

if __name__=="__main__":
    sys.exit(main())
