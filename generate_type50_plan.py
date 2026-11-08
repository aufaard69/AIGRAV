"""
generate_type50_plan.py - High-Fidelity Professional 2D Architectural Floor Plan Generator
Type 50/84 Minimalist Modern Residential House - ZWCAD 2023 Professional (Drawing1.dwg).

Key Architectural Enhancements:
1. True 1:100 Scale Native Dimensions (AcDbRotatedDimension):
   - Scaled text height (180 mm), architectural oblique slash ticks (120 mm), proper offsets and gaps.
   - Distinct 3-tier exterior hierarchy with zero overlapping.
2. Clean Structural Grid & Bubble System:
   - Grid lines stop at bubble perimeters (lines do NOT cut through bubbles or text).
   - Bubbles positioned outside all dimension tiers.
   - Text mathematically centered inside circles (acAlignmentMiddleCenter = 10 with TextAlignmentPoint).
3. Precision Centered Room Annotations:
   - Centered room labels and FFL elevations at unobstructed room centroids.
   - No collision with walls, doors, or furniture.
4. Solid Hatched Reinforced Concrete Columns:
   - Native SOLID hatching on all 150x150 mm RC columns.
5. Title Block & North Arrow Separation:
   - Title centered cleanly beneath drawing, North arrow placed in clear top-right zone.
"""

from __future__ import annotations

import math
import sys
import json
import time
from pathlib import Path

# Setup paths
AIGRAV_DIR = Path(r"C:\AIGRAV")
ZWCAD_DIR = Path(r"C:\ZWCAD-MCP")

for p in (AIGRAV_DIR, ZWCAD_DIR):
    if str(p) not in sys.path:
        sys.path.insert(0, str(p))

import cad_bridge

def create_floor_plan():
    print("=" * 75)
    print("STARTING PROFESSIONAL ARCHITECTURAL FLOOR PLAN OVERHAUL (TYPE 50 / 84)")
    print("=" * 75)

    with cad_bridge.cad_context() as (app, _):
        # Specifically locate and activate Drawing1.dwg
        target_doc = None
        for i in range(app.Documents.Count):
            d = app.Documents.Item(i)
            if d.Name.lower() == "drawing1.dwg":
                target_doc = d
                break
        if not target_doc:
            raise RuntimeError("Drawing1.dwg is not open in ZWCAD!")

        target_doc.Activate()
        doc = target_doc
        ms = doc.ModelSpace
        print(f"Connected to: {app.Caption}")
        print(f"Target Document: {doc.Name} (Initial entities: {ms.Count})")

        # 1. Clear previous entities in Drawing1.dwg fast
        if ms.Count > 0:
            print(f"Clearing {ms.Count} existing entities in Drawing1.dwg...")
            try:
                try:
                    ss = doc.SelectionSets.Item("TEMP_CLR")
                    ss.Delete()
                except Exception:
                    pass
                ss = doc.SelectionSets.Add("TEMP_CLR")
                ss.Select(5)  # acSelectionSetAll
                ss.Erase()
                ss.Delete()
            except Exception as e:
                print(f"Fast erase fallback: {e}")

        # 2. Setup AIA Standard Layers
        layers_def = [
            ("A-GRID", 1),         # Red - Structural Grid Lines
            ("S-COLS", 7),         # White - Structural Columns
            ("A-WALL-FULL", 4),    # Cyan - Cut Wall Profiles (0.40 mm)
            ("A-WALL-PATS", 8),    # Dark Gray - Wall Hatch / Cavity Pattern
            ("A-DOOR", 3),         # Green - Doors & Swing Arcs
            ("A-GLAZ", 4),         # Cyan - Windows, Frames & Glass
            ("A-FLOR", 9),         # Light Gray - Floor Transitions & Thresholds
            ("A-PLBG", 5),         # Blue - Plumbing & Sanitary Fixtures
            ("A-FURN", 8),         # Dark Gray - Furniture Layout
            ("A-DIMS-EXT", 3),     # Green - Hierarchical Exterior Dimensions
            ("A-DIMS-INT", 2),     # Yellow - Interior Clearance Dimensions
            ("A-ANNO-TEXT", 2),    # Yellow - Room Labels & Elevation Markers
            ("A-ANNO-SYMB", 1),    # Red - Grid Bubbles, North Arrow & Markers
        ]

        for lay_name, col_idx in layers_def:
            try:
                l_obj = doc.Layers.Add(lay_name)
            except Exception:
                l_obj = doc.Layers.Item(lay_name)
            l_obj.Color = int(col_idx)

        print(f"Configured {len(layers_def)} AIA CAD standard layers.")

        # Helper primitives
        def add_line(x1, y1, x2, y2, layer):
            p1 = cad_bridge.point3(x1, y1)
            p2 = cad_bridge.point3(x2, y2)
            ent = ms.AddLine(p1, p2)
            ent.Layer = layer
            return ent

        def add_polyline(pts, closed, layer):
            flat = []
            for p in pts:
                flat.extend([float(p[0]), float(p[1])])
            import win32com.client, pythoncom
            arr = win32com.client.VARIANT(pythoncom.VT_ARRAY | pythoncom.VT_R8, flat)
            ent = ms.AddLightWeightPolyline(arr)
            ent.Closed = bool(closed)
            ent.Layer = layer
            return ent

        def add_rect(x1, y1, x2, y2, layer):
            pts = [[x1, y1], [x2, y1], [x2, y2], [x1, y2]]
            return add_polyline(pts, True, layer)

        def add_circle(cx, cy, r, layer):
            c = cad_bridge.point3(cx, cy)
            ent = ms.AddCircle(c, float(r))
            ent.Layer = layer
            return ent

        def add_arc(cx, cy, r, sa, ea, layer):
            c = cad_bridge.point3(cx, cy)
            ent = ms.AddArc(c, float(r), float(sa), float(ea))
            ent.Layer = layer
            return ent

        def add_centered_text(txt, cx, cy, h, layer, rot=0.0):
            """Add text mathematically centered at (cx, cy) using acAlignmentMiddleCenter (10)."""
            pt = cad_bridge.point3(cx, cy)
            ent = ms.AddText(str(txt), pt, float(h))
            ent.Alignment = 10  # acAlignmentMiddleCenter
            ent.TextAlignmentPoint = pt
            ent.Rotation = float(rot)
            ent.Layer = layer
            return ent

        def add_dim_scaled(x1, y1, x2, y2, dx, dy, rot, layer, override=""):
            """Add native dimension styled for architectural 1:100 scale with oblique slash ticks."""
            ext1 = cad_bridge.point3(x1, y1)
            ext2 = cad_bridge.point3(x2, y2)
            line_pt = cad_bridge.point3(dx, dy)
            ent = ms.AddDimRotated(ext1, ext2, line_pt, float(rot))
            ent.Layer = layer

            # Apply Architectural Dimension Standards for 1:100 scale in mm
            try:
                ent.TextHeight = 180.0             # 1.8 mm paper text height
                ent.ArrowheadSize = 120.0          # 1.2 mm tick size
                ent.Arrowhead1Type = 12            # acArrowOblique (architectural 45 deg tick)
                ent.Arrowhead2Type = 12
                ent.ExtensionLineExtend = 120.0    # tick overshoot
                ent.ExtensionLineOffset = 150.0    # gap from building wall
                ent.TextGap = 60.0                 # gap above dimline
                ent.VerticalTextPosition = 1       # acAbove
                ent.DimensionLineColor = 3         # Green
                ent.ExtensionLineColor = 3         # Green
                ent.TextColor = 2                  # Yellow
            except Exception:
                pass

            if override:
                ent.TextOverride = str(override)
            return ent

        def add_solid_hatch(boundary_ent, layer):
            """Add native SOLID hatch to a closed polyline loop."""
            try:
                import win32com.client, pythoncom
                hatch = ms.AddHatch(0, "SOLID", False)
                hatch.Layer = layer
                loop = win32com.client.VARIANT(pythoncom.VT_ARRAY | pythoncom.VT_DISPATCH, [boundary_ent])
                hatch.AppendOuterLoop(loop)
                hatch.Evaluate()
                return hatch
            except Exception:
                return None

        # -------------------------------------------------------------
        # STEP 1: REINFORCED CONCRETE COLUMNS (S-COLS) - 150x150 mm
        # With Solid Hatch Fill
        # -------------------------------------------------------------
        print("Placing solid-hatched reinforced concrete columns (150x150 mm)...")
        column_locs = [
            (0.0, 0.0), (3000.0, 0.0), (6000.0, 0.0),
            (6000.0, 3000.0),
            (0.0, 4500.0), (3000.0, 4500.0), (6000.0, 4500.0),
            (0.0, 8000.0), (3000.0, 8000.0), (4500.0, 8000.0), (6000.0, 8000.0),
            (0.0, 10500.0), (3000.0, 10000.0), (4500.0, 10000.0), (6000.0, 10000.0),
            (0.0, 12500.0), (3000.0, 12500.0), (6000.0, 12500.0),
            (0.0, 14000.0), (3000.0, 14000.0), (6000.0, 14000.0),
        ]

        col_half = 75.0
        for cx, cy in column_locs:
            col_pl = add_rect(cx - col_half, cy - col_half, cx + col_half, cy + col_half, "S-COLS")
            add_solid_hatch(col_pl, "S-COLS")

        # -------------------------------------------------------------
        # STEP 2: CUT WALLS (A-WALL-FULL & A-WALL-PATS) - 150 mm Thick
        # -------------------------------------------------------------
        print("Drafting 150 mm cut walls with cavity patterns...")
        w_half = 75.0

        def make_wall(x1, y1, x2, y2):
            dx = x2 - x1
            dy = y2 - y1
            length = math.hypot(dx, dy)
            if length < 1.0:
                return
            ux = dx / length
            uy = dy / length
            nx = -uy * w_half
            ny = ux * w_half

            # Double-line cut profile
            add_line(x1 + nx, y1 + ny, x2 + nx, y2 + ny, "A-WALL-FULL")
            add_line(x1 - nx, y1 - ny, x2 - nx, y2 - ny, "A-WALL-FULL")
            # Cavity centerline pattern
            add_line(x1, y1, x2, y2, "A-WALL-PATS")

        walls = [
            # 1. Perimeter Party Walls
            (0.0, 4500.0, 0.0, 12500.0),
            (6000.0, 4500.0, 6000.0, 12500.0),

            # 2. Front Facade Wall (Y=4500)
            # Living Room: Window W3 (X: 400-1600), Door D1 (X: 1900-2800)
            (0.0, 4500.0, 400.0, 4500.0),
            (1600.0, 4500.0, 1900.0, 4500.0),
            (2800.0, 4500.0, 3000.0, 4500.0),
            # Master Bedroom: Window W1 (X: 3800-5200)
            (3000.0, 4500.0, 3800.0, 4500.0),
            (5200.0, 4500.0, 6000.0, 4500.0),

            # 3. Center Spine Partition (X=3000)
            # Living / Master Bed (Door D2 at Y: 7100-7900)
            (3000.0, 4500.0, 3000.0, 7100.0),
            (3000.0, 7900.0, 3000.0, 8000.0),
            # Hall / KM access
            (3000.0, 8000.0, 3000.0, 8200.0),
            (3000.0, 9100.0, 3000.0, 10000.0),
            # Kitchen / Bed 2 (Door D2 at Y: 10100-10900)
            (3000.0, 10000.0, 3000.0, 10100.0),
            (3000.0, 10900.0, 3000.0, 12500.0),

            # 4. Mid Transverse Wall (Y=8000)
            (3000.0, 8000.0, 6000.0, 8000.0),

            # 5. KM/WC Enclosure
            # Left wall at X=4500 (Door D3 at Y: 8200-8900)
            (4500.0, 8000.0, 4500.0, 8200.0),
            (4500.0, 8900.0, 4500.0, 10000.0),
            # Rear wall at Y=10000
            (4500.0, 10000.0, 6000.0, 10000.0),

            # 6. Rear Facade Wall (Y=12500)
            # Kitchen: Service Door D4 (X: 500-1300), Window W4 (X: 1700-2600)
            (0.0, 12500.0, 500.0, 12500.0),
            (1300.0, 12500.0, 1700.0, 12500.0),
            (2600.0, 12500.0, 3000.0, 12500.0),
            # Bed 2: Window W2 (X: 3900-5100)
            (3000.0, 12500.0, 3900.0, 12500.0),
            (5100.0, 12500.0, 6000.0, 12500.0),

            # 7. Boundary / Yard Walls
            (0.0, 0.0, 0.0, 4500.0),
            (0.0, 12500.0, 0.0, 14000.0),
            (6000.0, 12500.0, 6000.0, 14000.0),
            (0.0, 14000.0, 6000.0, 14000.0),
        ]

        for x1, y1, x2, y2 in walls:
            make_wall(x1, y1, x2, y2)

        # -------------------------------------------------------------
        # STEP 3: DOORS & SWING ARCS (A-DOOR)
        # -------------------------------------------------------------
        print("Drafting architectural doors and swing arcs...")
        def make_door_single(hinge_x, hinge_y, width, angle_deg, swing_dir, tag):
            # 50x100 mm Kusen
            add_rect(hinge_x - 25.0, hinge_y - 50.0, hinge_x + 25.0, hinge_y + 50.0, "A-DOOR")
            if angle_deg == 0:  # Horizontal wall
                latch_x = hinge_x + width
                add_rect(latch_x - 25.0, hinge_y - 50.0, latch_x + 25.0, hinge_y + 50.0, "A-DOOR")
                if swing_dir == "UP":
                    add_line(hinge_x, hinge_y, hinge_x, hinge_y + width, "A-DOOR")
                    add_arc(hinge_x, hinge_y, width, 0.0, math.pi / 2.0, "A-DOOR")
                else:
                    add_line(hinge_x, hinge_y, hinge_x, hinge_y - width, "A-DOOR")
                    add_arc(hinge_x, hinge_y, width, 1.5 * math.pi, 2.0 * math.pi, "A-DOOR")
                add_centered_text(tag, hinge_x + width / 2.0, hinge_y + 200.0, 150.0, "A-DOOR")

            elif angle_deg == 90:  # Vertical wall
                latch_y = hinge_y + width
                add_rect(hinge_x - 50.0, latch_y - 25.0, hinge_x + 50.0, latch_y + 25.0, "A-DOOR")
                if swing_dir == "RIGHT":
                    add_line(hinge_x, hinge_y, hinge_x + width, hinge_y, "A-DOOR")
                    add_arc(hinge_x, hinge_y, width, 0.0, math.pi / 2.0, "A-DOOR")
                else:
                    add_line(hinge_x, hinge_y, hinge_x - width, hinge_y, "A-DOOR")
                    add_arc(hinge_x, hinge_y, width, math.pi / 2.0, math.pi, "A-DOOR")
                add_centered_text(tag, hinge_x + 200.0, hinge_y + width / 2.0, 150.0, "A-DOOR")

        make_door_single(1900.0, 4500.0, 900.0, 0, "UP", "D1")
        make_door_single(3000.0, 7100.0, 800.0, 90, "RIGHT", "D2")
        make_door_single(3000.0, 10100.0, 800.0, 90, "RIGHT", "D2")
        make_door_single(4500.0, 8200.0, 700.0, 90, "RIGHT", "D3")
        make_door_single(500.0, 12500.0, 800.0, 0, "UP", "D4")

        # -------------------------------------------------------------
        # STEP 4: WINDOWS & GLAZING (A-GLAZ)
        # -------------------------------------------------------------
        print("Drafting architectural windows and glass lines...")
        def make_window_horiz(x1, x2, y, tag, leaves=2):
            w = x2 - x1
            add_rect(x1, y - 50.0, x1 + 50.0, y + 50.0, "A-GLAZ")
            add_rect(x2 - 50.0, y - 50.0, x2, y + 50.0, "A-GLAZ")
            add_line(x1, y - 50.0, x2, y - 50.0, "A-GLAZ")
            add_line(x1, y + 50.0, x2, y + 50.0, "A-GLAZ")
            add_line(x1 + 50.0, y - 10.0, x2 - 50.0, y - 10.0, "A-GLAZ")
            add_line(x1 + 50.0, y + 10.0, x2 - 50.0, y + 10.0, "A-GLAZ")

            if leaves == 2:
                mid_x = (x1 + x2) / 2.0
                add_rect(mid_x - 25.0, y - 50.0, mid_x + 25.0, y + 50.0, "A-GLAZ")

            add_centered_text(tag, (x1 + x2) / 2.0, y + 180.0, 150.0, "A-GLAZ")

        make_window_horiz(3800.0, 5200.0, 4500.0, "W1", leaves=2)
        make_window_horiz(3900.0, 5100.0, 12500.0, "W2", leaves=2)
        make_window_horiz(400.0, 1600.0, 4500.0, "W3", leaves=2)
        make_window_horiz(1700.0, 2600.0, 12500.0, "W4", leaves=1)

        # -------------------------------------------------------------
        # STEP 5: SANITARY PLUMBING FIXTURES (A-PLBG)
        # -------------------------------------------------------------
        print("Drafting sanitary plumbing fixtures...")
        # Monoblock Water Closet
        add_rect(5150.0, 8075.0, 5650.0, 8275.0, "A-PLBG")
        add_rect(5200.0, 8275.0, 5600.0, 8700.0, "A-PLBG")
        add_circle(5400.0, 8500.0, 120.0, "A-PLBG")

        # Shower Drop & Kerb
        add_line(5100.0, 9100.0, 6000.0, 9100.0, "A-PLBG")
        add_line(5100.0, 9100.0, 5100.0, 10000.0, "A-PLBG")
        add_circle(5550.0, 9550.0, 120.0, "A-PLBG")
        add_line(5550.0, 9550.0, 5925.0, 9550.0, "A-PLBG")

        # Floor Drain
        add_circle(4800.0, 9600.0, 75.0, "A-PLBG")
        add_rect(4700.0, 9500.0, 4900.0, 9700.0, "A-PLBG")
        add_centered_text("FD", 4800.0, 9780.0, 100.0, "A-PLBG")

        # Kitchen Sink
        add_rect(1800.0, 11950.0, 2500.0, 12425.0, "A-PLBG")
        add_rect(1850.0, 12000.0, 2125.0, 12375.0, "A-PLBG")
        add_rect(2175.0, 12000.0, 2450.0, 12375.0, "A-PLBG")
        add_circle(2150.0, 12400.0, 30.0, "A-PLBG")

        # -------------------------------------------------------------
        # STEP 6: FURNITURE LAYOUT (A-FURN)
        # -------------------------------------------------------------
        print("Drafting furniture layout...")
        # Master Bed (Queen 1600x2000)
        bx, by = 4100.0, 5600.0
        add_rect(bx, by, bx + 1600.0, by + 2000.0, "A-FURN")
        add_rect(bx - 50.0, by + 1950.0, bx + 1650.0, by + 2050.0, "A-FURN")
        add_rect(bx + 100.0, by + 1550.0, bx + 650.0, by + 1900.0, "A-FURN")
        add_rect(bx + 950.0, by + 1550.0, bx + 1500.0, by + 1900.0, "A-FURN")
        add_line(bx, by + 600.0, bx + 1600.0, by + 600.0, "A-FURN")
        add_rect(bx - 500.0, by + 1550.0, bx - 50.0, by + 2000.0, "A-FURN")
        # Wardrobe
        add_rect(4400.0, 4575.0, 5800.0, 5075.0, "A-FURN")
        add_line(4400.0, 4575.0, 5800.0, 5075.0, "A-FURN")

        # Secondary Bed (Single 1200x2000)
        sbx, sby = 4600.0, 10400.0
        add_rect(sbx, sby, sbx + 1200.0, sby + 2000.0, "A-FURN")
        add_rect(sbx - 30.0, sby + 1950.0, sbx + 1230.0, sby + 2030.0, "A-FURN")
        add_rect(sbx + 200.0, sby + 1550.0, sbx + 1000.0, sby + 1900.0, "A-FURN")
        # Study Desk & Wardrobe
        add_rect(3200.0, 11900.0, 4100.0, 12400.0, "A-FURN")
        add_circle(3650.0, 11650.0, 180.0, "A-FURN")
        add_rect(3200.0, 10400.0, 4200.0, 10950.0, "A-FURN")

        # Living Room: Sofa + Table + TV Credenza
        sofa_x, sofa_y = 350.0, 5200.0
        add_rect(sofa_x, sofa_y, sofa_x + 850.0, sofa_y + 2000.0, "A-FURN")
        for i in range(3):
            add_rect(sofa_x + 150.0, sofa_y + i * 650.0 + 50.0, sofa_x + 800.0, sofa_y + (i + 1) * 650.0 - 50.0, "A-FURN")
        add_rect(sofa_x + 1100.0, sofa_y + 550.0, sofa_x + 1550.0, sofa_y + 1450.0, "A-FURN")
        add_rect(2600.0, 5400.0, 2925.0, 6800.0, "A-FURN")
        add_centered_text("TV", 2760.0, 6100.0, 120.0, "A-FURN")

        # Dining Area: Table (1200x800) + Chairs
        dt_x, dt_y = 900.0, 8400.0
        add_rect(dt_x, dt_y, dt_x + 1200.0, dt_y + 800.0, "A-FURN")
        add_rect(dt_x + 150.0, dt_y - 350.0, dt_x + 500.0, dt_y - 50.0, "A-FURN")
        add_rect(dt_x + 700.0, dt_y - 350.0, dt_x + 1050.0, dt_y - 50.0, "A-FURN")
        add_rect(dt_x + 150.0, dt_y + 850.0, dt_x + 500.0, dt_y + 1150.0, "A-FURN")
        add_rect(dt_x + 700.0, dt_y + 850.0, dt_x + 1050.0, dt_y + 1150.0, "A-FURN")

        # Kitchen Counter & Stove
        add_rect(75.0, 11875.0, 2925.0, 12425.0, "A-FURN")
        add_rect(650.0, 11950.0, 1350.0, 12400.0, "A-FURN")
        add_circle(850.0, 12175.0, 110.0, "A-FURN")
        add_circle(1150.0, 12175.0, 110.0, "A-FURN")

        # Carport Car Outline
        car_x1, car_y1 = 650.0, 300.0
        car_w, car_l = 1700.0, 4000.0
        add_rect(car_x1, car_y1, car_x1 + car_w, car_y1 + car_l, "A-FURN")
        add_line(car_x1 + 100.0, car_y1 + 1200.0, car_x1 + car_w - 100.0, car_y1 + 1200.0, "A-FURN")
        add_line(car_x1 + 150.0, car_y1 + 2800.0, car_x1 + car_w - 150.0, car_y1 + 2800.0, "A-FURN")
        add_centered_text("CARPORT (1 MOBIL)", car_x1 + car_w / 2.0, car_y1 + car_l / 2.0, 160.0, "A-FURN")

        # -------------------------------------------------------------
        # STEP 7: FLOOR TRANSITIONS (A-FLOR)
        # -------------------------------------------------------------
        add_rect(1900.0, 3000.0, 4500.0, 4500.0, "A-FLOR")
        add_line(1900.0, 3300.0, 4500.0, 3300.0, "A-FLOR")
        add_line(0.0, 100.0, 3000.0, 100.0, "A-FLOR")
        add_line(0.0, 12600.0, 6000.0, 12600.0, "A-FLOR")

        # -------------------------------------------------------------
        # STEP 8: CENTERED ROOM LABELS & FFL ELEVATION (A-ANNO-TEXT)
        # Placed at true clear room centroids
        # -------------------------------------------------------------
        print("Placing mathematically centered room names and FFL markers...")
        room_annotations = [
            ("TAMAN DEPAN", "9.0 m²", "-0.25", 4500.0, 1500.0),
            ("TERAS DEPAN", "3.8 m²", "-0.05", 3200.0, 3750.0),
            ("R. TAMU & KELUARGA", "11.5 m²", "±0.00", 1850.0, 6200.0),
            ("RUANG MAKAN", "6.5 m²", "±0.00", 1500.0, 7700.0),
            ("DAPUR & PANTRY", "4.5 m²", "-0.02", 1800.0, 11250.0),
            ("K. TIDUR UTAMA", "10.5 m²", "+0.03", 4500.0, 5050.0),
            ("KM / WC", "3.0 m²", "-0.05", 5000.0, 8900.0),
            ("K. TIDUR 2", "9.0 m²", "+0.03", 3900.0, 11500.0),
            ("TAMAN & SERVICE", "7.5 m²", "-0.15", 3000.0, 13300.0),
        ]

        for name, area, ffl, cx, cy in room_annotations:
            # Room Title (Center-Aligned)
            add_centered_text(name, cx, cy + 120.0, 200.0, "A-ANNO-TEXT")
            # Subtitle: Area & FFL
            add_centered_text(f"Luas: {area} | FFL: {ffl}", cx, cy - 100.0, 130.0, "A-ANNO-TEXT")

        # -------------------------------------------------------------
        # STEP 9: NATIVE HIERARCHICAL DIMENSIONS (A-DIMS-EXT & A-DIMS-INT)
        # 3-Tier Exterior Chains with Oblique Ticks & Scaled Text
        # Positioned INSIDE the grid bubble perimeter!
        # -------------------------------------------------------------
        print("Drafting native hierarchical dimension chains with architectural ticks...")

        # BOTTOM (South Elevation)
        # Tier 1 (Detailed Openings/Zones, Y = -800)
        add_dim_scaled(0.0, 0.0, 3000.0, 0.0, 1500.0, -800.0, 0.0, "A-DIMS-EXT", "3000")
        add_dim_scaled(3000.0, 0.0, 6000.0, 0.0, 4500.0, -800.0, 0.0, "A-DIMS-EXT", "3000")

        # Tier 2 (Grid Bays, Y = -1600)
        add_dim_scaled(0.0, 0.0, 3000.0, 0.0, 1500.0, -1600.0, 0.0, "A-DIMS-EXT", "3000")
        add_dim_scaled(3000.0, 0.0, 4500.0, 0.0, 3750.0, -1600.0, 0.0, "A-DIMS-EXT", "1500")
        add_dim_scaled(4500.0, 0.0, 6000.0, 0.0, 5250.0, -1600.0, 0.0, "A-DIMS-EXT", "1500")

        # Tier 3 (Overall Width, Y = -2400)
        add_dim_scaled(0.0, 0.0, 6000.0, 0.0, 3000.0, -2400.0, 0.0, "A-DIMS-EXT", "6000")

        # TOP (North / Rear Elevation)
        # Tier 1 (Rear Rooms, Y = 14800)
        add_dim_scaled(0.0, 14000.0, 3000.0, 14000.0, 1500.0, 14800.0, 0.0, "A-DIMS-EXT", "3000")
        add_dim_scaled(3000.0, 14000.0, 6000.0, 14000.0, 4500.0, 14800.0, 0.0, "A-DIMS-EXT", "3000")

        # Tier 2 (Grid Bays, Y = 15600)
        add_dim_scaled(0.0, 14000.0, 3000.0, 14000.0, 1500.0, 15600.0, 0.0, "A-DIMS-EXT", "3000")
        add_dim_scaled(3000.0, 14000.0, 6000.0, 14000.0, 4500.0, 15600.0, 0.0, "A-DIMS-EXT", "3000")

        # Tier 3 (Overall Width, Y = 16400)
        add_dim_scaled(0.0, 14000.0, 6000.0, 14000.0, 3000.0, 16400.0, 0.0, "A-DIMS-EXT", "6000")

        # LEFT (West Elevation)
        # Tier 1 (Detailed Segments, X = -800)
        add_dim_scaled(0.0, 0.0, 0.0, 4500.0, -800.0, 2250.0, 1.570796, "A-DIMS-EXT", "4500")
        add_dim_scaled(0.0, 4500.0, 0.0, 8000.0, -800.0, 6250.0, 1.570796, "A-DIMS-EXT", "3500")
        add_dim_scaled(0.0, 8000.0, 0.0, 10500.0, -800.0, 9250.0, 1.570796, "A-DIMS-EXT", "2500")
        add_dim_scaled(0.0, 10500.0, 0.0, 12500.0, -800.0, 11500.0, 1.570796, "A-DIMS-EXT", "2000")
        add_dim_scaled(0.0, 12500.0, 0.0, 14000.0, -800.0, 13250.0, 1.570796, "A-DIMS-EXT", "1500")

        # Tier 2 (Structural Zones, X = -1600)
        add_dim_scaled(0.0, 0.0, 0.0, 4500.0, -1600.0, 2250.0, 1.570796, "A-DIMS-EXT", "4500")
        add_dim_scaled(0.0, 4500.0, 0.0, 12500.0, -1600.0, 8500.0, 1.570796, "A-DIMS-EXT", "8000")
        add_dim_scaled(0.0, 12500.0, 0.0, 14000.0, -1600.0, 13250.0, 1.570796, "A-DIMS-EXT", "1500")

        # Tier 3 (Overall Depth, X = -2400)
        add_dim_scaled(0.0, 0.0, 0.0, 14000.0, -2400.0, 7000.0, 1.570796, "A-DIMS-EXT", "14000")

        # RIGHT (East Elevation)
        # Tier 1 (Detailed Rooms, X = 6800)
        add_dim_scaled(6000.0, 0.0, 6000.0, 3000.0, 6800.0, 1500.0, 1.570796, "A-DIMS-EXT", "3000")
        add_dim_scaled(6000.0, 3000.0, 6000.0, 4500.0, 6800.0, 3750.0, 1.570796, "A-DIMS-EXT", "1500")
        add_dim_scaled(6000.0, 4500.0, 6000.0, 8000.0, 6800.0, 6250.0, 1.570796, "A-DIMS-EXT", "3500")
        add_dim_scaled(6000.0, 8000.0, 6000.0, 10000.0, 6800.0, 9000.0, 1.570796, "A-DIMS-EXT", "2000")
        add_dim_scaled(6000.0, 10000.0, 6000.0, 12500.0, 6800.0, 11250.0, 1.570796, "A-DIMS-EXT", "2500")
        add_dim_scaled(6000.0, 12500.0, 6000.0, 14000.0, 6800.0, 13250.0, 1.570796, "A-DIMS-EXT", "1500")

        # Tier 2 (Building Depth, X = 7600)
        add_dim_scaled(6000.0, 4500.0, 6000.0, 12500.0, 7600.0, 8500.0, 1.570796, "A-DIMS-EXT", "8000")

        # Tier 3 (Overall Depth, X = 8400)
        add_dim_scaled(6000.0, 0.0, 6000.0, 14000.0, 8400.0, 7000.0, 1.570796, "A-DIMS-EXT", "14000")

        # INTERIOR DIMENSIONS (A-DIMS-INT)
        add_dim_scaled(3000.0, 4500.0, 6000.0, 4500.0, 4500.0, 4750.0, 0.0, "A-DIMS-INT", "3000")
        add_dim_scaled(3000.0, 4500.0, 3000.0, 8000.0, 3250.0, 6250.0, 1.570796, "A-DIMS-INT", "3500")
        add_dim_scaled(4500.0, 8000.0, 6000.0, 8000.0, 5250.0, 8250.0, 0.0, "A-DIMS-INT", "1500")
        add_dim_scaled(4500.0, 8000.0, 4500.0, 10000.0, 4750.0, 9000.0, 1.570796, "A-DIMS-INT", "2000")
        add_dim_scaled(3000.0, 10000.0, 6000.0, 10000.0, 4500.0, 10250.0, 0.0, "A-DIMS-INT", "3000")
        add_dim_scaled(3000.0, 10000.0, 3000.0, 12500.0, 3250.0, 11250.0, 1.570796, "A-DIMS-INT", "2500")
        add_dim_scaled(0.0, 7500.0, 3000.0, 7500.0, 1500.0, 7500.0, 0.0, "A-DIMS-INT", "3000")

        # -------------------------------------------------------------
        # STEP 10: STRUCTURAL GRID & BUBBLES (A-GRID & A-ANNO-SYMB)
        # Positioned OUTSIDE all dimensions with centered labels
        # -------------------------------------------------------------
        print("Drafting structural grid lines and centered bubbles...")
        grid_x = [
            ("1", 0.0),
            ("2", 3000.0),
            ("3", 4500.0),
            ("4", 6000.0)
        ]
        grid_y = [
            ("A", 0.0),
            ("B", 3000.0),
            ("C", 4500.0),
            ("D", 8000.0),
            ("E", 10000.0),
            ("F", 12500.0),
            ("G", 14000.0)
        ]

        # Geometry bounds for grid lines:
        # Bottom grid line stops at Y = -3200 (bubble tangent)
        # Top grid line stops at Y = 17200 (bubble tangent)
        # Left grid line stops at X = -3200 (bubble tangent)
        # Right grid line stops at X = 9200 (bubble tangent)
        y_bot_grid = -3200.0
        y_top_grid = 17200.0
        x_left_grid = -3200.0
        x_right_grid = 9200.0
        bubble_rad = 300.0

        for tag, gx in grid_x:
            # Vertical Grid line (cleanly stops at bubble edge)
            add_line(gx, y_bot_grid, gx, y_top_grid, "A-GRID")

            # Bottom Bubble (Center at Y = -3500)
            add_circle(gx, y_bot_grid - bubble_rad, bubble_rad, "A-ANNO-SYMB")
            add_centered_text(tag, gx, y_bot_grid - bubble_rad, 220.0, "A-ANNO-SYMB")

            # Top Bubble (Center at Y = 17500)
            add_circle(gx, y_top_grid + bubble_rad, bubble_rad, "A-ANNO-SYMB")
            add_centered_text(tag, gx, y_top_grid + bubble_rad, 220.0, "A-ANNO-SYMB")

        for tag, gy in grid_y:
            # Horizontal Grid line (cleanly stops at bubble edge)
            add_line(x_left_grid, gy, x_right_grid, gy, "A-GRID")

            # Left Bubble (Center at X = -3500)
            add_circle(x_left_grid - bubble_rad, gy, bubble_rad, "A-ANNO-SYMB")
            add_centered_text(tag, x_left_grid - bubble_rad, gy, 220.0, "A-ANNO-SYMB")

            # Right Bubble (Center at X = 9500)
            add_circle(x_right_grid + bubble_rad, gy, bubble_rad, "A-ANNO-SYMB")
            add_centered_text(tag, x_right_grid + bubble_rad, gy, 220.0, "A-ANNO-SYMB")

        # -------------------------------------------------------------
        # STEP 11: TITLE BLOCK & NORTH ARROW
        # Separated with zero overlap
        # -------------------------------------------------------------
        print("Drafting architectural title block and independent North arrow...")
        # Centered Title Block beneath all dimensions and bubbles (Y = -4600)
        title_cx = 3000.0
        title_y = -4400.0
        add_centered_text("DENAH RUMAH TINGGAL TYPE 50 / 84", title_cx, title_y, 350.0, "A-ANNO-TEXT")
        add_centered_text("SKALA 1 : 100   |   SATUAN : MILIMETER (mm)", title_cx, title_y - 450.0, 200.0, "A-ANNO-TEXT")
        add_centered_text("STANDAR ARSITEKTUR & AIA CAD LAYER STANDARDS", title_cx, title_y - 750.0, 160.0, "A-ANNO-TEXT")

        # Title rule bar
        add_line(500.0, title_y - 950.0, 5500.0, title_y - 950.0, "A-ANNO-TEXT")
        add_line(500.0, title_y - 980.0, 5500.0, title_y - 980.0, "A-ANNO-TEXT")

        # North Arrow placed in clear top-right area (X = 9500, Y = 16000)
        na_x = 9500.0
        na_y = 16000.0
        add_circle(na_x, na_y, 350.0, "A-ANNO-SYMB")
        add_line(na_x, na_y - 350.0, na_x, na_y + 350.0, "A-ANNO-SYMB")
        # Arrowhead
        add_line(na_x, na_y + 350.0, na_x - 100.0, na_y + 120.0, "A-ANNO-SYMB")
        add_line(na_x, na_y + 350.0, na_x + 100.0, na_y + 120.0, "A-ANNO-SYMB")
        add_line(na_x - 100.0, na_y + 120.0, na_x + 100.0, na_y + 120.0, "A-ANNO-SYMB")
        add_centered_text("U", na_x, na_y + 480.0, 220.0, "A-ANNO-SYMB")

        # -------------------------------------------------------------
        # STEP 12: UCS ICON RELOCATION & REGEN
        # -------------------------------------------------------------
        print("Regenerating drawing and fitting zoom extents...")
        try:
            doc.SendCommand("UCSICON Noorigin\n")
            doc.Regen(1)
            app.ZoomExtents()
        except Exception:
            pass

        print("=" * 75)
        print("OVERHAUL COMPLETE! PROFESSIONAL ARCHITECTURAL DRAWING GENERATED.")
        print(f"Total ModelSpace Entities: {ms.Count}")
        print("=" * 75)

if __name__ == "__main__":
    import threading
    t = threading.Thread(target=create_floor_plan)
    t.start()
    t.join()
