"""AIGRAV Decision & Review Engine Data Contracts and Models."""

from __future__ import annotations

import enum
from dataclasses import dataclass, field
from typing import Any, Dict, List, Optional


class DecisionOutcome(str, enum.Enum):
    """The 6 mandatory decision gate states for Phase 3.0."""
    READY = "READY"
    CLARIFICATION_REQUIRED = "CLARIFICATION_REQUIRED"
    RECOMMENDATION_REQUIRED = "RECOMMENDATION_REQUIRED"
    APPROVAL_REQUIRED = "APPROVAL_REQUIRED"
    UNSUPPORTED = "UNSUPPORTED"
    BLOCKED = "BLOCKED"


@dataclass
class IntentAnalysis:
    """Parsed structured intent of a user prompt."""
    raw_prompt: str
    requested_outcome: str
    target_elements: List[str] = field(default_factory=list)
    operations: List[str] = field(default_factory=list)
    constraints: Dict[str, Any] = field(default_factory=dict)
    is_mutating: bool = False
    is_destructive: bool = False
    missing_information: List[str] = field(default_factory=list)
    inferred_assumptions: List[str] = field(default_factory=list)

    def to_dict(self) -> Dict[str, Any]:
        return {
            "raw_prompt": self.raw_prompt,
            "requested_outcome": self.requested_outcome,
            "target_elements": list(self.target_elements),
            "operations": list(self.operations),
            "constraints": dict(self.constraints),
            "is_mutating": self.is_mutating,
            "is_destructive": self.is_destructive,
            "missing_information": list(self.missing_information),
            "inferred_assumptions": list(self.inferred_assumptions),
        }


@dataclass
class RecommendationOption:
    """Option presented when alternative approaches exist."""
    title: str
    description: str
    advantages: List[str] = field(default_factory=list)
    disadvantages: List[str] = field(default_factory=list)
    risks: List[str] = field(default_factory=list)

    def to_dict(self) -> Dict[str, Any]:
        return {
            "title": self.title,
            "description": self.description,
            "advantages": list(self.advantages),
            "disadvantages": list(self.disadvantages),
            "risks": list(self.risks),
        }


@dataclass
class DecisionReviewResult:
    """Evaluated prompt review and decision outcome."""
    outcome: DecisionOutcome
    reason: str
    intent: IntentAnalysis
    recommended_option: Optional[RecommendationOption] = None
    alternative_options: List[RecommendationOption] = field(default_factory=list)
    clarification_questions: List[str] = field(default_factory=list)
    approval_prompt: Optional[str] = None
    plan_steps: List[Dict[str, Any]] = field(default_factory=list)
    context_data: Dict[str, Any] = field(default_factory=dict)
    # Phase 3.1 additions (defaults preserve Phase 3.0 behaviour)
    risks: List[str] = field(default_factory=list)
    review_source: str = "deterministic"  # "deterministic" | "llm+deterministic"
    llm_status: str = "NOT_USED"          # NOT_USED | OK | UNAVAILABLE: ... | INVALID_OUTPUT: ...
    safety_overrides: List[str] = field(default_factory=list)

    def to_dict(self) -> Dict[str, Any]:
        return {
            "outcome": self.outcome.value,
            "reason": self.reason,
            "intent": self.intent.to_dict(),
            "recommended_option": self.recommended_option.to_dict() if self.recommended_option else None,
            "alternative_options": [opt.to_dict() for opt in self.alternative_options],
            "clarification_questions": list(self.clarification_questions),
            "approval_prompt": self.approval_prompt,
            "plan_steps": list(self.plan_steps),
            "context_data": dict(self.context_data),
            "risks": list(self.risks),
            "review_source": self.review_source,
            "llm_status": self.llm_status,
            "safety_overrides": list(self.safety_overrides),
        }


# ---------------------------------------------------------------------------
# Phase 3.1: deserialisation helpers (used by DecisionSessionManager)
# ---------------------------------------------------------------------------

def _option_from_dict(data: Optional[Dict[str, Any]]) -> Optional[RecommendationOption]:
    if not data:
        return None
    return RecommendationOption(
        title=str(data.get("title", "")),
        description=str(data.get("description", "")),
        advantages=[str(x) for x in data.get("advantages", []) or []],
        disadvantages=[str(x) for x in data.get("disadvantages", []) or []],
        risks=[str(x) for x in data.get("risks", []) or []],
    )


def review_result_from_dict(data: Dict[str, Any]) -> DecisionReviewResult:
    intent_d = data.get("intent", {}) or {}
    intent = IntentAnalysis(
        raw_prompt=str(intent_d.get("raw_prompt", "")),
        requested_outcome=str(intent_d.get("requested_outcome", "")),
        target_elements=list(intent_d.get("target_elements", [])),
        operations=list(intent_d.get("operations", [])),
        constraints=dict(intent_d.get("constraints", {})),
        is_mutating=bool(intent_d.get("is_mutating", False)),
        is_destructive=bool(intent_d.get("is_destructive", False)),
        missing_information=list(intent_d.get("missing_information", [])),
        inferred_assumptions=list(intent_d.get("inferred_assumptions", [])),
    )
    return DecisionReviewResult(
        outcome=DecisionOutcome(data.get("outcome", DecisionOutcome.BLOCKED.value)),
        reason=str(data.get("reason", "")),
        intent=intent,
        recommended_option=_option_from_dict(data.get("recommended_option")),
        alternative_options=[o for o in (_option_from_dict(x) for x in data.get("alternative_options", [])) if o],
        clarification_questions=list(data.get("clarification_questions", [])),
        approval_prompt=data.get("approval_prompt"),
        plan_steps=list(data.get("plan_steps", [])),
        context_data=dict(data.get("context_data", {})),
        risks=list(data.get("risks", [])),
        review_source=str(data.get("review_source", "deterministic")),
        llm_status=str(data.get("llm_status", "NOT_USED")),
        safety_overrides=list(data.get("safety_overrides", [])),
    )
