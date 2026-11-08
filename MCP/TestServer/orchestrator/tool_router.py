"""HTTP Tool Router dispatching directly to existing AIGRAV Bridge (localhost:8765)."""

from __future__ import annotations

import json
import urllib.error
import urllib.request
from dataclasses import dataclass
from typing import Any, Dict, Optional


@dataclass
class ToolExecutionResult:
    """Explicit separation of transport outcome vs Revit tool outcome."""
    transport_success: bool
    tool_success: bool
    raw_result: str
    error_message: Optional[str] = None
    is_transient: bool = False  # e.g., timeout or connection refused


class ToolRouter:
    """Routes tool execution through the official AIGRAV HTTP Bridge."""

    def __init__(self, bridge_url: str = "http://127.0.0.1:8765/", default_timeout: float = 30.0):
        self.bridge_url = bridge_url
        self.default_timeout = default_timeout

    def _get_zwcad_adapter(self):
        if not hasattr(self, "_zwcad_adapter"):
            try:
                import sys
                from pathlib import Path
                aigrav_dir = Path(__file__).resolve().parent.parent.parent
                if str(aigrav_dir) not in sys.path:
                    sys.path.insert(0, str(aigrav_dir))
                import zwcad_tool_adapter
                self._zwcad_adapter = zwcad_tool_adapter.zwcad_adapter
            except Exception:
                self._zwcad_adapter = None
        return self._zwcad_adapter

    def call_tool(
        self,
        tool_name: str,
        argument: Optional[str] = None,
        timeout: Optional[float] = None,
        user_confirmed: bool = False,
        **kwargs: Any,
    ) -> ToolExecutionResult:
        """Call Revit tool via POST http://127.0.0.1:8765/ or route to ZWCAD MCP adapter."""
        # Check if this tool is handled by the ZWCAD MCP adapter
        zwcad_adapter = self._get_zwcad_adapter()
        clean_name = tool_name.replace("zwcad_", "")
        if zwcad_adapter and (tool_name.startswith("zwcad_") or zwcad_adapter.get_tool(clean_name) is not None):
            args_dict: Dict[str, Any] = {}
            if argument:
                try:
                    parsed = json.loads(argument)
                    if isinstance(parsed, dict):
                        args_dict = parsed
                    else:
                        args_dict = {"argument": parsed}
                except Exception:
                    args_dict = {"argument": argument}
            if kwargs:
                args_dict.update(kwargs)

            confirmed = user_confirmed or args_dict.get("confirmed", False) or (args_dict.get("confirmation") in ["CONFIRMED", "DELETE THESE ENTITIES", "CONFIRM_EXPLODE"])
            return zwcad_adapter.call_tool(clean_name, args_dict, user_confirmed=confirmed)

        payload: Dict[str, Any] = {"tool": tool_name}
        if argument is not None:
            payload["argument"] = argument

        effective_timeout = timeout if timeout is not None else self.default_timeout
        data = json.dumps(payload).encode("utf-8")

        request = urllib.request.Request(
            self.bridge_url,
            data=data,
            headers={"Content-Type": "application/json"},
            method="POST",
        )

        try:
            with urllib.request.urlopen(request, timeout=effective_timeout) as response:
                response_data = response.read().decode("utf-8")

                try:
                    result_json = json.loads(response_data)
                except json.JSONDecodeError:
                    return ToolExecutionResult(
                        transport_success=True,
                        tool_success=False,
                        raw_result=response_data,
                        error_message="Bridge returned non-JSON payload.",
                        is_transient=False,
                    )

                # Matching C# AIGRAVHttpResponse schema (Success/Result)
                success = result_json.get(
                    "Success", result_json.get("success", False)
                )
                result_text = result_json.get(
                    "Result", result_json.get("result", "")
                )

                # Check if result_text contains error prefixes returned by catch blocks
                result_str = str(result_text)
                if not success or result_str.startswith("AIGRAV tool execution failed") or result_str.startswith("AIGRAV Error"):
                    return ToolExecutionResult(
                        transport_success=True,
                        tool_success=False,
                        raw_result=result_str,
                        error_message=f"Tool failed: {result_str.splitlines()[0]}",
                        is_transient=False,
                    )

                return ToolExecutionResult(
                    transport_success=True,
                    tool_success=True,
                    raw_result=result_str,
                    error_message=None,
                    is_transient=False,
                )

        except urllib.error.HTTPError as ex:
            return ToolExecutionResult(
                transport_success=False,
                tool_success=False,
                raw_result="",
                error_message=f"HTTP {ex.code}: {ex.reason}",
                is_transient=(ex.code in (408, 502, 503, 504)),
            )

        except urllib.error.URLError as ex:
            # Handles connection refused or network timeouts
            err_str = str(ex.reason) if hasattr(ex, "reason") else str(ex)
            is_timeout = "timed out" in err_str.lower()
            return ToolExecutionResult(
                transport_success=False,
                tool_success=False,
                raw_result="",
                error_message=f"AIGRAV Bridge unavailable: {err_str}",
                is_transient=True,  # Connection failure or timeout is retryable
            )

        except TimeoutError:
            return ToolExecutionResult(
                transport_success=False,
                tool_success=False,
                raw_result="",
                error_message="Request timed out.",
                is_transient=True,
            )

        except Exception as ex:
            return ToolExecutionResult(
                transport_success=False,
                tool_success=False,
                raw_result="",
                error_message=f"Unexpected transport error: {ex}",
                is_transient=False,
            )
