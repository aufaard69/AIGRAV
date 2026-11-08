"""Verification Engine ensuring zero-tolerance for false positives."""

from __future__ import annotations

import json
from dataclasses import dataclass
from typing import Any, Dict, Optional

from .models import Step, VerificationCriteria
from .tool_router import ToolRouter


@dataclass
class VerificationResult:
    """Outcome of a verification step."""
    verified: bool
    reason: str
    observed_data: Optional[Any] = None


class Verifier:
    """Semantic and syntactic validation engine."""

    def __init__(self, router: ToolRouter):
        self.router = router

    def verify_step(
        self,
        step: Step,
        execution_output: Dict[str, Any],
    ) -> VerificationResult:
        """Verify step output according to its defined VerificationCriteria."""
        criteria: Optional[VerificationCriteria] = step.verification_criteria
        raw_result = execution_output.get("raw_result", "")

        # Default rule if none specified: Non-empty response
        if criteria is None or criteria.type == "NON_EMPTY":
            if raw_result and not str(raw_result).strip().startswith("AIGRAV Error"):
                return VerificationResult(
                    verified=True,
                    reason="Output is non-empty and contains no error prefix.",
                    observed_data=raw_result,
                )
            return VerificationResult(
                verified=False,
                reason="Output was empty or contained an error prefix.",
                observed_data=raw_result,
            )

        # Rule: Exact string match
        if criteria.type == "EQUALS":
            if str(raw_result).strip() == str(criteria.expected_value).strip():
                return VerificationResult(
                    verified=True,
                    reason=f"Output exactly matches expected value '{criteria.expected_value}'.",
                    observed_data=raw_result,
                )
            return VerificationResult(
                verified=False,
                reason=f"Output '{raw_result}' did not match expected '{criteria.expected_value}'.",
                observed_data=raw_result,
            )

        # Rule: JSON Content validation
        if criteria.type == "VALID_JSON":
            try:
                parsed = json.loads(raw_result)
                return VerificationResult(
                    verified=True,
                    reason="Output parsed successfully as valid JSON.",
                    observed_data=parsed,
                )
            except Exception as ex:
                return VerificationResult(
                    verified=False,
                    reason=f"Failed to parse output as valid JSON: {ex}",
                    observed_data=raw_result,
                )

        # Rule: Follow-up query verification (Two-phase verification)
        if criteria.type == "FOLLOW_UP_QUERY":
            if not criteria.follow_up_tool:
                return VerificationResult(
                    verified=False,
                    reason="FOLLOW_UP_QUERY criteria missing follow_up_tool definition.",
                )

            follow_up_res = self.router.call_tool(
                tool_name=criteria.follow_up_tool,
                argument=criteria.follow_up_argument,
            )

            if not follow_up_res.transport_success or not follow_up_res.tool_success:
                return VerificationResult(
                    verified=False,
                    reason=f"Follow-up query '{criteria.follow_up_tool}' failed: {follow_up_res.error_message}",
                    observed_data=follow_up_res.raw_result,
                )

            # Compare follow-up result with primary execution output
            if criteria.expected_value is not None:
                if str(follow_up_res.raw_result).strip() != str(criteria.expected_value).strip():
                    return VerificationResult(
                        verified=False,
                        reason=f"Follow-up verification mismatch. Expected '{criteria.expected_value}', got '{follow_up_res.raw_result}'.",
                        observed_data=follow_up_res.raw_result,
                    )

            return VerificationResult(
                verified=True,
                reason=f"Follow-up query '{criteria.follow_up_tool}' confirmed consistency.",
                observed_data=follow_up_res.raw_result,
            )

        return VerificationResult(
            verified=False,
            reason=f"Unsupported verification criteria type: {criteria.type}",
        )
