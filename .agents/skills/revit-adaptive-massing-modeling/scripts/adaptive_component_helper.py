"""
AIGRAV Adaptive Component & Massing Helper
Module for querying, placing, and validating adaptive components in Revit.
"""

import json
import urllib.request
from typing import List, Tuple, Dict, Any, Optional

AIGRAV_URL = "http://127.0.0.1:8765/"

def call_aigrav(tool: str, argument: str = "") -> Dict[str, Any]:
    payload = {"tool": tool, "argument": argument}
    data = json.dumps(payload).encode("utf-8")
    req = urllib.request.Request(
        AIGRAV_URL,
        data=data,
        headers={"Content-Type": "application/json"},
        method="POST"
    )
    with urllib.request.urlopen(req, timeout=30) as resp:
        return json.loads(resp.read().decode("utf-8"))

def build_adaptive_placement_python_script(
    family_name: str,
    type_name: Optional[str],
    instances_points_mm: List[List[Tuple[float, float, float]]]
) -> str:
    """
    Generates a bulletproof Python script to execute in Revit Safe Sandbox via aigrav_execute_revit_python.
    """
    type_filter = f"fs.Name == '{type_name}'" if type_name else "True"
    script = f"""
import clr
clr.AddReference('RevitAPI')
from Autodesk.Revit.DB import (
    XYZ, Transaction, AdaptiveComponentInstanceUtils, 
    ReferencePoint, FilteredElementCollector, FamilySymbol
)

doc = __revit__.ActiveUIDocument.Document
collector = FilteredElementCollector(doc).OfClass(FamilySymbol)

symbol = None
for fs in collector:
    if fs.FamilyName.lower() == '{family_name}'.lower():
        if {type_filter}:
            if AdaptiveComponentInstanceUtils.IsAdaptiveFamilySymbol(fs):
                symbol = fs
                break

if not symbol:
    raise Exception("Adaptive family symbol '{family_name}' not found or is not adaptive.")

tx = Transaction(doc, "AIGRAV - Place Adaptive Components")
tx.Start()
try:
    if not symbol.IsActive:
        symbol.Activate()
        doc.Regenerate()

    MM_TO_FT = 1.0 / 304.8
    created_ids = []
    points_data = {json.dumps(instances_points_mm)}

    for pts in points_data:
        inst = AdaptiveComponentInstanceUtils.CreateAdaptiveComponentInstance(doc, symbol)
        p_ids = AdaptiveComponentInstanceUtils.GetInstancePlacementPointElementRefIds(inst)
        if len(p_ids) != len(pts):
            raise Exception("Point count mismatch: expected " + str(len(p_ids)) + " got " + str(len(pts)))
        
        for i, pid in enumerate(p_ids):
            rp = doc.GetElement(pid)
            rp.Position = XYZ(pts[i][0] * MM_TO_FT, pts[i][1] * MM_TO_FT, pts[i][2] * MM_TO_FT)
        created_ids.append(inst.Id.Value)

    doc.Regenerate()
    tx.Commit()
    print("SUCCESS: Created " + str(len(created_ids)) + " adaptive instances: " + str(created_ids))
except Exception as e:
    tx.RollBack()
    print("ERROR: " + str(e))
"""
    return script

def execute_adaptive_placement(
    family_name: str,
    type_name: Optional[str],
    instances_points_mm: List[List[Tuple[float, float, float]]]
) -> Dict[str, Any]:
    script = build_adaptive_placement_python_script(family_name, type_name, instances_points_mm)
    return call_aigrav("aigrav_execute_revit_python", script)
