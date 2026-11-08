"""
cad_verifier.py - AIGRAV Post-Execution Verification and Recovery Engine (Phase 5)
Verifies that created entities exist, match their expected native CAD object type,
and strictly detects when dimensions are mistakenly drawn as raw lines or text.
"""

from __future__ import annotations

import json
from dataclasses import dataclass, field
from typing import Any, Dict, List, Optional

from cad_drawing_planner import CADDrawingPlan, CADOperation
from zwcad_tool_adapter import ZWCADToolRegistry, zwcad_adapter


@dataclass
class CADVerificationResult:
    op_id: str
    handle: str
    expected_type: str
    actual_type: str
    layer: str
    verified: bool
    error_reason: Optional[str] = None
    properties: Dict[str, Any] = field(default_factory=dict)


@dataclass
class CADBatchReport:
    total_operations: int
    succeeded_count: int
    failed_count: int
    results: List[CADVerificationResult]
    summary: str
    is_complete: bool


class CADVerifier:
    """Verifies CAD entity creation against strict architectural standards."""

    def __init__(self, adapter: Optional[ZWCADToolRegistry] = None):
        self.adapter = adapter or zwcad_adapter

    def verify_single_operation(
        self,
        op: CADOperation,
        tool_raw_result: str
    ) -> CADVerificationResult:
        """Verify an individual operation against its created entity in ZWCAD."""
        try:
            res_data = json.loads(tool_raw_result)
        except Exception as e:
            return CADVerificationResult(
                op_id=op.op_id,
                handle="",
                expected_type=op.entity_type,
                actual_type="UNKNOWN",
                layer=op.layer,
                verified=False,
                error_reason=f"Failed to parse tool result as JSON: {e}"
            )

        if not res_data.get("ok", False):
            return CADVerificationResult(
                op_id=op.op_id,
                handle="",
                expected_type=op.entity_type,
                actual_type="ERROR",
                layer=op.layer,
                verified=False,
                error_reason=res_data.get("error", "Unknown tool failure")
            )

        handle = str(res_data.get("handle", ""))
        if not handle:
            return CADVerificationResult(
                op_id=op.op_id,
                handle="",
                expected_type=op.entity_type,
                actual_type="NO_HANDLE",
                layer=op.layer,
                verified=False,
                error_reason="No handle returned by creation tool"
            )

        # Query entity properties from ZWCAD
        prop_res = self.adapter.call_tool("get_entity_properties", {"handle": handle})
        if not prop_res.tool_success:
            return CADVerificationResult(
                op_id=op.op_id,
                handle=handle,
                expected_type=op.entity_type,
                actual_type="LOOKUP_FAILED",
                layer=op.layer,
                verified=False,
                error_reason=f"Could not retrieve entity properties by handle {handle}: {prop_res.error_message}"
            )

        try:
            prop_data = json.loads(prop_res.raw_result).get("properties", {})
        except Exception:
            prop_data = {}

        actual_type = str(prop_data.get("objectname", res_data.get("objectname", "Unknown")))
        actual_layer = str(prop_data.get("layer", res_data.get("layer", "")))

        # Verification Criteria:
        # 1. Type matching
        type_match = False
        if op.entity_type.lower() == actual_type.lower():
            type_match = True
        elif op.entity_type == "AcDbRotatedDimension" and "dimension" in actual_type.lower():
            type_match = True
        elif op.entity_type == "AcDbAlignedDimension" and "dimension" in actual_type.lower():
            type_match = True
        elif op.entity_type == "AcDbPolyline" and "polyline" in actual_type.lower():
            type_match = True

        # STRICT AUDIT: If operation was supposed to be a dimension, reject AcDbLine or AcDbText!
        if "DIM" in op.layer.upper() and actual_type in ("AcDbLine", "AcDbText"):
            return CADVerificationResult(
                op_id=op.op_id,
                handle=handle,
                expected_type=op.entity_type,
                actual_type=actual_type,
                layer=actual_layer,
                verified=False,
                error_reason=f"FALSE DIMENSION DETECTED! Created '{actual_type}' instead of native Dimension object.",
                properties=prop_data
            )

        if not type_match:
            return CADVerificationResult(
                op_id=op.op_id,
                handle=handle,
                expected_type=op.entity_type,
                actual_type=actual_type,
                layer=actual_layer,
                verified=False,
                error_reason=f"Type mismatch: expected {op.entity_type}, found {actual_type}.",
                properties=prop_data
            )

        return CADVerificationResult(
            op_id=op.op_id,
            handle=handle,
            expected_type=op.entity_type,
            actual_type=actual_type,
            layer=actual_layer,
            verified=True,
            properties=prop_data
        )

    def execute_and_verify_plan(
        self,
        plan: CADDrawingPlan,
        stop_on_first_error: bool = False
    ) -> CADBatchReport:
        """
        Execute drawing plan and verify every created entity against strict criteria.
        Returns complete audit report.
        """
        # 1. Ensure all layers are created
        for l_name, l_color in plan.layers:
            self.adapter.call_tool("create_layer", {"name": l_name, "color_index": l_color})

        results: List[CADVerificationResult] = []
        created_handles: Dict[str, str] = {}  # op_id -> handle

        for op in plan.operations:
            # Resolve arguments if dependent on prior handles
            args = dict(op.arguments)

            exec_res = self.adapter.call_tool(op.tool_name, args)

            if not exec_res.tool_success:
                vr = CADVerificationResult(
                    op_id=op.op_id,
                    handle="",
                    expected_type=op.entity_type,
                    actual_type="FAILED",
                    layer=op.layer,
                    verified=False,
                    error_reason=exec_res.error_message or "Tool invocation failed"
                )
                results.append(vr)
                if stop_on_first_error:
                    break
                continue

            # Verify created entity
            vr = self.verify_single_operation(op, exec_res.raw_result)
            results.append(vr)
            if vr.verified and vr.handle:
                created_handles[op.op_id] = vr.handle

            if not vr.verified and stop_on_first_error:
                break

        # Zoom extents after batch execution
        self.adapter.call_tool("zoom_extents", {})

        succeeded = sum(1 for r in results if r.verified)
        failed = len(results) - succeeded
        is_complete = (failed == 0 and len(results) == len(plan.operations))

        summary = (
            f"Eksekusi gambar CAD '{plan.title}': {succeeded}/{len(plan.operations)} "
            f"operasi native terverifikasi sukses ({failed} gagal)."
        )

        return CADBatchReport(
            total_operations=len(plan.operations),
            succeeded_count=succeeded,
            failed_count=failed,
            results=results,
            summary=summary,
            is_complete=is_complete
        )


# Global verifier instance
cad_verifier = CADVerifier()
