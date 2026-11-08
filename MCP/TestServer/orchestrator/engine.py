"""State machine and execution engine for DAG tasks."""

from __future__ import annotations

import time
from typing import Dict, List, Optional, Set

from .models import (
    ExecutionStatus,
    FinalReport,
    Step,
    StepError,
    Task,
    current_iso_timestamp,
)
from .task_store import TaskStore
from .tool_router import ToolRouter
from .verifier import Verifier


class DependencyCycleError(ValueError):
    """Raised when task steps form a circular dependency."""
    pass


class TaskExecutionEngine:
    """Supervises multi-step DAG execution with persistent state."""

    def __init__(
        self,
        router: ToolRouter,
        store: TaskStore,
        verifier: Optional[Verifier] = None,
    ):
        self.router = router
        self.store = store
        self.verifier = verifier or Verifier(router)

    def validate_task_dag(self, task: Task) -> None:
        """Validate step IDs uniqueness and check for dependency cycles."""
        step_ids: Set[str] = set()
        for step in task.steps:
            if not step.step_id or not step.step_id.strip():
                raise ValueError("Step ID cannot be empty.")
            if step.step_id in step_ids:
                raise ValueError(f"Duplicate step ID detected: {step.step_id}")
            step_ids.add(step.step_id)

        # Check unknown dependencies
        for step in task.steps:
            for dep in step.dependencies:
                if dep not in step_ids:
                    raise ValueError(
                        f"Step '{step.step_id}' references unknown dependency '{dep}'"
                    )

        # Cycle detection using topological DFS
        visited: Dict[str, int] = {}  # 0: unvisited, 1: visiting, 2: visited

        def visit(s_id: str) -> None:
            state = visited.get(s_id, 0)
            if state == 1:
                raise DependencyCycleError(f"Dependency cycle detected involving step '{s_id}'.")
            if state == 2:
                return

            visited[s_id] = 1
            step_obj = task.get_step(s_id)
            if step_obj:
                for d in step_obj.dependencies:
                    visit(d)
            visited[s_id] = 2

        for s_id in step_ids:
            if visited.get(s_id, 0) == 0:
                visit(s_id)

    def execute_task(self, task: Task) -> Task:
        """Execute or resume task steps deterministically."""
        self.validate_task_dag(task)

        task.execution_status = ExecutionStatus.RUNNING
        task.updated_at = current_iso_timestamp()
        self.store.save(task)

        # Main execution loop
        while True:
            # Check completed / failed counts
            all_completed = True
            any_failed = False
            progress_made = False

            for step in task.steps:
                if step.execution_status == ExecutionStatus.FAILED:
                    any_failed = True
                    all_completed = False
                    continue

                if step.execution_status == ExecutionStatus.COMPLETED:
                    continue

                all_completed = False

                # Check if dependencies are satisfied
                deps_met = True
                dep_failed = False
                for dep_id in step.dependencies:
                    dep_step = task.get_step(dep_id)
                    if not dep_step or dep_step.execution_status != ExecutionStatus.COMPLETED:
                        deps_met = False
                    if dep_step and dep_step.execution_status in (
                        ExecutionStatus.FAILED,
                        ExecutionStatus.CANCELLED,
                    ):
                        dep_failed = True

                if dep_failed:
                    # Cascade cancellation
                    step.execution_status = ExecutionStatus.CANCELLED
                    task.updated_at = current_iso_timestamp()
                    self.store.save(task)
                    progress_made = True
                    continue

                if not deps_met:
                    if step.execution_status != ExecutionStatus.WAITING:
                        step.execution_status = ExecutionStatus.WAITING
                        task.updated_at = current_iso_timestamp()
                        self.store.save(task)
                    continue

                # Dependencies are fully met: Transition to READY -> RUNNING
                step.execution_status = ExecutionStatus.READY
                task.updated_at = current_iso_timestamp()
                self.store.save(task)

                self._run_step(task, step)
                progress_made = True

                # If this step failed, stop sequential batching to preserve state
                if step.execution_status == ExecutionStatus.FAILED:
                    any_failed = True
                    break

            if any_failed:
                task.execution_status = ExecutionStatus.FAILED
                break

            if all_completed:
                task.execution_status = ExecutionStatus.COMPLETED
                break

            if not progress_made:
                # Deadlock or unresolvable state
                task.execution_status = ExecutionStatus.FAILED
                break

        # Generate final report
        completed_count = sum(
            1 for s in task.steps if s.execution_status == ExecutionStatus.COMPLETED
        )
        failed_count = sum(
            1 for s in task.steps if s.execution_status == ExecutionStatus.FAILED
        )

        task.final_report = FinalReport(
            success=(task.execution_status == ExecutionStatus.COMPLETED),
            summary=(
                "All steps executed and verified successfully."
                if task.execution_status == ExecutionStatus.COMPLETED
                else f"Task halted. {failed_count} step(s) failed."
            ),
            completed_steps=completed_count,
            total_steps=len(task.steps),
            failed_steps=failed_count,
        )
        task.updated_at = current_iso_timestamp()
        self.store.save(task)
        return task

    def _run_step(self, task: Task, step: Step) -> None:
        """Execute a single step with bounded retries and verification."""
        while step.retry_count <= step.max_retries:
            step.execution_status = ExecutionStatus.RUNNING
            task.updated_at = current_iso_timestamp()
            self.store.save(task)

            start_t = time.time()
            res = self.router.call_tool(
                tool_name=step.tool_name,
                argument=step.argument,
                timeout=step.timeout_seconds,
            )
            duration_ms = int((time.time() - start_t) * 1000)

            # Check transport & tool outcome
            if not res.transport_success or not res.tool_success:
                err_type = "TRANSIENT_TRANSPORT" if res.is_transient else "TOOL_ERROR"
                err_msg = res.error_message or "Unknown failure"

                step.errors.append(
                    StepError(
                        attempt=step.retry_count + 1,
                        error_type=err_type,
                        message=err_msg,
                    )
                )

                # Retry policy: only retry transient failures within max_retries limit
                if res.is_transient and step.retry_count < step.max_retries:
                    step.retry_count += 1
                    task.updated_at = current_iso_timestamp()
                    self.store.save(task)
                    time.sleep(0.5 * (2 ** (step.retry_count - 1)))  # Backoff
                    continue
                else:
                    step.execution_status = ExecutionStatus.FAILED
                    task.updated_at = current_iso_timestamp()
                    self.store.save(task)
                    return

            # Tool call executed successfully: Transition to VERIFYING
            step.execution_status = ExecutionStatus.VERIFYING
            task.updated_at = current_iso_timestamp()
            self.store.save(task)

            verif_res = self.verifier.verify_step(
                step=step,
                execution_output={
                    "raw_result": res.raw_result,
                    "duration_ms": duration_ms,
                },
            )

            if not verif_res.verified:
                step.errors.append(
                    StepError(
                        attempt=step.retry_count + 1,
                        error_type="VERIFICATION_FAILED",
                        message=verif_res.reason,
                    )
                )
                # Verification failures are logical; do not auto-retry identically
                step.execution_status = ExecutionStatus.FAILED
                task.updated_at = current_iso_timestamp()
                self.store.save(task)
                return

            # Verification passed: Freeze step output and mark COMPLETED
            step.output = {
                "raw_result": res.raw_result,
                "verified": True,
                "verification_reason": verif_res.reason,
                "duration_ms": duration_ms,
            }
            step.execution_status = ExecutionStatus.COMPLETED
            task.updated_at = current_iso_timestamp()
            self.store.save(task)
            return

    def get_task(self, task_id: str) -> Optional[Task]:
        """Retrieve authoritative task state from store."""
        return self.store.load(task_id)

    def pause_task(self, task_id: str) -> Optional[Task]:
        """Safely pause a task at the current checkpoint without interrupting in-flight Revit transactions."""
        task = self.store.load(task_id)
        if not task:
            return None
        if task.execution_status in (
            ExecutionStatus.RUNNING,
            ExecutionStatus.READY,
            ExecutionStatus.PLANNED,
            ExecutionStatus.WAITING,
        ):
            task.execution_status = ExecutionStatus.PAUSED
            task.updated_at = current_iso_timestamp()
            self.store.save(task)
        return task

    def resume_task(self, task_id: str) -> Optional[Task]:
        """Resume execution of a paused or uncompleted task."""
        task = self.store.load(task_id)
        if not task:
            return None
        if task.execution_status in (
            ExecutionStatus.PAUSED,
            ExecutionStatus.WAITING,
            ExecutionStatus.FAILED,
            ExecutionStatus.PLANNED,
            ExecutionStatus.READY,
        ):
            return self.execute_task(task)
        return task

    def cancel_task(self, task_id: str) -> Optional[Task]:
        """Cancel a task and mark all remaining non-completed steps as CANCELLED."""
        task = self.store.load(task_id)
        if not task:
            return None
        if task.execution_status != ExecutionStatus.COMPLETED:
            for step in task.steps:
                if step.execution_status not in (
                    ExecutionStatus.COMPLETED,
                    ExecutionStatus.FAILED,
                ):
                    step.execution_status = ExecutionStatus.CANCELLED
            task.execution_status = ExecutionStatus.CANCELLED
            task.updated_at = current_iso_timestamp()
            self.store.save(task)
        return task
