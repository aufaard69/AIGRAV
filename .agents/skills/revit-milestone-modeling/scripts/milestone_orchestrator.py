"""
AIGRAV Milestone Orchestrator Engine
Facilitates the execution and validation of Macro-Milestones in Autodesk Revit.
"""

import json
import urllib.request
from typing import Dict, Any, List

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
    with urllib.request.urlopen(req, timeout=45) as resp:
        return json.loads(resp.read().decode("utf-8"))

class MilestoneOrchestrator:
    def __init__(self, project_name: str):
        self.project_name = project_name
        self.current_milestone = 0
        self.milestone_history = []

    def run_milestone(self, milestone_num: int, milestone_name: str, batch_script: str) -> Dict[str, Any]:
        """
        Executes a macro-milestone inside Revit Safe Sandbox under a named TransactionGroup.
        """
        wrapped_script = f"""
# AIGRAV Milestone {milestone_num}: {milestone_name}
import clr
clr.AddReference('RevitAPI')
from Autodesk.Revit.DB import TransactionGroup

doc = __revit__.ActiveUIDocument.Document
tg = TransactionGroup(doc, "AIGRAV M{milestone_num} - {milestone_name}")
tg.Start()

try:
{batch_script}
    tg.Assimilate()
    print("SUCCESS: Milestone {milestone_num} completed and assimilated cleanly.")
except Exception as e:
    tg.RollBack()
    print("FAILED: Milestone {milestone_num} rolled back cleanly. Error: " + str(e))
    raise e
"""
        exec_res = call_aigrav("aigrav_execute_revit_python", wrapped_script)
        
        # Immediate Viewport Capture
        viewport_res = call_aigrav("aigrav_capture_revit_viewport", "")
        
        record = {
            "milestone": milestone_num,
            "name": milestone_name,
            "execution": exec_res,
            "viewport": viewport_res
        }
        self.milestone_history.append(record)
        self.current_milestone = milestone_num
        return record
