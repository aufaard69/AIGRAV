"""
test_zwcad_integration.py - AIGRAV Native ZWCAD Integration Test Suite (Phase 7)

Comprehensive automated test harness covering:
1. Tool discovery and registration (catalog count == 44, schemas, risk levels, read/write classification).
2. Calling read-only tools: diagnose_cad, list_layers, query_entities.
3. Creating and verifying native Line and Polyline entities.
4. Creating native linear dimension (AcDbRotatedDimension) and strictly verifying entity type.
5. Running native CAD commands (execute_command) with prompt tracking and completion verification.
6. Handling unsupported commands, destructive commands without confirmation, and invalid inputs.
7. Ensuring existing AIGRAV/Revit MCP tool routing and behavior remain completely unaffected.
"""

from __future__ import annotations

import json
import math
import sys
import unittest
from pathlib import Path
from typing import List

# Ensure project paths
AIGRAV_DIR = Path(r"C:\AIGRAV")
ZWCAD_DIR = Path(r"C:\ZWCAD-MCP")
TESTSERVER_DIR = Path(r"C:\AIGRAV\MCP\TestServer")

for p in (AIGRAV_DIR, ZWCAD_DIR, TESTSERVER_DIR):
    if str(p) not in sys.path:
        sys.path.insert(0, str(p))

from cad_security_policy import CADSecurityPolicy, CADRiskCategory, CADReadWriteType
from zwcad_tool_adapter import ZWCADToolRegistry, zwcad_adapter
from cad_drawing_planner import CADDrawingPlanner, CADOperation
from cad_verifier import CADVerifier
from orchestrator.tool_router import ToolRouter, ToolExecutionResult


class TestZWCADIntegration(unittest.TestCase):
    """Phase 7 Integration Tests for Native ZWCAD 2023 Tools and AIGRAV Router."""

    created_handles: List[str] = []

    @classmethod
    def setUpClass(cls):
        cls.adapter = zwcad_adapter
        cls.router = ToolRouter()
        cls.verifier = CADVerifier(cls.adapter)
        cls.created_handles = []

    @classmethod
    def tearDownClass(cls):
        """Clean up any test entities created during the test run."""
        if cls.created_handles:
            try:
                cls.adapter.call_tool(
                    "delete_entities",
                    {"handles": cls.created_handles, "confirmation": "DELETE THESE ENTITIES"},
                    user_confirmed=True
                )
            except Exception:
                pass

    # -------------------------------------------------------------
    # 1. Tool Discovery and Registration
    # -------------------------------------------------------------
    def test_01_tool_discovery_and_registration(self):
        """Discover and verify all tools exposed by zwcad-2023."""
        tools = self.adapter.list_tools()
        self.assertGreaterEqual(len(tools), 44, "Expected at least 44 registered tools in catalog.")

        tool_names = {t["name"] for t in tools}
        expected_core = [
            "diagnose_cad", "get_current_drawing", "list_layers", "query_entities",
            "create_line", "create_polyline", "create_rectangle", "create_circle", "create_arc",
            "create_linear_dimension", "create_aligned_dimension", "create_radial_dimension",
            "create_hatch", "create_block", "insert_block", "execute_command", "delete_entities"
        ]
        for exp in expected_core:
            self.assertIn(exp, tool_names, f"Expected tool '{exp}' to be registered.")

        for t in tools:
            self.assertIn("name", t)
            self.assertIn("risk_category", t)
            self.assertIn("read_write", t)
            self.assertIn("input_schema", t)
            self.assertIsInstance(t["input_schema"], dict)

    # -------------------------------------------------------------
    # 2. Calling diagnose_cad, list_layers, and query_entities
    # -------------------------------------------------------------
    def test_02_read_only_tools(self):
        """Invoke diagnose_cad, list_layers, and query_entities via ToolRouter."""
        # 2a. diagnose_cad
        res_diag = self.router.call_tool("diagnose_cad")
        self.assertTrue(res_diag.transport_success, f"diagnose_cad transport failed: {res_diag.error_message}")
        self.assertTrue(res_diag.tool_success, f"diagnose_cad failed: {res_diag.error_message}")
        diag_data = json.loads(res_diag.raw_result)
        self.assertTrue(diag_data.get("ok"))
        self.assertEqual(diag_data.get("version"), "2023")

        # 2b. list_layers
        res_layers = self.router.call_tool("list_layers")
        self.assertTrue(res_layers.tool_success, f"list_layers failed: {res_layers.error_message}")
        layers_data = json.loads(res_layers.raw_result)
        self.assertTrue(layers_data.get("ok"))
        self.assertGreaterEqual(layers_data.get("count", 0), 1)

        # 2c. query_entities
        res_query = self.router.call_tool("query_entities", argument=json.dumps({"limit": 10}))
        self.assertTrue(res_query.tool_success, f"query_entities failed: {res_query.error_message}")
        query_data = json.loads(res_query.raw_result)
        self.assertTrue(query_data.get("ok"))
        self.assertIn("entities", query_data)

    # -------------------------------------------------------------
    # 3. Creating and Verifying Native Line and Polyline
    # -------------------------------------------------------------
    def test_03_create_and_verify_line_and_polyline(self):
        """Create native line and polyline and verify their COM entity types."""
        # Line
        line_res = self.router.call_tool(
            "create_line",
            argument=json.dumps({"x1": 80000.0, "y1": 80000.0, "x2": 82000.0, "y2": 80000.0, "layer": "0"})
        )
        self.assertTrue(line_res.tool_success, f"create_line failed: {line_res.error_message}")
        line_data = json.loads(line_res.raw_result)
        h_line = line_data["handle"]
        self.created_handles.append(h_line)

        # Polyline
        pline_res = self.router.call_tool(
            "create_polyline",
            argument=json.dumps({
                "points": [[80000.0, 80000.0], [82000.0, 80000.0], [82000.0, 81000.0], [80000.0, 81000.0]],
                "closed": True,
                "layer": "0"
            })
        )
        self.assertTrue(pline_res.tool_success, f"create_polyline failed: {pline_res.error_message}")
        pline_data = json.loads(pline_res.raw_result)
        h_pline = pline_data["handle"]
        self.created_handles.append(h_pline)

        # Verify handles and ObjectNames
        prop_line = json.loads(self.adapter.call_tool("get_entity_properties", {"handle": h_line}).raw_result)["properties"]
        prop_pline = json.loads(self.adapter.call_tool("get_entity_properties", {"handle": h_pline}).raw_result)["properties"]

        self.assertEqual(prop_line.get("objectname"), "AcDbLine")
        self.assertIn("polyline", prop_pline.get("objectname", "").lower())

    # -------------------------------------------------------------
    # 4. Creating Native Linear Dimension & Strict Type Verification
    # -------------------------------------------------------------
    def test_04_create_and_verify_native_dimension(self):
        """Create native AcDbRotatedDimension and verify it is not lines/text."""
        dim_res = self.router.call_tool(
            "create_linear_dimension",
            argument=json.dumps({
                "x1": 80000.0, "y1": 80000.0,
                "x2": 82000.0, "y2": 80000.0,
                "dim_line_x": 81000.0, "dim_line_y": 79500.0,
                "rotation_rad": 0.0,
                "layer": "0",
                "text_override": "2000"
            })
        )
        self.assertTrue(dim_res.tool_success, f"create_linear_dimension failed: {dim_res.error_message}")
        dim_data = json.loads(dim_res.raw_result)
        h_dim = dim_data["handle"]
        self.created_handles.append(h_dim)

        self.assertEqual(dim_data.get("objectname"), "AcDbRotatedDimension")
        self.assertEqual(dim_data.get("measurement"), 2000.0)

        # Pass through CADVerifier
        op = CADOperation(
            op_id="TEST-DIM-01",
            tool_name="create_linear_dimension",
            entity_type="AcDbRotatedDimension",
            arguments={},
            layer="0",
            description="Test Dimension"
        )
        ver_res = self.verifier.verify_single_operation(op, dim_res.raw_result)
        self.assertTrue(ver_res.verified, f"Dimension verification failed: {ver_res.error_reason}")
        self.assertNotIn(ver_res.actual_type, ("AcDbLine", "AcDbText"), "Dimension must NOT be emulated lines/text!")

    # -------------------------------------------------------------
    # 5. Running Native Command with Prompt Tracking
    # -------------------------------------------------------------
    def test_05_native_command_execution(self):
        """Execute native command ZOOM E and verify completion."""
        cmd_res = self.router.call_tool(
            "execute_command",
            argument=json.dumps({"command_str": "ZOOM E "})
        )
        self.assertTrue(cmd_res.tool_success, f"execute_command failed: {cmd_res.error_message}")
        cmd_data = json.loads(cmd_res.raw_result)
        self.assertTrue(cmd_data.get("completed"))
        self.assertIn("last_prompt", cmd_data)

    # -------------------------------------------------------------
    # 6. Safety Policy: Unsupported, Destructive & Invalid Inputs
    # -------------------------------------------------------------
    def test_06_safety_and_error_handling(self):
        """Verify allowlist enforcement, destructive confirmation gates, and validation."""
        # 6a. Unsupported command blocked by allowlist
        unsupp_res = self.router.call_tool(
            "execute_command",
            argument=json.dumps({"command_str": "UNSUPPORTED_MALICIOUS_CMD"})
        )
        self.assertFalse(unsupp_res.tool_success)
        self.assertIn("not in the ZWCAD allowlist", str(unsupp_res.error_message or unsupp_res.raw_result))

        # 6b. Destructive command blocked without confirmation token
        destruct_res = self.router.call_tool(
            "delete_entities",
            argument=json.dumps({"handles": ["dummy_handle"]})
        )
        self.assertFalse(destruct_res.tool_success)
        self.assertIn("Security gate blocked", str(destruct_res.error_message))

        # 6c. Invalid coordinates rejected cleanly
        invalid_res = self.router.call_tool(
            "create_line",
            argument=json.dumps({"x1": float("inf"), "y1": 0.0, "x2": 100.0, "y2": 100.0})
        )
        self.assertFalse(invalid_res.tool_success)

    # -------------------------------------------------------------
    # 7. Non-Regression: Revit MCP Router Preservation
    # -------------------------------------------------------------
    def test_07_revit_mcp_unaffected(self):
        """Verify Revit tool calls route to HTTP Bridge (:8765) and leave Revit flow intact."""
        revit_res = self.router.call_tool("aigrav_get_revit_info")
        # Since Revit Bridge is not running on 8765 during CAD testing, it should cleanly return
        # a transport error from urllib (Connection refused), NOT route to ZWCAD!
        self.assertFalse(revit_res.transport_success)
        self.assertIn("AIGRAV Bridge unavailable", str(revit_res.error_message))


if __name__ == "__main__":
    unittest.main(verbosity=2)
