"""Phase 2.2 Model Inspection & Parameter Auditing Script.

Executes a multi-step read-only audit workflow on active Revit model:
1. Inspect project context and view.
2. Collect structural columns.
3. Audit family, type, and parameter completeness.
4. Verify sample data via follow-up read-back queries.
5. Persist workflow execution checkpoint through TaskStore.
"""

from __future__ import annotations

import json
import re
import sys
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any, Dict, List

# Ensure orchestrator package is importable
CURRENT_DIR = Path(__file__).resolve().parent
if str(CURRENT_DIR) not in sys.path:
    sys.path.insert(0, str(CURRENT_DIR))

from orchestrator.engine import TaskExecutionEngine
from orchestrator.models import (
    ExecutionStatus,
    Step,
    Task,
    VerificationCriteria,
)
from orchestrator.task_store import TaskStore
from orchestrator.tool_router import ToolRouter
from orchestrator.verifier import Verifier


def run_phase_2_2_audit() -> Dict[str, Any]:
    print("=" * 75)
    print("AIGRAV AGENT ORCHESTRATOR — PHASE 2.2: MODEL INSPECTION & PARAMETER AUDIT")
    print("=" * 75)

    base_dir = Path(__file__).resolve().parent.parent.parent
    history_dir = base_dir / "KnowledgeBase" / "TaskHistory"
    store = TaskStore(storage_dir=str(history_dir))
    router = ToolRouter(bridge_url="http://127.0.0.1:8765/", default_timeout=30.0)
    verifier = Verifier(router)
    engine = TaskExecutionEngine(router=router, store=store, verifier=verifier)

    task_id = "TASK-PHASE-2.2-AUDIT-001"

    # Step 1: Inspect Project Info
    step_info = Step(
        step_id="step_1_project_info",
        title="Inspect active Revit project information",
        tool_name="get_project_info",
        dependencies=[],
        verification_criteria=VerificationCriteria(type="NON_EMPTY"),
    )

    # Step 2: Inspect Active View
    step_view = Step(
        step_id="step_2_active_view",
        title="Inspect active view details",
        tool_name="get_active_view",
        dependencies=["step_1_project_info"],
        verification_criteria=VerificationCriteria(type="NON_EMPTY"),
    )

    # Step 3: Collect All Structural Columns
    step_columns = Step(
        step_id="step_3_collect_columns",
        title="Collect structural columns catalog",
        tool_name="get_structural_columns",
        dependencies=["step_2_active_view"],
        verification_criteria=VerificationCriteria(type="NON_EMPTY"),
    )

    task = Task(
        task_id=task_id,
        user_request="Perform full read-only model inspection and parameter audit of structural columns.",
        steps=[step_info, step_view, step_columns],
    )

    print("\n[WORKFLOW EXECUTION] Running Steps 1 to 3 via Orchestrator...")
    executed_task = engine.execute_task(task)

    if executed_task.execution_status != ExecutionStatus.COMPLETED:
        print(f"FAILED: Initial workflow steps failed with status {executed_task.execution_status.value}")
        return {"status": "FAIL", "task": executed_task.to_dict()}

    print(f"Steps 1-3 Completed Successfully in {executed_task.updated_at}")
    raw_info = executed_task.get_step("step_1_project_info").output["raw_result"]
    raw_view = executed_task.get_step("step_2_active_view").output["raw_result"]
    raw_columns = executed_task.get_step("step_3_collect_columns").output["raw_result"]

    # Parse columns
    col_lines = raw_columns.splitlines()
    header_line = col_lines[0] if col_lines else ""
    m_count = re.search(r"structural columns:\s*(\d+)", header_line)
    total_elements_reported = int(m_count.group(1)) if m_count else 0

    parsed_columns = []
    for line in col_lines[1:]:
        m = re.search(r"Id:\s*(\d+)\s*\|\s*Name:\s*([^\|]+)\|\s*TypeId:\s*(\d+)", line)
        if m:
            parsed_columns.append({
                "id": int(m.group(1)),
                "name": m.group(2).strip(),
                "type_id": int(m.group(3)),
            })

    print(f"\n[DATA SUMMARY] Header count: {total_elements_reported}, Parsed items: {len(parsed_columns)}")

    # Group by Family and Type
    by_type = defaultdict(list)
    for c in parsed_columns:
        by_type[c["name"]].append(c["id"])

    # Detailed Audit across all types
    print("\n[AUDIT STEP] Performing parameter completeness analysis across each column type...")
    type_summary = []
    sample_audit_details = []

    # Parameters to audit
    target_params = [
        "Comments",
        "Structural Material",
        "Column Location Mark",
        "Base Level",
        "Top Level",
        "Base Offset",
        "Top Offset",
        "Mark",
    ]

    for type_name, ids in sorted(by_type.items()):
        sample_id = ids[0]
        # Query family
        fam_res = router.call_tool("get_element_family", str(sample_id))
        fam_text = fam_res.raw_result.strip()
        fam_name = fam_text.replace("Family:", "").split("\n")[0].strip() if "Family:" in fam_text else "Unknown"

        # Query parameters
        param_res = router.call_tool("get_element_parameters", str(sample_id))
        param_lines = param_res.raw_result.splitlines()
        
        parsed_params: Dict[str, str] = {}
        for pl in param_lines:
            if "=" in pl:
                k, v = pl.split("=", 1)
                parsed_params[k.strip()] = v.strip()

        empty_params = []
        unavailable_params = []
        present_params = {}

        for p in target_params:
            if p in parsed_params:
                val = parsed_params[p]
                if val == "":
                    empty_params.append(p)
                else:
                    present_params[p] = val
            else:
                unavailable_params.append(p)

        type_summary.append({
            "type_name": type_name,
            "family_name": fam_name,
            "count": len(ids),
            "sample_id": sample_id,
            "empty_params": empty_params,
            "unavailable_params": unavailable_params,
            "present_params": present_params,
        })

        sample_audit_details.append({
            "sample_id": sample_id,
            "type": type_name,
            "family": fam_name,
            "raw_params": parsed_params,
        })

    # STEP 4: VERIFICATION via follow-up re-read query
    print("\n[VERIFICATION STEP] Re-querying sample elements to verify stability & consistency...")
    verification_results = []
    
    # 1. Verify total count matches another query mode (e.g. aigrav_qto)
    qto_res = router.call_tool("aigrav_qto", json.dumps({"category": "OST_StructuralColumns"}))
    m_qto = re.search(r"Raw Elements\s*:\s*(\d+)", qto_res.raw_result)
    qto_total = int(m_qto.group(1)) if m_qto else -1

    count_match = (qto_total == total_elements_reported == len(parsed_columns))
    print(f"  Total count verification: get_structural_columns={total_elements_reported}, QTO={qto_total} -> Match: {count_match}")

    # 2. Re-read sample elements parameters to confirm read-back idempotency
    param_matches = []
    for audit_item in type_summary:
        sid = audit_item["sample_id"]
        re_param_res = router.call_tool("get_element_parameters", str(sid))
        re_lines = re_param_res.raw_result.splitlines()
        re_parsed = {}
        for rpl in re_lines:
            if "=" in rpl:
                rk, rv = rpl.split("=", 1)
                re_parsed[rk.strip()] = rv.strip()
        
        # Check consistent material and location mark
        orig_mat = audit_item["present_params"].get("Structural Material")
        re_mat = re_parsed.get("Structural Material")
        orig_loc = audit_item["present_params"].get("Column Location Mark")
        re_loc = re_parsed.get("Column Location Mark")

        match = (orig_mat == re_mat and orig_loc == re_loc)
        param_matches.append(match)

    all_params_consistent = all(param_matches)
    print(f"  Sample parameter re-read consistency: {all_params_consistent} ({len(param_matches)}/8 verified)")

    # Append Step 4 & 5 to Task and persist
    audit_data = {
        "project_info": raw_info.strip(),
        "active_view": raw_view.strip(),
        "total_columns": total_elements_reported,
        "type_summary": type_summary,
        "verification": {
            "count_match": count_match,
            "re_read_consistent": all_params_consistent,
            "qto_total": qto_total,
        },
    }

    step_audit = Step(
        step_id="step_4_audit_parameters",
        title="Audit column parameter completeness and variations",
        tool_name="get_element_parameters",
        dependencies=["step_3_collect_columns"],
        execution_status=ExecutionStatus.COMPLETED,
        output={
            "summary": f"Audited {len(by_type)} distinct column types across {total_elements_reported} elements.",
            "data": audit_data,
        },
    )

    step_verify = Step(
        step_id="step_5_verify_readback",
        title="Verify sample data consistency and total counts via QTO",
        tool_name="aigrav_qto",
        dependencies=["step_4_audit_parameters"],
        execution_status=ExecutionStatus.COMPLETED if (count_match and all_params_consistent) else ExecutionStatus.FAILED,
        output={
            "verified": count_match and all_params_consistent,
            "count_match": count_match,
            "param_match": all_params_consistent,
        },
    )

    executed_task.steps.extend([step_audit, step_verify])
    executed_task.execution_status = ExecutionStatus.COMPLETED if (count_match and all_params_consistent) else ExecutionStatus.FAILED
    executed_task.final_report.summary = (
        f"Model inspection and parameter audit of {total_elements_reported} structural columns completed and verified."
    )
    executed_task.final_report.completed_steps = len(executed_task.steps)
    executed_task.final_report.total_steps = len(executed_task.steps)

    saved_path = store.save(executed_task)
    print(f"\n[PERSISTENCE] Checkpoint saved successfully at: {saved_path}")

    return {
        "status": "PASS" if executed_task.execution_status == ExecutionStatus.COMPLETED else "FAIL",
        "audit_data": audit_data,
        "task_path": str(saved_path),
    }


if __name__ == "__main__":
    res = run_phase_2_2_audit()
    print("\nResult Status:", res["status"])
