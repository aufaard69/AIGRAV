"""
Antigravity Agent Client for AIGRAV Voice HUD
Programmatic Python interface leveraging the official Antigravity Language Server runtime
and active desktop subscription authentication (Zero API keys, Zero additional costs).
"""

import os
import sys
import json
import time
import subprocess
from pathlib import Path
from typing import Any, Dict, List, Optional, Tuple

AGENTAPI_BAT = r"C:\Users\sapuser\.gemini\antigravity\bin\agentapi.bat"
BRAIN_DIR = Path(r"C:\Users\sapuser\.gemini\antigravity\brain")

class AntigravityAgentClient:
    """Client for spawning and conversing with Antigravity AI agents programmatically."""

    def __init__(self, agentapi_path: str = AGENTAPI_BAT, brain_dir: Path = BRAIN_DIR):
        self.agentapi_path = agentapi_path
        self.brain_dir = brain_dir
        if not os.path.exists(self.agentapi_path):
            raise FileNotFoundError(f"agentapi binary not found at: {self.agentapi_path}")

    def create_conversation(
        self,
        prompt: str,
        title: str = "AIGRAV Voice Session",
        model: str = "flash_lite"  # "flash_lite", "flash", "pro"
    ) -> Tuple[str, str]:
        """
        Start a new Antigravity agent conversation using active subscription entitlement.
        Returns: (conversation_id, initial_response_text)
        """
        cmd = [
            self.agentapi_path,
            "new-conversation",
            f"--model={model}",
            f"--title={title}",
            prompt
        ]
        
        proc = subprocess.run(
            cmd,
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace"
        )
        if proc.returncode != 0:
            raise RuntimeError(f"agentapi new-conversation failed: {proc.stderr or proc.stdout}")

        try:
            data = json.loads(proc.stdout)
            conv_id = data["response"]["newConversation"]["conversationId"]
        except Exception as e:
            raise RuntimeError(f"Failed to parse newConversation JSON: {proc.stdout} ({e})")

        # Wait for agent's initial response
        response_text = self._wait_for_latest_response(conv_id, start_step_index=0)
        return conv_id, response_text

    def send_message(
        self,
        conversation_id: str,
        content: str,
        title: Optional[str] = None,
        timeout_seconds: float = 30.0
    ) -> str:
        """
        Send a follow-up turn to the active conversation, maintaining full multi-turn context.
        Returns: assistant response text.
        """
        # Determine the current last step index before sending
        current_step = self._get_latest_step_index(conversation_id)

        cmd = [self.agentapi_path, "send-message"]
        if title:
            cmd.append(f"--title={title}")
        cmd.extend([conversation_id, content])

        proc = subprocess.run(
            cmd,
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace"
        )
        if proc.returncode != 0:
            raise RuntimeError(f"agentapi send-message failed: {proc.stderr or proc.stdout}")

        # Wait for agent's turn to complete
        response_text = self._wait_for_latest_response(
            conversation_id,
            start_step_index=current_step + 1,
            timeout_seconds=timeout_seconds
        )
        return response_text

    def _get_transcript_path(self, conversation_id: str) -> Path:
        return self.brain_dir / conversation_id / ".system_generated" / "logs" / "transcript.jsonl"

    def _get_latest_step_index(self, conversation_id: str) -> int:
        trans_path = self._get_transcript_path(conversation_id)
        if not trans_path.exists():
            return -1
        max_step = -1
        try:
            with open(trans_path, "r", encoding="utf-8", errors="replace") as f:
                for line in f:
                    line_str = line.strip()
                    if line_str:
                        entry = json.loads(line_str)
                        s = entry.get("step_index", -1)
                        if s > max_step:
                            max_step = s
        except Exception:
            pass
        return max_step

    def _wait_for_latest_response(
        self,
        conversation_id: str,
        start_step_index: int = 0,
        timeout_seconds: float = 30.0
    ) -> str:
        """Poll the atomic transcript log until the MODEL generates its final response."""
        trans_path = self._get_transcript_path(conversation_id)
        deadline = time.time() + timeout_seconds

        last_planner_content = ""
        while time.time() < deadline:
            if trans_path.exists():
                try:
                    entries = []
                    with open(trans_path, "r", encoding="utf-8", errors="replace") as f:
                        for line in f:
                            if line.strip():
                                entries.append(json.loads(line.strip()))

                    # Look for the last MODEL PLANNER_RESPONSE created at or after start_step_index
                    for entry in reversed(entries):
                        s_idx = entry.get("step_index", -1)
                        if s_idx >= start_step_index and entry.get("source") == "MODEL" and entry.get("type") == "PLANNER_RESPONSE":
                            content = entry.get("content", "")
                            # If it executed tools, wait until a following PLANNER_RESPONSE with final text exists
                            if content:
                                last_planner_content = content
                                return content
                except Exception:
                    pass

            time.sleep(0.5)

        if last_planner_content:
            return last_planner_content
        raise TimeoutError(f"Timed out waiting for Antigravity response in conversation {conversation_id}")
