"""LLM adapter layer for the AIGRAV Decision & Review Engine.

Design rules:
- Standard library only (urllib); no new dependencies.
- Provider auto-detected from environment variables. Keys are read at call time,
  never logged, never returned, never persisted.
- The LLM only produces *proposals* (JSON). Safety gates are enforced afterwards
  by DecisionReviewEngine; the LLM cannot approve, verify, or supply model data.

Environment variables (all optional):
  AIGRAV_LLM_PROVIDER   : "gemini" | "openai_compatible" | "none"
  AIGRAV_LLM_MODEL      : model name override
  GEMINI_API_KEY / GOOGLE_API_KEY            -> gemini REST
  AIGRAV_LLM_BASE_URL (+ optional AIGRAV_LLM_API_KEY / OPENAI_API_KEY)
                                              -> OpenAI-compatible /chat/completions
                                                 (OpenAI, LM Studio, Ollama /v1, ...)
"""

from __future__ import annotations

import json
import os
import re
import urllib.error
import urllib.request
from typing import Any, Dict, List, Optional


class LLMUnavailableError(RuntimeError):
    """Raised when no LLM is configured or the provider call fails."""


class LLMAdapter:
    name = "base"

    def is_available(self) -> bool:
        return False

    def complete_json(self, system_prompt: str, user_prompt: str, timeout: float = 30.0) -> Dict[str, Any]:
        raise LLMUnavailableError("No LLM adapter configured.")


class NullLLMAdapter(LLMAdapter):
    """Explicit 'no LLM' adapter. Always unavailable."""

    name = "none"

    def __init__(self, reason: str = "No LLM provider configured in the AIGRAV environment."):
        self.reason = reason

    def complete_json(self, system_prompt: str, user_prompt: str, timeout: float = 30.0) -> Dict[str, Any]:
        raise LLMUnavailableError(self.reason)


def _extract_json(text: str) -> Dict[str, Any]:
    """Parse a JSON object from model text (tolerates ```json fences)."""
    cleaned = text.strip()
    fence = re.search(r"```(?:json)?\s*(\{.*\})\s*```", cleaned, re.DOTALL)
    if fence:
        cleaned = fence.group(1)
    else:
        start, end = cleaned.find("{"), cleaned.rfind("}")
        if start != -1 and end > start:
            cleaned = cleaned[start:end + 1]
    data = json.loads(cleaned)
    if not isinstance(data, dict):
        raise ValueError("LLM output is not a JSON object.")
    return data


def _post_json(url: str, body: Dict[str, Any], headers: Dict[str, str], timeout: float) -> Dict[str, Any]:
    req = urllib.request.Request(
        url,
        data=json.dumps(body).encode("utf-8"),
        headers={"Content-Type": "application/json", **headers},
        method="POST",
    )
    try:
        with urllib.request.urlopen(req, timeout=timeout) as resp:
            return json.loads(resp.read().decode("utf-8"))
    except urllib.error.HTTPError as ex:
        # Never include request headers (they may contain keys) in the message.
        raise LLMUnavailableError(f"LLM HTTP error {ex.code}: {ex.reason}") from None
    except (urllib.error.URLError, TimeoutError) as ex:
        raise LLMUnavailableError(f"LLM endpoint unreachable: {getattr(ex, 'reason', ex)}") from None


class GeminiRestAdapter(LLMAdapter):
    """Google Gemini via REST generateContent (UNTESTED live: no key in this environment)."""

    name = "gemini"

    def __init__(self, model: Optional[str] = None):
        self.model = model or os.environ.get("AIGRAV_LLM_MODEL", "gemini-2.5-flash")

    @staticmethod
    def _key() -> Optional[str]:
        return os.environ.get("GEMINI_API_KEY") or os.environ.get("GOOGLE_API_KEY")

    def is_available(self) -> bool:
        return bool(self._key())

    def complete_json(self, system_prompt: str, user_prompt: str, timeout: float = 30.0) -> Dict[str, Any]:
        key = self._key()
        if not key:
            raise LLMUnavailableError("GEMINI_API_KEY / GOOGLE_API_KEY not set.")
        url = f"https://generativelanguage.googleapis.com/v1beta/models/{self.model}:generateContent"
        body = {
            "systemInstruction": {"parts": [{"text": system_prompt}]},
            "contents": [{"role": "user", "parts": [{"text": user_prompt}]}],
            "generationConfig": {"responseMimeType": "application/json", "temperature": 0.2},
        }
        data = _post_json(url, body, {"x-goog-api-key": key}, timeout)
        try:
            text = data["candidates"][0]["content"]["parts"][0]["text"]
        except (KeyError, IndexError, TypeError):
            raise LLMUnavailableError("Gemini response missing candidate text.") from None
        return _extract_json(text)


class OpenAICompatibleAdapter(LLMAdapter):
    """OpenAI-compatible /chat/completions (UNTESTED live: no endpoint in this environment)."""

    name = "openai_compatible"

    def __init__(self, base_url: Optional[str] = None, model: Optional[str] = None):
        self.base_url = (base_url or os.environ.get("AIGRAV_LLM_BASE_URL", "")).rstrip("/")
        self.model = model or os.environ.get("AIGRAV_LLM_MODEL", "gpt-4o-mini")

    def is_available(self) -> bool:
        return bool(self.base_url)

    def complete_json(self, system_prompt: str, user_prompt: str, timeout: float = 30.0) -> Dict[str, Any]:
        if not self.base_url:
            raise LLMUnavailableError("AIGRAV_LLM_BASE_URL not set.")
        key = os.environ.get("AIGRAV_LLM_API_KEY") or os.environ.get("OPENAI_API_KEY")
        headers = {"Authorization": f"Bearer {key}"} if key else {}
        body = {
            "model": self.model,
            "temperature": 0.2,
            "response_format": {"type": "json_object"},
            "messages": [
                {"role": "system", "content": system_prompt},
                {"role": "user", "content": user_prompt},
            ],
        }
        data = _post_json(f"{self.base_url}/chat/completions", body, headers, timeout)
        try:
            text = data["choices"][0]["message"]["content"]
        except (KeyError, IndexError, TypeError):
            raise LLMUnavailableError("LLM response missing message content.") from None
        return _extract_json(text)


def detect_llm_adapter() -> LLMAdapter:
    """Pick a provider from env. Returns NullLLMAdapter when nothing is configured."""
    provider = os.environ.get("AIGRAV_LLM_PROVIDER", "").strip().lower()
    if provider == "none":
        return NullLLMAdapter("LLM disabled via AIGRAV_LLM_PROVIDER=none.")
    candidates: List[LLMAdapter] = []
    if provider in ("", "gemini"):
        candidates.append(GeminiRestAdapter())
    if provider in ("", "openai_compatible"):
        candidates.append(OpenAICompatibleAdapter())
    for adapter in candidates:
        if adapter.is_available():
            return adapter
    return NullLLMAdapter()


# ---------------------------------------------------------------------------
# Prompt contract shared with DecisionReviewEngine
# ---------------------------------------------------------------------------

LLM_SYSTEM_PROMPT = """You are the review stage of AIGRAV, a BIM agent operating Autodesk Revit.
You DO NOT execute anything and you DO NOT know model data beyond the CONTEXT given.
Return ONE JSON object with exactly these keys:
{
  "requested_outcome": str,
  "target_elements": [str],
  "operations": [str],
  "is_mutating": bool,
  "is_destructive": bool,
  "ambiguities": [str],
  "clarification_questions": [str],
  "unverified_assumptions": [str],
  "risks": [str],
  "proposed_outcome": "READY"|"CLARIFICATION_REQUIRED"|"RECOMMENDATION_REQUIRED"|"APPROVAL_REQUIRED"|"UNSUPPORTED"|"BLOCKED",
  "reason": str,
  "recommended_option": {"title": str, "description": str, "advantages": [str], "disadvantages": [str], "risks": [str]} | null,
  "alternative_options": [same shape],
  "plan_steps": [{"step_id": str, "title": str, "tool_name": str, "argument": str|null,
                  "expected_outcome": str,
                  "verification": {"type": "NON_EMPTY"} | {"type": "FOLLOW_UP_QUERY", "follow_up_tool": str, "follow_up_argument": str|null, "expected_value": str|null}}]
}
Rules:
- Use ONLY tool names from ALLOWED_TOOLS. If the task needs anything else, say UNSUPPORTED.
- Never invent element IDs, counts, parameter values or project data. Unknown facts go to unverified_assumptions.
- Any create/modify/delete operation must set is_mutating=true (and is_destructive=true for deletion).
- Ask clarification only for information that is essential; do not ask questions for clear read-only requests.
- Recommendations are suggestions, not verified engineering rules."""
