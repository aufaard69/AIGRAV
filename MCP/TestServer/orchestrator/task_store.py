"""Atomic JSON persistence for task state and history."""

from __future__ import annotations

import json
import os
import re
import tempfile
from pathlib import Path
from typing import List, Optional

from .models import Task


# Sensitive pattern keywords to scrub before writing to disk
SENSITIVE_PATTERNS = [
    (re.compile(r"(api[_-]?key\s*[:=]\s*['\"]?)([^'\";\s]+)(['\"]?)", re.IGNORECASE), r"\1***REDACTED***\3"),
    (re.compile(r"(bearer\s+)([a-zA-Z0-9_\-\.]+)", re.IGNORECASE), r"\1***REDACTED***"),
    (re.compile(r"(password\s*[:=]\s*['\"]?)([^'\";\s]+)(['\"]?)", re.IGNORECASE), r"\1***REDACTED***\3"),
    (re.compile(r"(secret\s*[:=]\s*['\"]?)([^'\";\s]+)(['\"]?)", re.IGNORECASE), r"\1***REDACTED***\3"),
]


def scrub_sensitive_data(text: str) -> str:
    """Mask tokens, secrets, or keys in log strings."""
    scrubbed = text
    for pat, repl in SENSITIVE_PATTERNS:
        scrubbed = pat.sub(repl, scrubbed)
    return scrubbed


def sanitize_task_id(task_id: str) -> str:
    """Ensure safe path naming for task file storage."""
    safe = re.sub(r"[^a-zA-Z0-9_\-]", "_", task_id)
    return safe or "unnamed_task"


class TaskStore:
    """Thread-safe and atomic file-based task repository."""

    def __init__(self, storage_dir: Optional[str] = None):
        if storage_dir is None:
            # Default to C:\AIGRAV\KnowledgeBase\TaskHistory\
            base_dir = Path(__file__).resolve().parent.parent.parent.parent
            self.storage_dir = base_dir / "KnowledgeBase" / "TaskHistory"
        else:
            self.storage_dir = Path(storage_dir)

        self.storage_dir.mkdir(parents=True, exist_ok=True)

    def _get_task_path(self, task_id: str) -> Path:
        safe_id = sanitize_task_id(task_id)
        return self.storage_dir / f"{safe_id}.json"

    def save(self, task: Task) -> Path:
        """Atomically persist task state as formatted JSON."""
        target_path = self._get_task_path(task.task_id)
        raw_dict = task.to_dict()
        json_str = json.dumps(raw_dict, indent=2, ensure_ascii=False)
        scrubbed_json = scrub_sensitive_data(json_str)

        # Atomic write: write to temp file on same volume, then replace
        temp_fd, temp_path_str = tempfile.mkstemp(
            prefix=f"task_{sanitize_task_id(task.task_id)}_",
            suffix=".tmp",
            dir=str(self.storage_dir),
        )
        try:
            with os.fdopen(temp_fd, "w", encoding="utf-8") as f:
                f.write(scrubbed_json)
                f.flush()
                os.fsync(f.fileno())

            # Atomic replace on Windows/POSIX
            os.replace(temp_path_str, target_path)
        except Exception:
            if os.path.exists(temp_path_str):
                try:
                    os.remove(temp_path_str)
                except OSError:
                    pass
            raise

        return target_path

    def load(self, task_id: str) -> Optional[Task]:
        """Load a task by ID from storage."""
        target_path = self._get_task_path(task_id)
        if not target_path.exists():
            return None

        try:
            with open(target_path, "r", encoding="utf-8") as f:
                data = json.load(f)
            return Task.from_dict(data)
        except (json.JSONDecodeError, OSError):
            return None

    def exists(self, task_id: str) -> bool:
        """Check if task checkpoint exists."""
        return self._get_task_path(task_id).exists()

    def list_task_ids(self) -> List[str]:
        """List all saved task IDs."""
        task_ids = []
        for file_path in self.storage_dir.glob("*.json"):
            task_ids.append(file_path.stem)
        return sorted(task_ids)
