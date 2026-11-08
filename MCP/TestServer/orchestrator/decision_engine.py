"""AIGRAV Decision & Review Engine.

Pre-execution intelligence layer evaluating user intent, inspecting model context,
generating recommendations, classifying risk into 6 Decision Gates, and synthesizing
execution plans for the TaskExecutionEngine.
"""

from __future__ import annotations

import re
from typing import Any, Dict, List, Optional, Set

from .decision_models import (
    DecisionOutcome,
    DecisionReviewResult,
    IntentAnalysis,
    RecommendationOption,
)
from .models import (
    ExecutionStatus,
    Step,
    Task,
    VerificationCriteria,
)
from .tool_router import ToolRouter


class DecisionReviewEngine:
    """Pre-execution Decision and Review Layer."""

    # Built-in supported read/inspection operations
    KNOWN_READ_OPERATIONS = {
        "read_project_info", "get_project_info", "aigrav_get_revit_info",
        "get_active_view", "aigrav_get_active_view",
        "get_selection", "aigrav_get_selection",
        "get_structural_columns", "get_columns",
        "get_element", "aigrav_get_element",
        "get_element_parameters",
        "aigrav_qto",
    }

    # Modifying operations
    KNOWN_MODIFYING_OPERATIONS = {
        "create_column", "aigrav_create_column",
        "create_beam", "aigrav_create_beam",
        "create_wall", "aigrav_create_wall",
        "create_floor", "aigrav_create_floor",
        "set_parameter", "aigrav_set_parameter",
        "move_element", "copy_element", "rotate_element",
    }

    # Destructive operations requiring explicit approval
    KNOWN_DESTRUCTIVE_OPERATIONS = {
        "delete_element", "delete_elements",
        "unjoin_geometry", "purge",
    }

    def __init__(self, router: ToolRouter):
        self.router = router

    def analyze_intent(self, prompt: str) -> IntentAnalysis:
        """Parse raw prompt into structured intent."""
        p_lower = prompt.lower().strip()

        # Mutating & destructive classification
        is_destructive = any(
            kw in p_lower for kw in ["delete", "hapus", "purge", "remove", "unjoin"]
        )
        is_mutating = is_destructive or any(
            kw in p_lower for kw in ["create", "buat", "tambah", "modify", "ubah", "set", "move", "rotate"]
        )

        # Detect requested outcome & operations
        operations: List[str] = []
        target_elements: List[str] = []
        missing_info: List[str] = []
        inferred_assumptions: List[str] = []

        if "column" in p_lower or "kolom" in p_lower:
            target_elements.append("OST_StructuralColumns")
        if "view" in p_lower or "active view" in p_lower:
            target_elements.append("ActiveView")
        if "project" in p_lower or "proyek" in p_lower:
            target_elements.append("ProjectInfo")

        # Check for specific intents
        if any(w in p_lower for w in ["read", "baca", "inspect", "inspeksi", "lihat", "check", "periksa"]):
            if any(k in p_lower for k in ["project", "proyek", "model", "revit"]):
                operations.append("get_project_info")
            if any(k in p_lower for k in ["view", "tampilan", "model"]):
                operations.append("get_active_view")
            if "selection" in p_lower or "seleksi" in p_lower:
                operations.append("get_selection")
            if "column" in p_lower or "kolom" in p_lower:
                operations.append("get_structural_columns")

        if any(w in p_lower for w in ["create", "buat", "tambah"]):
            if "column" in p_lower or "kolom" in p_lower:
                operations.append("create_column")
                # Check for essential parameters
                if not re.search(r"\b(type|tipe|level|koordinat|x|y)\b", p_lower):
                    missing_info.append("Column type, location coordinates, or base level")
            elif "beam" in p_lower or "balok" in p_lower:
                operations.append("create_beam")
                missing_info.append("Beam profile, start/end points, or level")

        if any(w in p_lower for w in ["delete", "hapus"]):
            operations.append("delete_element")
            # Element id or target filter needed
            if not re.search(r"\b(\d{4,9}|all|semua)\b", p_lower):
                missing_info.append("Target Element ID(s) to delete")

        # Unsupported operations detection
        if any(kw in p_lower for kw in ["analysis", "analisa beban", "sap2000", "etabs", "dynamo script", "export ifc4", "render lumion"]):
            operations.append("unsupported_external_task")

        requested_outcome = prompt
        return IntentAnalysis(
            raw_prompt=prompt,
            requested_outcome=requested_outcome,
            target_elements=target_elements,
            operations=operations,
            constraints={},
            is_mutating=is_mutating,
            is_destructive=is_destructive,
            missing_information=missing_info,
            inferred_assumptions=inferred_assumptions,
        )

    def review_and_decide(
        self,
        prompt: str,
        active_context: Optional[Dict[str, Any]] = None,
    ) -> DecisionReviewResult:
        """Evaluate intent, inspect context if needed, and classify into a Decision Gate."""
        intent = self.analyze_intent(prompt)
        p_lower = prompt.lower().strip()

        # 1. GATE: UNSUPPORTED
        if "unsupported_external_task" in intent.operations or any(
            kw in p_lower for kw in ["fem analysis", "etabs export", "sap2000 calculation", "lumion rendering"]
        ):
            return DecisionReviewResult(
                outcome=DecisionOutcome.UNSUPPORTED,
                reason="Requested operation requires external FEA/rendering software not supported by active Revit Bridge tools.",
                intent=intent,
            )

        # 2. GATE: CLARIFICATION_REQUIRED (Missing essential parameters or ambiguous intent)
        if intent.missing_information:
            return DecisionReviewResult(
                outcome=DecisionOutcome.CLARIFICATION_REQUIRED,
                reason="Essential parameters are missing to safely proceed with the request.",
                intent=intent,
                clarification_questions=[
                    f"Please specify {info}." for info in intent.missing_information
                ],
            )

        is_ambiguous_phrase = (
            p_lower in ["ubah model", "perbaiki kolom", "update parameter", "proses elemen", "do something"]
            or (any(p_lower.startswith(pfx) for pfx in ["ubah model", "perbaiki kolom", "update parameter", "proses elemen"])
                and not any(k in p_lower for k in ["kolom", "balok", "level", "k1", "grid", "wall", "lantai", "tipe"]))
        )
        if is_ambiguous_phrase:
            return DecisionReviewResult(
                outcome=DecisionOutcome.CLARIFICATION_REQUIRED,
                reason="Request is highly ambiguous without specific target elements or operations.",
                intent=intent,
                clarification_questions=[
                    "Which specific Revit elements or categories should be modified?",
                    "What target parameter or property values should be applied?",
                ],
            )

        # 3. Context Inspection & BLOCKED Gate Check
        context_data = active_context or {}
        if not context_data and any(op in ["get_project_info", "get_active_view", "get_selection", "get_structural_columns"] for op in intent.operations):
            # Probe bridge live
            ping_res = self.router.call_tool("aigrav_ping")
            if not ping_res.transport_success:
                return DecisionReviewResult(
                    outcome=DecisionOutcome.BLOCKED,
                    reason=f"Revit Bridge is offline or inaccessible: {ping_res.error_message}. Cannot inspect model context.",
                    intent=intent,
                )

        # 4. GATE: RECOMMENDATION_REQUIRED (Multiple valid approaches / trade-offs)
        if any(kw in p_lower for kw in ["hitung volume", "hitung kuantitas", "qto", "quantity takeoff", "bandingkan cara"]):
            rec_opt = RecommendationOption(
                title="AIGRAV QTO Engine (Fast & Category-Grouped)",
                description="Extract volume, unit count, and classification directly through native QTO engine grouped by Type.",
                advantages=["Extremely fast execution (<50ms)", "Consistent with tender standards", "Zero model modification"],
                disadvantages=["Aggregated report format without per-instance coordinates"],
            )
            alt_opt = RecommendationOption(
                title="Instance-by-Instance Query & Manual Parameter Aggregation",
                description="Query all 572 columns individually via get_element_parameters and sum volume values in Python.",
                advantages=["Per-element location marks and level mapping"],
                disadvantages=["Higher API latency (sequential HTTP requests)", "Greater processing overhead"],
            )
            return DecisionReviewResult(
                outcome=DecisionOutcome.RECOMMENDATION_REQUIRED,
                reason="Multiple engineering approaches exist for quantity takeoff and volume inspection.",
                intent=intent,
                recommended_option=rec_opt,
                alternative_options=[alt_opt],
            )

        # 5. GATE: APPROVAL_REQUIRED (Consequential modifications or deletions)
        if intent.is_destructive:
            return DecisionReviewResult(
                outcome=DecisionOutcome.APPROVAL_REQUIRED,
                reason="Operation involves permanent deletion of model elements which is consequential and irreversible.",
                intent=intent,
                approval_prompt=f"[AIGRAV APPROVAL REQUIRED] Consequential deletion requested: '{prompt}'. Do you confirm execution?",
            )

        if intent.is_mutating:
            return DecisionReviewResult(
                outcome=DecisionOutcome.APPROVAL_REQUIRED,
                reason="Operation mutates Revit model geometry or parameters.",
                intent=intent,
                approval_prompt=f"[AIGRAV APPROVAL REQUIRED] Model modification requested: '{prompt}'. Proceed with transaction?",
            )

        # 6. GATE: READY (Clear, supported, non-mutating request)
        plan_steps: List[Dict[str, Any]] = []
        if "get_project_info" in intent.operations or "read_project_info" in intent.operations:
            plan_steps.append({
                "step_id": "step_info",
                "title": "Read Project Information",
                "tool_name": "get_project_info",
                "argument": None,
                "verification": {"type": "NON_EMPTY"},
            })
        if "get_active_view" in intent.operations:
            plan_steps.append({
                "step_id": "step_view",
                "title": "Read Active View",
                "tool_name": "get_active_view",
                "argument": None,
                "verification": {"type": "NON_EMPTY"},
            })
        if "get_selection" in intent.operations:
            plan_steps.append({
                "step_id": "step_selection",
                "title": "Read Current Selection",
                "tool_name": "get_selection",
                "argument": None,
                "verification": {"type": "NON_EMPTY"},
            })
        if "get_structural_columns" in intent.operations:
            plan_steps.append({
                "step_id": "step_columns",
                "title": "Read Structural Columns",
                "tool_name": "get_structural_columns",
                "argument": None,
                "verification": {"type": "NON_EMPTY"},
            })

        # If explicit BIM operations were requested, return READY with plan_steps
        if plan_steps:
            return DecisionReviewResult(
                outcome=DecisionOutcome.READY,
                reason="Request is fully specified, supported by available tools, read-only, and safe to execute.",
                intent=intent,
                plan_steps=plan_steps,
            )

        # Conversational or general utterance with no automated BIM transactions required
        return DecisionReviewResult(
            outcome=DecisionOutcome.READY,
            reason="Non-mutating conversational utterance with no automated BIM transactions required.",
            intent=intent,
            plan_steps=[],
        )

    def create_task_from_decision(
        self,
        task_id: str,
        review_result: DecisionReviewResult,
    ) -> Optional[Task]:
        """Convert approved READY decision into executable Task for TaskExecutionEngine."""
        if review_result.outcome != DecisionOutcome.READY:
            return None

        steps: List[Step] = []
        dep_id = None
        for s_data in review_result.plan_steps:
            v_crit = None
            if "verification" in s_data:
                v_crit = VerificationCriteria.from_dict(s_data["verification"])

            step = Step(
                step_id=s_data["step_id"],
                title=s_data["title"],
                tool_name=s_data["tool_name"],
                argument=s_data.get("argument"),
                dependencies=[dep_id] if dep_id else [],
                verification_criteria=v_crit,
            )
            steps.append(step)
            dep_id = step.step_id

        return Task(
            task_id=task_id,
            user_request=review_result.intent.raw_prompt,
            steps=steps,
        )
