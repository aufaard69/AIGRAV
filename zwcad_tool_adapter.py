"""
zwcad_tool_adapter.py - AIGRAV ZWCAD MCP Tool Adapter & Registry (Phase 1)
Discovers, catalogs, validates, and invokes tools exposed by the 'zwcad-2023' MCP server.
Provides tool metadata: name, schema, description, risk category, and read/write classification.
"""

from __future__ import annotations

import json
import os
import sys
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Callable, Dict, List, Optional

# Ensure project paths are available
ZWCAD_PROJECT_DIR = Path(r"C:\ZWCAD-MCP")
MCP_SCHEMAS_DIR = Path(r"C:\Users\sapuser\.gemini\antigravity\mcp\zwcad-2023")

if str(ZWCAD_PROJECT_DIR) not in sys.path:
    sys.path.insert(0, str(ZWCAD_PROJECT_DIR))

# Import security policy
from cad_security_policy import CADSecurityPolicy, CADRiskCategory, CADReadWriteType

# Import ToolExecutionResult model contract from AIGRAV orchestrator
try:
    from orchestrator.tool_router import ToolExecutionResult
except ImportError:
    @dataclass
    class ToolExecutionResult:
        transport_success: bool
        tool_success: bool
        raw_result: str
        error_message: Optional[str] = None
        is_transient: bool = False


@dataclass
class ZWCADToolDefinition:
    """Metadata schema definition for an exposed ZWCAD tool."""
    name: str
    description: str
    input_schema: Dict[str, Any]
    risk_category: CADRiskCategory
    read_write: CADReadWriteType
    callable_func: Optional[Callable] = None


class ZWCADToolRegistry:
    """Registry discovering and managing all native ZWCAD tools."""

    def __init__(self, schemas_dir: Path = MCP_SCHEMAS_DIR):
        self.schemas_dir = schemas_dir
        self.tools: Dict[str, ZWCADToolDefinition] = {}
        self._server_module = None
        self._discover_and_register_tools()

    def _get_server_module(self):
        if self._server_module is None:
            try:
                import importlib.util
                server_py = ZWCAD_PROJECT_DIR / "server.py"
                if server_py.exists():
                    spec = importlib.util.spec_from_file_location("zwcad_mcp_server", str(server_py))
                    if spec and spec.loader:
                        mod = importlib.util.module_from_spec(spec)
                        # Ensure ZWCAD_PROJECT_DIR is first in sys.path during exec
                        if str(ZWCAD_PROJECT_DIR) not in sys.path:
                            sys.path.insert(0, str(ZWCAD_PROJECT_DIR))
                        spec.loader.exec_module(mod)
                        self._server_module = mod
            except Exception as e:
                self._server_module = None
        return self._server_module

    def _discover_and_register_tools(self) -> None:
        """Scan schema directory and register every discovered ZWCAD tool."""
        server_mod = self._get_server_module()

        if self.schemas_dir.exists():
            for json_path in sorted(self.schemas_dir.glob("*.json")):
                try:
                    with open(json_path, "r", encoding="utf-8") as f:
                        data = json.load(f)

                    name = data.get("name", json_path.stem)
                    desc = data.get("description", "")
                    schema = data.get("parameters", {})

                    risk = CADSecurityPolicy.get_tool_risk(name)
                    rw = CADSecurityPolicy.get_read_write(name)

                    func = getattr(server_mod, name, None) if server_mod else None

                    self.tools[name] = ZWCADToolDefinition(
                        name=name,
                        description=desc,
                        input_schema=schema,
                        risk_category=risk,
                        read_write=rw,
                        callable_func=func
                    )
                except Exception as ex:
                    print(f"[ZWCAD_REGISTRY_WARN] Failed to load schema {json_path}: {ex}", file=sys.stderr)

    def get_tool(self, tool_name: str) -> Optional[ZWCADToolDefinition]:
        clean_name = tool_name.replace("zwcad_", "")
        return self.tools.get(clean_name) or self.tools.get(tool_name)

    def list_tools(self) -> List[Dict[str, Any]]:
        """Return catalog of all registered tools with schemas and risk classifications."""
        catalog = []
        for t in self.tools.values():
            catalog.append({
                "name": t.name,
                "description": t.description,
                "risk_category": t.risk_category.value,
                "read_write": t.read_write.value,
                "input_schema": t.input_schema
            })
        return catalog

    def call_tool(
        self,
        tool_name: str,
        arguments: Optional[Dict[str, Any]] = None,
        user_confirmed: bool = False
    ) -> ToolExecutionResult:
        """
        Execute tool call using argument schema validation and risk gates.
        Preserves original tool response and errors.
        """
        clean_name = tool_name.replace("zwcad_", "")
        tool_def = self.get_tool(clean_name)

        if not tool_def:
            return ToolExecutionResult(
                transport_success=False,
                tool_success=False,
                raw_result="",
                error_message=f"ZWCAD tool '{tool_name}' is not recognized in registry.",
                is_transient=False
            )

        args = arguments or {}

        # 1. Enforce safety policy
        try:
            CADSecurityPolicy.validate_execution_permission(clean_name, args, user_confirmed=user_confirmed)
        except PermissionError as pe:
            return ToolExecutionResult(
                transport_success=True,
                tool_success=False,
                raw_result="",
                error_message=f"Security gate blocked: {pe}",
                is_transient=False
            )

        # 2. Invoke tool
        func = tool_def.callable_func
        if not func:
            server_mod = self._get_server_module()
            func = getattr(server_mod, clean_name, None) if server_mod else None

        if not func:
            return ToolExecutionResult(
                transport_success=False,
                tool_success=False,
                raw_result="",
                error_message=f"Callable function for '{clean_name}' not resolved in ZWCAD bridge.",
                is_transient=False
            )

        try:
            # Call function with kwargs in isolated desktop thread for universal COM ROT access
            result_box: Dict[str, Any] = {}
            def _worker():
                try:
                    import ctypes
                    user32 = ctypes.windll.user32
                    h_default = user32.OpenDesktopW('Default', 0, False, 0x0100 | 0x0040 | 0x0001 | 0x0002 | 0x0004 | 0x0008 | 0x0010 | 0x0020)
                    if h_default:
                        user32.SetThreadDesktop(h_default)
                except Exception:
                    pass
                try:
                    result_box["val"] = func(**args)
                    result_box["ok"] = True
                except Exception as exc:
                    result_box["ok"] = False
                    result_box["error"] = exc

            import threading
            t = threading.Thread(target=_worker)
            t.start()
            t.join()

            if not result_box.get("ok"):
                raise result_box.get("error", RuntimeError("Thread execution failed without error object"))

            raw_output = result_box["val"]
            output_str = str(raw_output)

            # Check if JSON payload indicated ok: false
            try:
                parsed = json.loads(output_str)
                tool_ok = parsed.get("ok", True)
                err_msg = parsed.get("error") if not tool_ok else None
            except Exception:
                tool_ok = True
                err_msg = None

            return ToolExecutionResult(
                transport_success=True,
                tool_success=tool_ok,
                raw_result=output_str,
                error_message=err_msg,
                is_transient=False
            )

        except TypeError as te:
            return ToolExecutionResult(
                transport_success=True,
                tool_success=False,
                raw_result="",
                error_message=f"Argument schema mismatch for '{clean_name}': {te}",
                is_transient=False
            )
        except Exception as ex:
            return ToolExecutionResult(
                transport_success=False,
                tool_success=False,
                raw_result="",
                error_message=f"ZWCAD execution error in '{clean_name}': {ex}",
                is_transient=False
            )


# Global singleton adapter
zwcad_adapter = ZWCADToolRegistry()
