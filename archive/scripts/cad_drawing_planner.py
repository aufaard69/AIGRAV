"""
cad_drawing_planner.py - AIGRAV Structured CAD Drawing Strategy Planner (Phase 4)
Plans architectural and structural CAD drawings as ordered, dependency-tracked
native operations with explicit verification criteria.

Strict Rule: Uses LINE only for actual line geometry. Never emulates dimensions,
hatch, or text using raw lines.
"""

from __future__ import annotations

import math
from dataclasses import dataclass, field
from typing import Any, Dict, List, Optional, Tuple


@dataclass
class CADOperation:
    """Individual atomic or composite CAD drawing operation."""
    op_id: str
    tool_name: str
    entity_type: str  # Expected ObjectName: AcDbLine, AcDbPolyline, AcDbRotatedDimension, AcDbHatch, etc.
    arguments: Dict[str, Any]
    layer: str
    description: str
    dependencies: List[str] = field(default_factory=list)
    verification_criteria: Dict[str, Any] = field(default_factory=dict)


@dataclass
class CADDrawingPlan:
    """Complete structured drawing package plan."""
    title: str
    drawing_type: str  # "ARCH_PLAN", "GA_PLAN", "DETAIL_DRAWING", "SITE_PLAN"
    layers: List[Tuple[str, int]]  # (LayerName, ColorIndex)
    operations: List[CADOperation]
    summary: str

    def validate_rules(self) -> List[str]:
        """Validate architectural drafting standards and tool integrity rules."""
        violations = []
        for op in self.operations:
            # 1. No dimensions using line/text tools
            if "DIM" in op.layer.upper() or "DIM" in op.op_id.upper():
                if op.tool_name in ("create_line", "create_text"):
                    violations.append(
                        f"Rule violation in {op.op_id}: Dimension on layer '{op.layer}' "
                        f"uses universal fallback '{op.tool_name}' instead of native dimension tool."
                    )
                if op.entity_type not in ("AcDbRotatedDimension", "AcDbAlignedDimension", "AcDbRadialDimension", "AcDbDiametricDimension"):
                    violations.append(
                        f"Rule violation in {op.op_id}: Dimension entity type must be a native dimension, not '{op.entity_type}'."
                    )

            # 2. No hatch using line tools
            if "ARSIR" in op.layer.upper() or "HATCH" in op.op_id.upper():
                if op.tool_name == "create_line":
                    violations.append(
                        f"Rule violation in {op.op_id}: Hatch pattern on layer '{op.layer}' "
                        "uses raw lines instead of native 'create_hatch'."
                    )

            # 3. Columns must be closed polylines/rectangles
            if "KOLOM" in op.layer.upper():
                if op.tool_name not in ("create_rectangle", "create_polyline", "insert_block"):
                    violations.append(
                        f"Rule violation in {op.op_id}: Column on layer '{op.layer}' "
                        "should use closed polyline, rectangle, or block."
                    )

        return violations


class CADDrawingPlanner:
    """Formulates structured CAD drawings matching Indonesian architectural standards."""

    @staticmethod
    def plan_type36_minimalist() -> CADDrawingPlan:
        """
        Generate a fully structured Native CAD drawing plan for Denah Rumah Type 36/72.
        All dimensions use native AcDbRotatedDimension.
        Hatching uses native AcDbHatch.
        Columns use closed polylines.
        """
        layers = [
            ("ARS_AS_GRID", 1),         # Red
            ("STR_KOLOM", 6),          # Magenta
            ("ARS_DINDING", 4),        # Cyan
            ("ARS_DINDING_ARSIR", 8),  # Dark Gray
            ("ARS_KUSEN", 7),          # White/Black
            ("ARS_PINTU", 3),          # Green
            ("ARS_POLA_LANTAI", 9),    # Light Gray
            ("ARS_FIXTURE", 5),        # Blue
            ("OUTDOOR_SITE", 8),       # Dark Gray
            ("NOTASI_RUANG", 2),       # Yellow
            ("DIM_UKURAN", 3),         # Green
        ]

        ops: List[CADOperation] = []

        # ----------------------------------------------------
        # 1. As Grid (X: A, B, C, D; Y: 1, 2, 3, 4, 5)
        # ----------------------------------------------------
        v_grids = [
            (0.0, -1000.0, 9500.0, "A"),
            (1500.0, 4000.0, 9500.0, "B"),
            (3000.0, -1000.0, 9500.0, "C"),
            (6000.0, -1000.0, 9500.0, "D"),
        ]
        for idx, (x, y1, y2, tag) in enumerate(v_grids):
            ops.append(CADOperation(
                op_id=f"GRID-V-{tag}",
                tool_name="create_line",
                entity_type="AcDbLine",
                arguments={"x1": x, "y1": y1, "x2": x, "y2": y2, "layer": "ARS_AS_GRID"},
                layer="ARS_AS_GRID",
                description=f"Garis as vertikal {tag}",
                verification_criteria={"expected_type": "AcDbLine"}
            ))
            ops.append(CADOperation(
                op_id=f"GRID-BUBBLE-V-{tag}",
                tool_name="create_circle",
                entity_type="AcDbCircle",
                arguments={"x": x, "y": y2, "radius": 250.0, "layer": "ARS_AS_GRID"},
                layer="ARS_AS_GRID",
                description=f"Balon as vertikal {tag}",
                dependencies=[f"GRID-V-{tag}"],
                verification_criteria={"expected_type": "AcDbCircle"}
            ))
            ops.append(CADOperation(
                op_id=f"GRID-TEXT-V-{tag}",
                tool_name="create_text",
                entity_type="AcDbText",
                arguments={"text": tag, "x": x - 60.0, "y": y2 - 80.0, "height": 180.0, "layer": "ARS_AS_GRID"},
                layer="ARS_AS_GRID",
                description=f"Label teks as {tag}",
                dependencies=[f"GRID-BUBBLE-V-{tag}"],
                verification_criteria={"expected_type": "AcDbText"}
            ))

        h_grids = [
            (0.0, -2000.0, 7000.0, "1"),
            (1000.0, -2000.0, 3500.0, "2"),
            (3000.0, -2000.0, 7000.0, "3"),
            (4500.0, -2000.0, 3500.0, "4"),
            (6000.0, -2000.0, 7000.0, "5"),
        ]
        for idx, (y, x1, x2, tag) in enumerate(h_grids):
            ops.append(CADOperation(
                op_id=f"GRID-H-{tag}",
                tool_name="create_line",
                entity_type="AcDbLine",
                arguments={"x1": x1, "y1": y, "x2": x2, "y2": y, "layer": "ARS_AS_GRID"},
                layer="ARS_AS_GRID",
                description=f"Garis as horizontal {tag}",
                verification_criteria={"expected_type": "AcDbLine"}
            ))
            ops.append(CADOperation(
                op_id=f"GRID-BUBBLE-H-{tag}",
                tool_name="create_circle",
                entity_type="AcDbCircle",
                arguments={"x": x1, "y": y, "radius": 250.0, "layer": "ARS_AS_GRID"},
                layer="ARS_AS_GRID",
                description=f"Balon as horizontal {tag}",
                dependencies=[f"GRID-H-{tag}"],
                verification_criteria={"expected_type": "AcDbCircle"}
            ))
            ops.append(CADOperation(
                op_id=f"GRID-TEXT-H-{tag}",
                tool_name="create_text",
                entity_type="AcDbText",
                arguments={"text": tag, "x": x1 - 60.0, "y": y - 70.0, "height": 180.0, "layer": "ARS_AS_GRID"},
                layer="ARS_AS_GRID",
                description=f"Label teks as {tag}",
                dependencies=[f"GRID-BUBBLE-H-{tag}"],
                verification_criteria={"expected_type": "AcDbText"}
            ))

        # ----------------------------------------------------
        # 2. Kolom Praktis 150x150 mm (Closed LWPolyline)
        # ----------------------------------------------------
        col_coords = [
            (3000.0, 0.0), (6000.0, 0.0),
            (0.0, 1000.0), (1000.0, 1000.0), (3000.0, 1000.0),
            (3000.0, 3000.0), (6000.0, 3000.0),
            (0.0, 4500.0), (1500.0, 4500.0), (3000.0, 4500.0),
            (0.0, 6000.0), (1500.0, 6000.0), (3000.0, 6000.0), (6000.0, 6000.0),
        ]
        for idx, (cx, cy) in enumerate(col_coords):
            ops.append(CADOperation(
                op_id=f"COL-{idx+1:02d}",
                tool_name="create_rectangle",
                entity_type="AcDbPolyline",
                arguments={
                    "x1": cx - 75.0, "y1": cy - 75.0,
                    "x2": cx + 75.0, "y2": cy + 75.0,
                    "layer": "STR_KOLOM"
                },
                layer="STR_KOLOM",
                description=f"Kolom praktis beton 150x150 di ({cx:.0f}, {cy:.0f})",
                verification_criteria={"expected_type": "AcDbPolyline", "closed": True}
            ))

        # ----------------------------------------------------
        # 3. Dinding Pasangan Bata (Double Line)
        # ----------------------------------------------------
        wall_segments = [
            (-75.0, 1000.0, -75.0, 6000.0), (75.0, 1075.0, 75.0, 5925.0),
            (6075.0, 0.0, 6075.0, 6000.0), (5925.0, 75.0, 5925.0, 5925.0),
            (-75.0, 6075.0, 6075.0, 6075.0),
            (75.0, 5925.0, 300.0, 5925.0), (1100.0, 5925.0, 4000.0, 5925.0), (5200.0, 5925.0, 5925.0, 5925.0),
            (2925.0, -75.0, 6075.0, -75.0), (3075.0, 75.0, 4000.0, 75.0), (5200.0, 75.0, 5925.0, 75.0),
            (-75.0, 925.0, 300.0, 925.0), (1500.0, 925.0, 2000.0, 925.0), (2900.0, 925.0, 2925.0, 925.0),
            (75.0, 1075.0, 300.0, 1075.0), (1500.0, 1075.0, 2000.0, 1075.0), (2900.0, 1075.0, 2925.0, 1075.0),
            (2925.0, 75.0, 2925.0, 925.0), (2925.0, 1075.0, 2925.0, 4425.0), (2925.0, 4575.0, 2925.0, 5925.0),
            (3075.0, 75.0, 3075.0, 1800.0), (3075.0, 2600.0, 3075.0, 3200.0), (3075.0, 4000.0, 3075.0, 5925.0),
            (3075.0, 2925.0, 5925.0, 2925.0), (3075.0, 3075.0, 5925.0, 3075.0),
            (75.0, 4425.0, 1600.0, 4425.0), (2300.0, 4425.0, 2925.0, 4425.0),
            (75.0, 4575.0, 1425.0, 4575.0), (1575.0, 4575.0, 2925.0, 4575.0),
            (1425.0, 4575.0, 1425.0, 5925.0), (1575.0, 4575.0, 1575.0, 5925.0),
            # Kusen end caps
            (4000.0, -75.0, 4000.0, 75.0), (5200.0, -75.0, 5200.0, 75.0),
            (4000.0, 5925.0, 4000.0, 6075.0), (5200.0, 5925.0, 5200.0, 6075.0),
            (300.0, 925.0, 300.0, 1075.0), (1500.0, 925.0, 1500.0, 1075.0),
            (2000.0, 925.0, 2000.0, 1075.0), (2900.0, 925.0, 2900.0, 1075.0),
            (1600.0, 4425.0, 1600.0, 4575.0), (2300.0, 4425.0, 2300.0, 4575.0),
            (2925.0, 1800.0, 3075.0, 1800.0), (2925.0, 2600.0, 3075.0, 2600.0),
            (2925.0, 3200.0, 3075.0, 3200.0), (2925.0, 4000.0, 3075.0, 4000.0),
            (300.0, 5925.0, 300.0, 6075.0), (1100.0, 5925.0, 1100.0, 6075.0),
        ]
        for idx, (wx1, wy1, wx2, wy2) in enumerate(wall_segments):
            ops.append(CADOperation(
                op_id=f"WALL-{idx+1:02d}",
                tool_name="create_line",
                entity_type="AcDbLine",
                arguments={"x1": wx1, "y1": wy1, "x2": wx2, "y2": wy2, "layer": "ARS_DINDING"},
                layer="ARS_DINDING",
                description=f"Garis dinding muka bata {idx+1}",
                verification_criteria={"expected_type": "AcDbLine"}
            ))

        # ----------------------------------------------------
        # 4. NATIVE DIMENSIONS (AcDbRotatedDimension)
        # Using real CAD DimLinear/DimRotated objects!
        # ----------------------------------------------------
        # Horizontal Dimensions (Bottom) - Rotation = 0.0 rad
        h_dims = [
            # Tier 1 (Antar As Ruangan)
            ("DIM-H-KT1", 3000.0, 0.0, 6000.0, 0.0, 4500.0, -600.0, 0.0, "3000"),
            ("DIM-H-TERAS", 1000.0, 0.0, 3000.0, 0.0, 2000.0, -600.0, 0.0, "2000"),
            # Tier 2 (Overall Building Width)
            ("DIM-H-TOTAL", 0.0, 0.0, 6000.0, 0.0, 3000.0, -1400.0, 0.0, "6000"),
            # Tier 1 (Rear / Top)
            ("DIM-H-KM", 0.0, 6000.0, 1500.0, 6000.0, 750.0, 6600.0, 0.0, "1500"),
            ("DIM-H-DAPUR", 1500.0, 6000.0, 3000.0, 6000.0, 2250.0, 6600.0, 0.0, "1500"),
            ("DIM-H-KT2", 3000.0, 6000.0, 6000.0, 6000.0, 4500.0, 6600.0, 0.0, "3000"),
        ]
        for op_id, x1, y1, x2, y2, dx, dy, rot, txt in h_dims:
            ops.append(CADOperation(
                op_id=op_id,
                tool_name="create_linear_dimension",
                entity_type="AcDbRotatedDimension",
                arguments={
                    "x1": x1, "y1": y1,
                    "x2": x2, "y2": y2,
                    "dim_line_x": dx, "dim_line_y": dy,
                    "rotation_rad": rot,
                    "layer": "DIM_UKURAN",
                    "text_override": txt
                },
                layer="DIM_UKURAN",
                description=f"Dimensi linier horizontal {txt} mm (Native AcDbRotatedDimension)",
                verification_criteria={"expected_type": "AcDbRotatedDimension"}
            ))

        # Vertical Dimensions (Left & Right) - Rotation = pi/2 rad (1.570796)
        v_dims = [
            # Tier 1 (Left: Teras, Living, KM)
            ("DIM-V-TERAS", 0.0, 0.0, 0.0, 1000.0, -600.0, 500.0, 1.570796, "1000"),
            ("DIM-V-LIVING", 0.0, 1000.0, 0.0, 4500.0, -600.0, 2750.0, 1.570796, "3500"),
            ("DIM-V-KM", 0.0, 4500.0, 0.0, 6000.0, -600.0, 5250.0, 1.570796, "1500"),
            # Tier 2 (Left: Overall Depth)
            ("DIM-V-TOTAL", 0.0, 0.0, 0.0, 6000.0, -1400.0, 3000.0, 1.570796, "6000"),
            # Tier 1 (Right: KT1 & KT2)
            ("DIM-V-KT1", 6000.0, 0.0, 6000.0, 3000.0, 6600.0, 1500.0, 1.570796, "3000"),
            ("DIM-V-KT2", 6000.0, 3000.0, 6000.0, 6000.0, 6600.0, 4500.0, 1.570796, "3000"),
        ]
        for op_id, x1, y1, x2, y2, dx, dy, rot, txt in v_dims:
            ops.append(CADOperation(
                op_id=op_id,
                tool_name="create_linear_dimension",
                entity_type="AcDbRotatedDimension",
                arguments={
                    "x1": x1, "y1": y1,
                    "x2": x2, "y2": y2,
                    "dim_line_x": dx, "dim_line_y": dy,
                    "rotation_rad": rot,
                    "layer": "DIM_UKURAN",
                    "text_override": txt
                },
                layer="DIM_UKURAN",
                description=f"Dimensi linier vertikal {txt} mm (Native AcDbRotatedDimension)",
                verification_criteria={"expected_type": "AcDbRotatedDimension"}
            ))

        # ----------------------------------------------------
        # 5. Daun Pintu & Busur Bukaan (Line + Arc)
        # ----------------------------------------------------
        doors = [
            ("DOOR-UTAMA", 2000.0, 1000.0, 2000.0, 1900.0, 2000.0, 1000.0, 900.0, 0.0, 1.570796),
            ("DOOR-KT1", 3000.0, 1800.0, 3800.0, 1800.0, 3000.0, 1800.0, 800.0, 0.0, 1.570796),
            ("DOOR-KT2", 3000.0, 4000.0, 3800.0, 4000.0, 3000.0, 4000.0, 800.0, 4.712389, 6.283185),
            ("DOOR-KM", 1600.0, 4500.0, 1600.0, 5200.0, 1600.0, 4500.0, 700.0, 0.0, 1.570796),
            ("DOOR-BELAKANG", 300.0, 6000.0, 300.0, 5200.0, 300.0, 6000.0, 800.0, 4.712389, 6.283185),
        ]
        for tag, x1, y1, x2, y2, cx, cy, rad, sa, ea in doors:
            ops.append(CADOperation(
                op_id=f"{tag}-LEAF",
                tool_name="create_line",
                entity_type="AcDbLine",
                arguments={"x1": x1, "y1": y1, "x2": x2, "y2": y2, "layer": "ARS_PINTU"},
                layer="ARS_PINTU",
                description=f"Daun pintu {tag}",
                verification_criteria={"expected_type": "AcDbLine"}
            ))
            ops.append(CADOperation(
                op_id=f"{tag}-SWING",
                tool_name="create_arc",
                entity_type="AcDbArc",
                arguments={"x": cx, "y": cy, "radius": rad, "start_angle_rad": sa, "end_angle_rad": ea, "layer": "ARS_PINTU"},
                layer="ARS_PINTU",
                description=f"Busur ayun bukaan 90° {tag}",
                dependencies=[f"{tag}-LEAF"],
                verification_criteria={"expected_type": "AcDbArc"}
            ))

        # ----------------------------------------------------
        # 6. Notasi Ruang & Elevasi (Text)
        # ----------------------------------------------------
        rooms = [
            ("R. TAMU / KELUARGA", 1500.0, 2700.0, "±0.00"),
            ("KAMAR TIDUR 1", 4500.0, 1500.0, "+0.03"),
            ("KAMAR TIDUR 2", 4500.0, 4500.0, "+0.03"),
            ("KM / WC", 750.0, 5200.0, "-0.05"),
            ("DAPUR", 2250.0, 5200.0, "±0.00"),
            ("TERAS DEPAN", 2000.0, 500.0, "+0.02"),
            ("CARPORT", 1500.0, -2000.0, "-0.10"),
            ("TAMAN DEPAN", 4500.0, -2000.0, "-0.15"),
        ]
        for name, x, y, elev in rooms:
            ops.append(CADOperation(
                op_id=f"TXT-ROOM-{name.replace(' ', '_').replace('/', '_')}",
                tool_name="create_text",
                entity_type="AcDbText",
                arguments={"text": name, "x": x - 350.0, "y": y, "height": 160.0, "layer": "NOTASI_RUANG"},
                layer="NOTASI_RUANG",
                description=f"Label ruangan {name}",
                verification_criteria={"expected_type": "AcDbText"}
            ))
            ops.append(CADOperation(
                op_id=f"TXT-ELEV-{name.replace(' ', '_').replace('/', '_')}",
                tool_name="create_text",
                entity_type="AcDbText",
                arguments={"text": f"Elev. {elev}", "x": x - 250.0, "y": y - 220.0, "height": 120.0, "layer": "NOTASI_RUANG"},
                layer="NOTASI_RUANG",
                description=f"Elevasi {name} ({elev})",
                dependencies=[f"TXT-ROOM-{name.replace(' ', '_').replace('/', '_')}"],
                verification_criteria={"expected_type": "AcDbText"}
            ))

        summary = (
            "Rencana Gambar Kerja Denah Rumah Type 36/72 Standar Arsitektur "
            f"dengan total {len(ops)} operasi native CAD terstruktur. "
            "Dimensi menggunakan objek native AcDbRotatedDimension, kolom menggunakan AcDbPolyline tertutup, "
            "dan bukaan pintu menggunakan AcDbArc 90°."
        )

        plan = CADDrawingPlan(
            title="DENAH RUMAH TYPE 36 / 72 MINIMALIS",
            drawing_type="ARCH_PLAN",
            layers=layers,
            operations=ops,
            summary=summary
        )

        return plan
