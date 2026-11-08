"""AIGRAV Agent Orchestrator Data Contracts and Models."""

from __future__ import annotations

import enum
from dataclasses import dataclass, field
from datetime import datetime, timezone
from typing import Any, Dict, List, Optional


class ExecutionStatus(str, enum.Enum):
    """Lifecycle states for Task and individual Steps."""
    PLANNED = "PLANNED"
    READY = "READY"
    RUNNING = "RUNNING"
    WAITING = "WAITING"
    VERIFYING = "VERIFYING"
    COMPLETED = "COMPLETED"
    FAILED = "FAILED"
    CANCELLED = "CANCELLED"
    PAUSED = "PAUSED"


def current_iso_timestamp() -> str:
    """Return ISO 8601 UTC timestamp."""
    return datetime.now(timezone.utc).isoformat()


@dataclass
class StepError:
    """Structured record of a single failure occurrence."""
    attempt: int
    error_type: str
    message: str
    timestamp: str = field(default_factory=current_iso_timestamp)

    def to_dict(self) -> Dict[str, Any]:
        return {
            "attempt": self.attempt,
            "error_type": self.error_type,
            "message": self.message,
            "timestamp": self.timestamp,
        }

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> StepError:
        return cls(
            attempt=int(data.get("attempt", 1)),
            error_type=str(data.get("error_type", "UNKNOWN")),
            message=str(data.get("message", "")),
            timestamp=str(data.get("timestamp", current_iso_timestamp())),
        )


@dataclass
class VerificationCriteria:
    """Criteria evaluated by the Verifier before a step completes."""
    type: str  # e.g., NON_EMPTY, EQUALS, JSON_PATH_MATCH, FOLLOW_UP_QUERY
    assertion: Optional[str] = None
    follow_up_tool: Optional[str] = None
    follow_up_argument: Optional[str] = None
    expected_value: Optional[Any] = None

    def to_dict(self) -> Dict[str, Any]:
        return {
            "type": self.type,
            "assertion": self.assertion,
            "follow_up_tool": self.follow_up_tool,
            "follow_up_argument": self.follow_up_argument,
            "expected_value": self.expected_value,
        }

    @classmethod
    def from_dict(cls, data: Optional[Dict[str, Any]]) -> Optional[VerificationCriteria]:
        if not data:
            return None
        return cls(
            type=str(data.get("type", "NON_EMPTY")),
            assertion=data.get("assertion"),
            follow_up_tool=data.get("follow_up_tool"),
            follow_up_argument=data.get("follow_up_argument"),
            expected_value=data.get("expected_value"),
        )


@dataclass
class Step:
    """Atomic execution unit in a task DAG."""
    step_id: str
    title: str
    tool_name: str
    argument: Optional[str] = None
    dependencies: List[str] = field(default_factory=list)
    execution_status: ExecutionStatus = ExecutionStatus.PLANNED
    retry_count: int = 0
    max_retries: int = 2
    timeout_seconds: float = 30.0
    verification_criteria: Optional[VerificationCriteria] = None
    output: Optional[Dict[str, Any]] = None
    errors: List[StepError] = field(default_factory=list)

    def to_dict(self) -> Dict[str, Any]:
        return {
            "step_id": self.step_id,
            "title": self.title,
            "tool_name": self.tool_name,
            "argument": self.argument,
            "dependencies": list(self.dependencies),
            "execution_status": self.execution_status.value,
            "retry_count": self.retry_count,
            "max_retries": self.max_retries,
            "timeout_seconds": self.timeout_seconds,
            "verification_criteria": (
                self.verification_criteria.to_dict()
                if self.verification_criteria
                else None
            ),
            "output": self.output,
            "errors": [err.to_dict() for err in self.errors],
        }

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> Step:
        status_val = data.get("execution_status", ExecutionStatus.PLANNED.value)
        try:
            status = ExecutionStatus(status_val)
        except ValueError:
            status = ExecutionStatus.PLANNED

        return cls(
            step_id=str(data["step_id"]),
            title=str(data.get("title", data["step_id"])),
            tool_name=str(data["tool_name"]),
            argument=data.get("argument"),
            dependencies=list(data.get("dependencies", [])),
            execution_status=status,
            retry_count=int(data.get("retry_count", 0)),
            max_retries=int(data.get("max_retries", 2)),
            timeout_seconds=float(data.get("timeout_seconds", 30.0)),
            verification_criteria=VerificationCriteria.from_dict(
                data.get("verification_criteria")
            ),
            output=data.get("output"),
            errors=[StepError.from_dict(e) for e in data.get("errors", [])],
        )


@dataclass
class FinalReport:
    """Summary of task outcome."""
    success: bool
    summary: str
    completed_steps: int = 0
    total_steps: int = 0
    failed_steps: int = 0
    details: Dict[str, Any] = field(default_factory=dict)

    def to_dict(self) -> Dict[str, Any]:
        return {
            "success": self.success,
            "summary": self.summary,
            "completed_steps": self.completed_steps,
            "total_steps": self.total_steps,
            "failed_steps": self.failed_steps,
            "details": self.details,
        }

    @classmethod
    def from_dict(cls, data: Optional[Dict[str, Any]]) -> Optional[FinalReport]:
        if not data:
            return None
        return cls(
            success=bool(data.get("success", False)),
            summary=str(data.get("summary", "")),
            completed_steps=int(data.get("completed_steps", 0)),
            total_steps=int(data.get("total_steps", 0)),
            failed_steps=int(data.get("failed_steps", 0)),
            details=dict(data.get("details", {})),
        )


@dataclass
class Task:
    """Structured Task Representation."""
    task_id: str
    user_request: str
    steps: List[Step] = field(default_factory=list)
    execution_status: ExecutionStatus = ExecutionStatus.PLANNED
    created_at: str = field(default_factory=current_iso_timestamp)
    updated_at: str = field(default_factory=current_iso_timestamp)
    final_report: Optional[FinalReport] = None

    def get_step(self, step_id: str) -> Optional[Step]:
        for step in self.steps:
            if step.step_id == step_id:
                return step
        return None

    def to_dict(self) -> Dict[str, Any]:
        return {
            "task_id": self.task_id,
            "user_request": self.user_request,
            "execution_status": self.execution_status.value,
            "created_at": self.created_at,
            "updated_at": self.updated_at,
            "steps": [s.to_dict() for s in self.steps],
            "final_report": self.final_report.to_dict() if self.final_report else None,
        }

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> Task:
        status_val = data.get("execution_status", ExecutionStatus.PLANNED.value)
        try:
            status = ExecutionStatus(status_val)
        except ValueError:
            status = ExecutionStatus.PLANNED

        return cls(
            task_id=str(data["task_id"]),
            user_request=str(data.get("user_request", "")),
            execution_status=status,
            created_at=str(data.get("created_at", current_iso_timestamp())),
            updated_at=str(data.get("updated_at", current_iso_timestamp())),
            steps=[Step.from_dict(s) for s in data.get("steps", [])],
            final_report=FinalReport.from_dict(data.get("final_report")),
        )
