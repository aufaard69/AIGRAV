"""Comprehensive Test Suite for Phase 3.0 Decision & Review Engine.

Covers all 8 required test cases:
1. Clear read-only request -> READY without unnecessary questions.
2. Ambiguous request -> CLARIFICATION_REQUIRED.
3. Request with multiple meaningful approaches -> RECOMMENDATION_REQUIRED.
4. Unsupported operation -> UNSUPPORTED.
5. Consequential model modification -> APPROVAL_REQUIRED.
6. Missing Revit context / offline bridge -> BLOCKED.
7. Execution result mismatch -> Verification failure (FAILED), not false success.
8. Successful read-only task -> Real/mock execution with semantic read-back verification.
"""

from __future__ import annotations

import json
import shutil
import sys
import tempfile
from pathlib import Path
from typing import Any, Dict, List, Optional

CURRENT_DIR = Path(__file__).resolve().parent
if str(CURRENT_DIR) not in sys.path:
    sys.path.insert(0, str(CURRENT_DIR))

from orchestrator.decision_engine import DecisionReviewEngine
from orchestrator.decision_models import DecisionOutcome
from orchestrator.engine import TaskExecutionEngine
from orchestrator.models import ExecutionStatus, Step, Task, VerificationCriteria
from orchestrator.task_store import TaskStore
from orchestrator.tool_router import ToolExecutionResult, ToolRouter
from orchestrator.verifier import Verifier


class MockDecisionToolRouter(ToolRouter):
    """Configurable mock router for isolated decision and verification tests."""

    def __init__(self, is_online: bool = True):
        super().__init__()
        self.is_online = is_online
        self.responses: Dict[str, List[ToolExecutionResult]] = {}
        self.call_history: List[Dict[str, Any]] = []

    def set_responses(self, tool_name: str, results: List[ToolExecutionResult]) -> None:
        self.responses[tool_name] = results

    def call_tool(
        self,
        tool_name: str,
        argument: Optional[str] = None,
        timeout: Optional[float] = None,
    ) -> ToolExecutionResult:
        self.call_history.append({"tool": tool_name, "argument": argument})
        if not self.is_online:
            return ToolExecutionResult(
                transport_success=False,
                tool_success=False,
                raw_result="",
                error_message="Connection refused: Bridge offline",
                is_transient=True,
            )

        if tool_name in self.responses and self.responses[tool_name]:
            return self.responses[tool_name].pop(0)

        return ToolExecutionResult(
            transport_success=True,
            tool_success=True,
            raw_result=f"Mock response for {tool_name}",
        )


def run_phase_3_tests() -> Dict[str, str]:
    print("=" * 75)
    print("AIGRAV PHASE 3.0: DECISION & REVIEW ENGINE TEST HARNESS")
    print("=" * 75)

    test_results: Dict[str, str] = {}
    mock_router = MockDecisionToolRouter(is_online=True)
    engine_mock = DecisionReviewEngine(router=mock_router)

    # --------------------------------------------------------------------------
    # TEST 1: Clear read-only request -> READY without unnecessary questions
    # --------------------------------------------------------------------------
    print("\n[TEST 1] Testing Clear Read-Only Request...")
    res1 = engine_mock.review_and_decide("Inspect active project information and current view")
    if res1.outcome == DecisionOutcome.READY and not res1.clarification_questions and res1.plan_steps:
        print(f"  -> Outcome: {res1.outcome.value} (Plan steps: {len(res1.plan_steps)})")
        test_results["Test 1: Clear Read-Only Request"] = "PASS (Mocked)"
    else:
        print(f"  -> Unexpected outcome: {res1.outcome.value}")
        test_results["Test 1: Clear Read-Only Request"] = "FAIL"

    # --------------------------------------------------------------------------
    # TEST 2: Ambiguous request -> CLARIFICATION_REQUIRED
    # --------------------------------------------------------------------------
    print("\n[TEST 2] Testing Ambiguous Request...")
    res2 = engine_mock.review_and_decide("ubah model")
    if res2.outcome == DecisionOutcome.CLARIFICATION_REQUIRED and len(res2.clarification_questions) >= 1:
        print(f"  -> Outcome: {res2.outcome.value} (Clarifications: {res2.clarification_questions})")
        test_results["Test 2: Ambiguous Request"] = "PASS (Mocked)"
    else:
        print(f"  -> Unexpected outcome: {res2.outcome.value}")
        test_results["Test 2: Ambiguous Request"] = "FAIL"

    # --------------------------------------------------------------------------
    # TEST 3: Multiple meaningful approaches -> RECOMMENDATION_REQUIRED
    # --------------------------------------------------------------------------
    print("\n[TEST 3] Testing Request with Alternative Approaches...")
    res3 = engine_mock.review_and_decide("Hitung volume kolom struktural dan bandingkan cara perhitungannya")
    if (
        res3.outcome == DecisionOutcome.RECOMMENDATION_REQUIRED
        and res3.recommended_option is not None
        and len(res3.alternative_options) >= 1
    ):
        print(f"  -> Outcome: {res3.outcome.value}")
        print(f"     Recommended: {res3.recommended_option.title}")
        print(f"     Alternative: {res3.alternative_options[0].title}")
        test_results["Test 3: Multiple Meaningful Approaches"] = "PASS (Mocked)"
    else:
        print(f"  -> Unexpected outcome: {res3.outcome.value}")
        test_results["Test 3: Multiple Meaningful Approaches"] = "FAIL"

    # --------------------------------------------------------------------------
    # TEST 4: Unsupported operation -> UNSUPPORTED
    # --------------------------------------------------------------------------
    print("\n[TEST 4] Testing Unsupported Operation...")
    res4 = engine_mock.review_and_decide("Lakukan FEM analysis dan export model ke SAP2000")
    if res4.outcome == DecisionOutcome.UNSUPPORTED and "not supported" in res4.reason.lower():
        print(f"  -> Outcome: {res4.outcome.value} (Reason: {res4.reason})")
        test_results["Test 4: Unsupported Operation"] = "PASS (Mocked)"
    else:
        print(f"  -> Unexpected outcome: {res4.outcome.value}")
        test_results["Test 4: Unsupported Operation"] = "FAIL"

    # --------------------------------------------------------------------------
    # TEST 5: Consequential model modification / deletion -> APPROVAL_REQUIRED
    # --------------------------------------------------------------------------
    print("\n[TEST 5] Testing Consequential Deletion Request...")
    res5 = engine_mock.review_and_decide("Delete element 330753 from Level 1")
    if res5.outcome == DecisionOutcome.APPROVAL_REQUIRED and res5.approval_prompt is not None:
        print(f"  -> Outcome: {res5.outcome.value}")
        print(f"     Approval Prompt: {res5.approval_prompt}")
        test_results["Test 5: Consequential Modification / Deletion"] = "PASS (Mocked)"
    else:
        print(f"  -> Unexpected outcome: {res5.outcome.value}")
        test_results["Test 5: Consequential Modification / Deletion"] = "FAIL"

    # --------------------------------------------------------------------------
    # TEST 6: Missing Revit Context (Offline Bridge) -> BLOCKED
    # --------------------------------------------------------------------------
    print("\n[TEST 6] Testing Missing Context / Offline Bridge Detection...")
    offline_router = MockDecisionToolRouter(is_online=False)
    engine_offline = DecisionReviewEngine(router=offline_router)
    res6 = engine_offline.review_and_decide("Inspect project info and columns")
    if res6.outcome == DecisionOutcome.BLOCKED and "offline or inaccessible" in res6.reason:
        print(f"  -> Outcome: {res6.outcome.value} (Reason: {res6.reason})")
        test_results["Test 6: Missing Context / Offline Bridge"] = "PASS (Mocked)"
    else:
        print(f"  -> Unexpected outcome: {res6.outcome.value}")
        test_results["Test 6: Missing Context / Offline Bridge"] = "FAIL"

    # --------------------------------------------------------------------------
    # TEST 7: Execution Result Mismatch -> Fails Verification (Anti-False Success)
    # --------------------------------------------------------------------------
    print("\n[TEST 7] Testing Verification Failure on Result Mismatch...")
    temp_dir = tempfile.mkdtemp(prefix="aigrav_test_verif_")
    try:
        store = TaskStore(storage_dir=temp_dir)
        router7 = MockDecisionToolRouter(is_online=True)
        # Primary call returns Success, but Follow-up verification returns Mismatched data
        router7.set_responses(
            "get_project_info",
            [
                ToolExecutionResult(transport_success=True, tool_success=True, raw_result="Project Alpha"),
                # Follow up returns different project name
                ToolExecutionResult(transport_success=True, tool_success=True, raw_result="Project Beta Mismatch"),
            ],
        )

        step_mismatch = Step(
            step_id="step_mismatch",
            title="Read and Assert Project Info",
            tool_name="get_project_info",
            verification_criteria=VerificationCriteria(
                type="FOLLOW_UP_QUERY",
                follow_up_tool="get_project_info",
                expected_value="Project Alpha",  # Expected Alpha, but follow-up gives Beta
            ),
        )

        task_mismatch = Task(
            task_id="TASK-MISMATCH-001",
            user_request="Verify project info consistency",
            steps=[step_mismatch],
        )

        exec_engine = TaskExecutionEngine(router=router7, store=store)
        res_mismatch_task = exec_engine.execute_task(task_mismatch)

        if (
            res_mismatch_task.execution_status == ExecutionStatus.FAILED
            and res_mismatch_task.get_step("step_mismatch").execution_status == ExecutionStatus.FAILED
            and any("mismatch" in e.message.lower() for e in res_mismatch_task.get_step("step_mismatch").errors)
        ):
            print("  -> Verification correctly caught mismatch: Marked as FAILED (No false positive).")
            test_results["Test 7: Result Mismatch Fails Verification"] = "PASS (Mocked)"
        else:
            print(f"  -> Unexpected status: {res_mismatch_task.execution_status.value}")
            test_results["Test 7: Result Mismatch Fails Verification"] = "FAIL"
    finally:
        shutil.rmtree(temp_dir, ignore_errors=True)

    # --------------------------------------------------------------------------
    # TEST 8: Successful Read-Only Task with Live Revit Verification
    # --------------------------------------------------------------------------
    print("\n[TEST 8] Testing End-to-End Decision -> Plan -> Live Revit Execution -> Verification...")
    live_router = ToolRouter(bridge_url="http://127.0.0.1:8765/", default_timeout=15.0)
    live_ping = live_router.call_tool("aigrav_ping")

    if live_ping.transport_success and live_ping.tool_success:
        print("  -> Live Revit Bridge detected. Executing against live Revit 2026 instance...")
        engine_live = DecisionReviewEngine(router=live_router)
        prompt_live = "Inspect active project information and current view"
        decision_live = engine_live.review_and_decide(prompt_live)

        assert decision_live.outcome == DecisionOutcome.READY, f"Expected READY, got {decision_live.outcome}"

        base_dir = Path(__file__).resolve().parent.parent.parent
        history_dir = base_dir / "KnowledgeBase" / "TaskHistory"
        live_store = TaskStore(storage_dir=str(history_dir))
        live_exec_engine = TaskExecutionEngine(router=live_router, store=live_store)

        live_task = engine_live.create_task_from_decision("TASK-PHASE-3-LIVE-001", decision_live)
        assert live_task is not None, "Failed to create Task from decision"

        # Execute on live Revit
        final_live_task = live_exec_engine.execute_task(live_task)

        if final_live_task.execution_status == ExecutionStatus.COMPLETED:
            print("  -> Live Task COMPLETED and semantic verification verified.")
            for st in final_live_task.steps:
                print(f"     Step '{st.step_id}': {repr(st.output.get('raw_result')[:60])}...")
            test_results["Test 8: End-to-End Live Decision & Execution"] = "PASS (Live Revit)"
        else:
            print(f"  -> Live Task FAILED: {final_live_task.execution_status.value}")
            test_results["Test 8: End-to-End Live Decision & Execution"] = "FAIL (Live Revit)"
    else:
        print("  -> Live Bridge offline. Running Test 8 under simulated transport...")
        mock_e2e_router = MockDecisionToolRouter(is_online=True)
        mock_e2e_engine = DecisionReviewEngine(router=mock_e2e_router)
        decision_mock = mock_e2e_engine.review_and_decide("Inspect active project information and current view")
        temp_dir_e2e = tempfile.mkdtemp()
        try:
            e2e_store = TaskStore(storage_dir=temp_dir_e2e)
            e2e_engine = TaskExecutionEngine(router=mock_e2e_router, store=e2e_store)
            task_e2e = mock_e2e_engine.create_task_from_decision("TASK-E2E-MOCK-001", decision_mock)
            final_mock_task = e2e_engine.execute_task(task_e2e)
            if final_mock_task.execution_status == ExecutionStatus.COMPLETED:
                test_results["Test 8: End-to-End Decision & Execution"] = "PASS (Mocked)"
            else:
                test_results["Test 8: End-to-End Decision & Execution"] = "FAIL"
        finally:
            shutil.rmtree(temp_dir_e2e, ignore_errors=True)

    # --------------------------------------------------------------------------
    # Summary Table
    # --------------------------------------------------------------------------
    print("\n" + "=" * 75)
    print("PHASE 3.0 TEST RESULTS SUMMARY")
    print("=" * 75)
    all_passed = True
    for t_name, t_status in test_results.items():
        print(f"  {t_name.ljust(50)}: {t_status}")
        if "FAIL" in t_status:
            all_passed = False
    print("=" * 75)

    if all_passed:
        print("OVERALL PHASE 3.0 RESULT: ALL TESTS PASSED.")
    else:
        print("OVERALL PHASE 3.0 RESULT: SOME TESTS FAILED.")
        sys.exit(1)

    return test_results


if __name__ == "__main__":
    run_phase_3_tests()
