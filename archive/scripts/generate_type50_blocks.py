"""
generate_type50_blocks.py - Professional Production Architectural CAD Generator
Type 50/84 Minimalist Modern Residential House - ZWCAD 2023 (Drawing1.dwg).

Full Architectural Block & Modular Drafting Implementation:
1. Wall Cut Profile & Native ANSI31 Brick Hatch:
   - Closed polyline walls (0.40 mm Cyan)
   - Native ANSI31 diagonal brick hatching inside all wall cavities (Layer A-WALL-PATS)
2. Modular Architectural Blocks (AcDbBlockReference):
   - BLK_KOLOM_15X15 (150x150 mm practical RC columns with SOLID hatch)
   - BLK_DOOR_D1_HORIZ_900, BLK_DOOR_D2_VERT_800, BLK_DOOR_D3_VERT_700, BLK_DOOR_D4_HORIZ_800
   - BLK_WIN_W1_1400, BLK_WIN_W2_1200, BLK_WIN_W3_1200, BLK_WIN_W4_900
   - BLK_SAN_CLOSET, BLK_SAN_SHOWER, BLK_SAN_DRAIN, BLK_SAN_SINK
   - BLK_FURN_MASTER_BED, BLK_FURN_SINGLE_BED, BLK_FURN_WARDROBE, BLK_FURN_STUDY, BLK_FURN_LIVING, BLK_FURN_DINING, BLK_FURN_STOVE, BLK_FURN_CAR
3. Floor Texture Hatches:
   - KM/WC ceramic floor tile hatch (NET 300x300 mm)
   - Teras depan tile pattern (NET 600x600 mm)
4. Clean Structural Grid (A-GRID & A-ANNO-SYMB):
   - Linetype: CENTER2 (loaded from zwcad.lin)
   - Lineweight: 0.09 mm (Thin dashed centerline)
   - LTSCALE = 60.0
   - Lines stop cleanly at bubble circles (zero line piercing through text)
   - Bubbles (Ø600 mm) placed outside all dimension tiers
   - Bubble letters/numbers 100% middle-centered (acAlignmentMiddleCenter = 10)
5. 3-Tier Hierarchical Dimensions (AcDbRotatedDimension):
   - TextHeight = 180.0 mm (skalatis 1:100)
   - Arrowhead = Oblique 45 deg tick (120.0 mm)
   - Zero collision with building or bubbles
6. Precision Centered Room Annotations (A-ANNO-TEXT):
   - Middle-center alignment at unobstructed room centroids
"""

from __future__ import annotations

import math
import sys
from pathlib import Path

# Setup paths
AIGRAV_DIR = Path(r"C:\AIGRAV")
ZWCAD_DIR = Path(r"C:\ZWCAD-MCP")

for p in (AIGRAV_DIR, ZWCAD_DIR):
    if str(p) not in sys.path:
        sys.path.insert(0, str(p))

import cad_bridge

def run():
    print("=" * 75)
    print("STARTING COMPLETE ARCHITECTURAL CAD GENERATOR WITH NATIVE HATCH & BLOCKS")
    print("=" * 75)

    with cad_bridge.cad_context() as (app, _):
        # 1. Target Drawing1.dwg explicitly
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
        print(f"Target Document: {doc.Name} (Initial count: {ms.Count})")

        # 2. Fast Clear Drawing1.dwg using SelectionSet
        if ms.Count > 0:
            print(f"Clearing {ms.Count} existing entities...")
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
                print(f"Erase note: {e}")

        # 3. Load CENTER2 and DASHED Linetypes from zwcad.lin
        for lt_name in ["CENTER2", "DASHED"]:
            try:
                doc.Linetypes.Load(lt_name, "zwcad.lin")
            except Exception:
                pass

        try:
            doc.SetVariable("LTSCALE", 60.0)
        except Exception:
            pass

        # 4. Setup AIA Standard CAD Layers
        layers_def = [
            ("A-GRID", 1, "CENTER2", 9),          # Red, CENTER2, 0.09 mm (Thin dashed grid)
            ("S-COLS", 7, "Continuous", 35),      # White, 0.35 mm
            ("A-WALL-FULL", 4, "Continuous", 40), # Cyan, 0.40 mm (Cut wall outline)
            ("A-WALL-PATS", 8, "Continuous", 9),  # Dark Gray, 0.09 mm (Brick ANSI31 Hatch)
            ("A-DOOR", 3, "Continuous", 25),      # Green, 0.25 mm
            ("A-GLAZ", 4, "Continuous", 20),      # Cyan, 0.20 mm
            ("A-FLOR", 9, "Continuous", 15),      # Light Gray, 0.15 mm
            ("A-PLBG", 5, "Continuous", 25),      # Blue, 0.25 mm
            ("A-FURN", 8, "Continuous", 15),      # Dark Gray, 0.15 mm
            ("A-DIMS-EXT", 3, "Continuous", 18),    # Green, 0.18 mm
            ("A-DIMS-INT", 2, "Continuous", 18),    # Yellow, 0.18 mm
            ("A-ANNO-TEXT", 2, "Continuous", 25),   # Yellow, 0.25 mm
            ("A-ANNO-SYMB", 1, "Continuous", 25),   # Red, 0.25 mm
        ]

        for lay_name, col_idx, lt, lw in layers_def:
            try:
                l_obj = doc.Layers.Add(lay_name)
            except Exception:
                l_obj = doc.Layers.Item(lay_name)
            l_obj.Color = int(col_idx)
            try:
                l_obj.Linetype = str(lt)
            except Exception:
                pass
            try:
                l_obj.LineWeight = int(lw)
            except Exception:
                pass

        print(f"Configured {len(layers_def)} AIA CAD layers with lineweights and linetypes.")

        # Helper primitives
        def add_line_to(space, x1, y1, x2, y2, layer):
            p1 = cad_bridge.point3(x1, y1)
            p2 = cad_bridge.point3(x2, y2)
            ent = space.AddLine(p1, p2)
            ent.Layer = layer
            return ent

        def add_polyline_to(space, pts, closed, layer):
            flat = []
            for p in pts:
                flat.extend([float(p[0]), float(p[1])])
            import win32com.client, pythoncom
            arr = win32com.client.VARIANT(pythoncom.VT_ARRAY | pythoncom.VT_R8, flat)
            ent = space.AddLightWeightPolyline(arr)
            ent.Closed = bool(closed)
            ent.Layer = layer
            return ent

        def add_rect_to(space, x1, y1, x2, y2, layer):
            pts = [[x1, y1], [x2, y1], [x2, y2], [x1, y2]]
            return add_polyline_to(space, pts, True, layer)

        def add_circle_to(space, cx, cy, r, layer):
            c = cad_bridge.point3(cx, cy)
            ent = space.AddCircle(c, float(r))
            ent.Layer = layer
            return ent

        def add_arc_to(space, cx, cy, r, sa, ea, layer):
            c = cad_bridge.point3(cx, cy)
            ent = space.AddArc(c, float(r), float(sa), float(ea))
            ent.Layer = layer
            return ent

        def add_centered_text_to(space, txt, cx, cy, h, layer, rot=0.0):
            pt = cad_bridge.point3(cx, cy)
            ent = space.AddText(str(txt), pt, float(h))
            ent.Alignment = 10  # acAlignmentMiddleCenter
            ent.TextAlignmentPoint = pt
            ent.Rotation = float(rot)
            ent.Layer = layer
            return ent

        def get_or_create_block(name, base_pt=(0.0, 0.0)):
            origin = cad_bridge.point3(base_pt[0], base_pt[1])
            try:
                blk = doc.Blocks.Item(name)
                for i in range(blk.Count - 1, -1, -1):
                    try:
                        blk.Item(i).Delete()
                    except Exception:
                        pass
            except Exception:
                blk = doc.Blocks.Add(origin, name)
            return blk

        def insert_block(name, ins_x, ins_y, rot_rad=0.0, sx=1.0, sy=1.0, layer="0"):
            pt = cad_bridge.point3(ins_x, ins_y)
            ent = ms.InsertBlock(pt, name, float(sx), float(sy), 1.0, float(rot_rad))
            if layer:
                ent.Layer = layer
            return ent

        # -------------------------------------------------------------
        # 5. DEFINE ARCHITECTURAL BLOCKS
        # -------------------------------------------------------------
        print("Defining architectural blocks in library...")

        # --- A. COLUMN BLOCK (150x150 mm with Solid Hatch) ---
        blk_col = get_or_create_block("BLK_KOLOM_15X15")
        col_poly = add_rect_to(blk_col, -75.0, -75.0, 75.0, 75.0, "S-COLS")
        try:
            import win32com.client, pythoncom
            hatch = blk_col.AddHatch(0, "SOLID", False)
            hatch.Layer = "S-COLS"
            loop = win32com.client.VARIANT(pythoncom.VT_ARRAY | pythoncom.VT_DISPATCH, [col_poly])
            hatch.AppendOuterLoop(loop)
            hatch.Evaluate()
        except Exception:
            pass

        # --- B. DOOR BLOCKS (Direct Horizontal & Vertical Variants) ---
        # 1. Main Door D1 (Horizontal 900 mm, swings +Y)
        b_d1 = get_or_create_block("BLK_DOOR_D1_HORIZ_900")
        add_rect_to(b_d1, -25.0, -50.0, 25.0, 50.0, "A-DOOR")
        add_rect_to(b_d1, 875.0, -50.0, 925.0, 50.0, "A-DOOR")
        add_rect_to(b_d1, 0.0, 0.0, 35.0, 900.0, "A-DOOR")
        add_arc_to(b_d1, 0.0, 0.0, 900.0, 0.0, math.pi / 2.0, "A-DOOR")
        add_centered_text_to(b_d1, "D1", 450.0, 200.0, 150.0, "A-DOOR")

        # 2. Vertical Bedroom Door D2 (Vertical 800 mm, swings +X)
        b_d2 = get_or_create_block("BLK_DOOR_D2_VERT_800")
        add_rect_to(b_d2, -50.0, -25.0, 50.0, 25.0, "A-DOOR")
        add_rect_to(b_d2, -50.0, 775.0, 50.0, 825.0, "A-DOOR")
        add_rect_to(b_d2, 0.0, 0.0, 800.0, 35.0, "A-DOOR")
        add_arc_to(b_d2, 0.0, 0.0, 800.0, 0.0, math.pi / 2.0, "A-DOOR")
        add_centered_text_to(b_d2, "D2", 200.0, 400.0, 150.0, "A-DOOR")

        # 3. Vertical KM/WC Door D3 (Vertical 700 mm, swings +X)
        b_d3 = get_or_create_block("BLK_DOOR_D3_VERT_700")
        add_rect_to(b_d3, -50.0, -25.0, 50.0, 25.0, "A-DOOR")
        add_rect_to(b_d3, -50.0, 675.0, 50.0, 725.0, "A-DOOR")
        add_rect_to(b_d3, 0.0, 0.0, 700.0, 35.0, "A-DOOR")
        add_arc_to(b_d3, 0.0, 0.0, 700.0, 0.0, math.pi / 2.0, "A-DOOR")
        add_centered_text_to(b_d3, "D3", 200.0, 350.0, 150.0, "A-DOOR")

        # 4. Service Door D4 (Horizontal 800 mm, swings +Y)
        b_d4 = get_or_create_block("BLK_DOOR_D4_HORIZ_800")
        add_rect_to(b_d4, -25.0, -50.0, 25.0, 50.0, "A-DOOR")
        add_rect_to(b_d4, 775.0, -50.0, 825.0, 50.0, "A-DOOR")
        add_rect_to(b_d4, 0.0, 0.0, 35.0, 800.0, "A-DOOR")
        add_arc_to(b_d4, 0.0, 0.0, 800.0, 0.0, math.pi / 2.0, "A-DOOR")
        add_centered_text_to(b_d4, "D4", 400.0, 200.0, 150.0, "A-DOOR")

        # --- C. WINDOW BLOCKS ---
        def define_window_block(blk_name, width, tag, leaves=2):
            b = get_or_create_block(blk_name)
            w_half = width / 2.0
            add_rect_to(b, -w_half, -50.0, -w_half + 50.0, 50.0, "A-GLAZ")
            add_rect_to(b, w_half - 50.0, -50.0, w_half, 50.0, "A-GLAZ")
            add_line_to(b, -w_half, -50.0, w_half, -50.0, "A-GLAZ")
            add_line_to(b, -w_half, 50.0, w_half, 50.0, "A-GLAZ")
            add_line_to(b, -w_half + 50.0, -10.0, w_half - 50.0, -10.0, "A-GLAZ")
            add_line_to(b, -w_half + 50.0, 10.0, w_half - 50.0, 10.0, "A-GLAZ")
            if leaves == 2:
                add_rect_to(b, -25.0, -50.0, 25.0, 50.0, "A-GLAZ")
            add_centered_text_to(b, tag, 0.0, 180.0, 150.0, "A-GLAZ")

        define_window_block("BLK_WIN_W1_1400", 1400.0, "W1", 2)
        define_window_block("BLK_WIN_W2_1200", 1200.0, "W2", 2)
        define_window_block("BLK_WIN_W3_1200", 1200.0, "W3", 2)
        define_window_block("BLK_WIN_W4_900", 900.0, "W4", 1)

        # --- D. SANITARY BLOCKS ---
        blk_closet = get_or_create_block("BLK_SAN_CLOSET")
        add_rect_to(blk_closet, -250.0, -200.0, 250.0, 0.0, "A-PLBG")
        add_rect_to(blk_closet, -200.0, 0.0, 200.0, 420.0, "A-PLBG")
        add_circle_to(blk_closet, 0.0, 220.0, 120.0, "A-PLBG")
        add_circle_to(blk_closet, 0.0, -100.0, 25.0, "A-PLBG")

        blk_shower = get_or_create_block("BLK_SAN_SHOWER")
        add_line_to(blk_shower, 0.0, 0.0, 900.0, 0.0, "A-PLBG")
        add_line_to(blk_shower, 0.0, 0.0, 0.0, 900.0, "A-PLBG")
        add_circle_to(blk_shower, 450.0, 450.0, 120.0, "A-PLBG")
        add_line_to(blk_shower, 450.0, 450.0, 900.0, 450.0, "A-PLBG")

        blk_fd = get_or_create_block("BLK_SAN_DRAIN")
        add_rect_to(blk_fd, -100.0, -100.0, 100.0, 100.0, "A-PLBG")
        add_circle_to(blk_fd, 0.0, 0.0, 75.0, "A-PLBG")
        add_centered_text_to(blk_fd, "FD", 0.0, 180.0, 100.0, "A-PLBG")

        blk_sink = get_or_create_block("BLK_SAN_SINK")
        add_rect_to(blk_sink, -400.0, -225.0, 400.0, 225.0, "A-PLBG")
        add_rect_to(blk_sink, -360.0, -180.0, -40.0, 180.0, "A-PLBG")
        add_rect_to(blk_sink, 40.0, -180.0, 360.0, 180.0, "A-PLBG")
        add_circle_to(blk_sink, 0.0, 190.0, 25.0, "A-PLBG")

        # --- E. FURNITURE BLOCKS ---
        blk_mbed = get_or_create_block("BLK_FURN_MASTER_BED")
        add_rect_to(blk_mbed, 0.0, 0.0, 1600.0, 2000.0, "A-FURN")
        add_rect_to(blk_mbed, -50.0, 1950.0, 1650.0, 2050.0, "A-FURN")
        add_rect_to(blk_mbed, 100.0, 1550.0, 650.0, 1900.0, "A-FURN")
        add_rect_to(blk_mbed, 950.0, 1550.0, 1500.0, 1900.0, "A-FURN")
        add_line_to(blk_mbed, 0.0, 600.0, 1600.0, 600.0, "A-FURN")
        add_rect_to(blk_mbed, -500.0, 1550.0, -50.0, 2000.0, "A-FURN")
        add_rect_to(blk_mbed, 1650.0, 1550.0, 2100.0, 2000.0, "A-FURN")

        blk_sbed = get_or_create_block("BLK_FURN_SINGLE_BED")
        add_rect_to(blk_sbed, 0.0, 0.0, 1200.0, 2000.0, "A-FURN")
        add_rect_to(blk_sbed, -30.0, 1950.0, 1230.0, 2030.0, "A-FURN")
        add_rect_to(blk_sbed, 200.0, 1550.0, 1000.0, 1900.0, "A-FURN")
        add_line_to(blk_sbed, 0.0, 600.0, 1200.0, 600.0, "A-FURN")

        blk_ward = get_or_create_block("BLK_FURN_WARDROBE")
        add_rect_to(blk_ward, 0.0, 0.0, 1400.0, 600.0, "A-FURN")
        add_line_to(blk_ward, 0.0, 0.0, 1400.0, 600.0, "A-FURN")
        add_line_to(blk_ward, 0.0, 600.0, 1400.0, 0.0, "A-FURN")

        blk_study = get_or_create_block("BLK_FURN_STUDY_SET")
        add_rect_to(blk_study, 0.0, 0.0, 1000.0, 500.0, "A-FURN")
        add_circle_to(blk_study, 500.0, -220.0, 180.0, "A-FURN")

        blk_living = get_or_create_block("BLK_FURN_LIVING_SET")
        add_rect_to(blk_living, 0.0, 0.0, 850.0, 2000.0, "A-FURN")
        for i in range(3):
            add_rect_to(blk_living, 150.0, i * 650.0 + 50.0, 800.0, (i + 1) * 650.0 - 50.0, "A-FURN")
        add_rect_to(blk_living, 1100.0, 550.0, 1600.0, 1450.0, "A-FURN")
        add_rect_to(blk_living, 2200.0, 300.0, 2550.0, 1700.0, "A-FURN")
        add_centered_text_to(blk_living, "TV", 2375.0, 1000.0, 120.0, "A-FURN")

        blk_dining = get_or_create_block("BLK_FURN_DINING_SET")
        add_rect_to(blk_dining, 0.0, 0.0, 1200.0, 800.0, "A-FURN")
        add_rect_to(blk_dining, 150.0, -350.0, 500.0, -50.0, "A-FURN")
        add_rect_to(blk_dining, 700.0, -350.0, 1050.0, -50.0, "A-FURN")
        add_rect_to(blk_dining, 150.0, 850.0, 500.0, 1150.0, "A-FURN")
        add_rect_to(blk_dining, 700.0, 850.0, 1050.0, 1150.0, "A-FURN")

        blk_stove = get_or_create_block("BLK_FURN_STOVE")
        add_rect_to(blk_stove, 0.0, 0.0, 700.0, 420.0, "A-FURN")
        add_circle_to(blk_stove, 200.0, 210.0, 110.0, "A-FURN")
        add_circle_to(blk_stove, 500.0, 210.0, 110.0, "A-FURN")

        blk_car = get_or_create_block("BLK_FURN_CAR")
        add_rect_to(blk_car, 0.0, 0.0, 1700.0, 4000.0, "A-FURN")
        add_line_to(blk_car, 100.0, 1100.0, 1600.0, 1100.0, "A-FURN")
        add_line_to(blk_car, 150.0, 2800.0, 1550.0, 2800.0, "A-FURN")
        add_line_to(blk_car, 100.0, 1100.0, 150.0, 2800.0, "A-FURN")
        add_line_to(blk_car, 1600.0, 1100.0, 1550.0, 2800.0, "A-FURN")
        add_centered_text_to(blk_car, "CARPORT (1 MOBIL)", 850.0, 1950.0, 160.0, "A-FURN")

        print("All architectural blocks successfully defined.")

        # -------------------------------------------------------------
        # 6. INSERT BLOCKS INTO MODEL SPACE
        # -------------------------------------------------------------
        print("Inserting architectural blocks into drawing...")

        # 1. Structural Columns (21 columns)
        column_locs = [
            (0.0, 0.0), (3000.0, 0.0), (6000.0, 0.0),
            (6000.0, 3000.0),
            (0.0, 4500.0), (3000.0, 4500.0), (6000.0, 4500.0),
            (0.0, 8000.0), (3000.0, 8000.0), (4500.0, 8000.0), (6000.0, 8000.0),
            (0.0, 10500.0), (3000.0, 10000.0), (4500.0, 10000.0), (6000.0, 10000.0),
            (0.0, 12500.0), (3000.0, 12500.0), (6000.0, 12500.0),
            (0.0, 14000.0), (3000.0, 14000.0), (6000.0, 14000.0),
        ]
        for cx, cy in column_locs:
            insert_block("BLK_KOLOM_15X15", cx, cy)

        # 2. Doors (Direct Unrotated Placements)
        insert_block("BLK_DOOR_D1_HORIZ_900", 1900.0, 4500.0, 0.0)
        insert_block("BLK_DOOR_D2_VERT_800", 3000.0, 7100.0, 0.0)
        insert_block("BLK_DOOR_D2_VERT_800", 3000.0, 10100.0, 0.0)
        insert_block("BLK_DOOR_D3_VERT_700", 4500.0, 8200.0, 0.0)
        insert_block("BLK_DOOR_D4_HORIZ_800", 500.0, 12500.0, 0.0)

        # 3. Windows
        insert_block("BLK_WIN_W1_1400", 4500.0, 4500.0, 0.0)
        insert_block("BLK_WIN_W2_1200", 4500.0, 12500.0, 0.0)
        insert_block("BLK_WIN_W3_1200", 1000.0, 4500.0, 0.0)
        insert_block("BLK_WIN_W4_900", 2150.0, 12500.0, 0.0)

        # 4. Sanitary Fixtures
        insert_block("BLK_SAN_CLOSET", 5400.0, 8300.0, 0.0)
        insert_block("BLK_SAN_SHOWER", 5100.0, 9100.0, 0.0)
        insert_block("BLK_SAN_DRAIN", 4800.0, 9600.0, 0.0)
        insert_block("BLK_SAN_SINK", 2150.0, 12200.0, 0.0)

        # 5. Furniture
        insert_block("BLK_FURN_MASTER_BED", 3950.0, 5600.0, 0.0)
        insert_block("BLK_FURN_WARDROBE", 4400.0, 4600.0, 0.0)
        insert_block("BLK_FURN_SINGLE_BED", 4600.0, 10350.0, 0.0)
        insert_block("BLK_FURN_STUDY_SET", 3200.0, 11900.0, 0.0)
        insert_block("BLK_FURN_WARDROBE", 3200.0, 10400.0, 0.0, sx=0.85)
        insert_block("BLK_FURN_LIVING_SET", 350.0, 5200.0, 0.0)
        insert_block("BLK_FURN_DINING_SET", 900.0, 8400.0, 0.0)
        add_rect_to(ms, 75.0, 11875.0, 2925.0, 12425.0, "A-FURN")
        insert_block("BLK_FURN_STOVE", 650.0, 11950.0, 0.0)
        insert_block("BLK_FURN_CAR", 650.0, 300.0, 0.0)

        # -------------------------------------------------------------
        # 7. CUT WALLS & NATIVE ANSI31 BRICK HATCHING
        # -------------------------------------------------------------
        print("Drafting cut walls (150 mm) with native ANSI31 brick hatching...")
        w_half = 75.0

        def make_wall_with_hatch(x1, y1, x2, y2):
            dx = x2 - x1
            dy = y2 - y1
            length = math.hypot(dx, dy)
            if length < 5.0:
                return
            ux = dx / length
            uy = dy / length
            nx = -uy * w_half
            ny = ux * w_half

            # Closed rectangle polyline for wall cut profile
            pts = [
                [x1 + nx, y1 + ny],
                [x2 + nx, y2 + ny],
                [x2 - nx, y2 - ny],
                [x1 - nx, y1 - ny]
            ]
            wall_pl = add_polyline_to(ms, pts, True, "A-WALL-FULL")

            # Native ANSI31 Brick Hatch inside wall cavity
            try:
                import win32com.client, pythoncom
                hatch = ms.AddHatch(0, "ANSI31", False)
                hatch.Layer = "A-WALL-PATS"
                hatch.PatternScale = 80.0
                loop = win32com.client.VARIANT(pythoncom.VT_ARRAY | pythoncom.VT_DISPATCH, [wall_pl])
                hatch.AppendOuterLoop(loop)
                hatch.Evaluate()
            except Exception:
                pass

        walls = [
            # Perimeter Party Walls
            (0.0, 4500.0, 0.0, 12500.0),
            (6000.0, 4500.0, 6000.0, 12500.0),
            # Front Wall (Living & Master Bed)
            (0.0, 4500.0, 400.0, 4500.0),
            (1600.0, 4500.0, 1900.0, 4500.0),
            (2800.0, 4500.0, 3800.0, 4500.0),
            (5200.0, 4500.0, 6000.0, 4500.0),
            # Spine Wall (X=3000)
            (3000.0, 4500.0, 3000.0, 7100.0),
            (3000.0, 7900.0, 3000.0, 8000.0),
            (3000.0, 8000.0, 3000.0, 8200.0),
            (3000.0, 9100.0, 3000.0, 10000.0),
            (3000.0, 10000.0, 3000.0, 10100.0),
            (3000.0, 10900.0, 3000.0, 12500.0),
            # Mid Wall (Y=8000)
            (3000.0, 8000.0, 6000.0, 8000.0),
            # KM/WC Walls
            (4500.0, 8000.0, 4500.0, 8200.0),
            (4500.0, 8900.0, 4500.0, 10000.0),
            (4500.0, 10000.0, 6000.0, 10000.0),
            # Rear Facade Wall (Y=12500)
            (0.0, 12500.0, 500.0, 12500.0),
            (1300.0, 12500.0, 1700.0, 12500.0),
            (2600.0, 12500.0, 3900.0, 12500.0),
            (5100.0, 12500.0, 6000.0, 12500.0),
            # Boundary & Garden Walls
            (0.0, 0.0, 0.0, 4500.0),
            (0.0, 12500.0, 0.0, 14000.0),
            (6000.0, 12500.0, 6000.0, 14000.0),
            (0.0, 14000.0, 6000.0, 14000.0),
        ]
        for x1, y1, x2, y2 in walls:
            make_wall_with_hatch(x1, y1, x2, y2)

        # -------------------------------------------------------------
        # 8. FLOOR TEXTURES & TRANSITIONS (A-FLOR)
        # -------------------------------------------------------------
        print("Drafting floor ceramic patterns and transitions...")
        # KM/WC Ceramic Floor Hatch (300x300 mm NET pattern)
        km_floor_pl = add_rect_to(ms, 4575.0, 8075.0, 5925.0, 9925.0, "A-FLOR")
        try:
            import win32com.client, pythoncom
            h_km = ms.AddHatch(0, "NET", False)
            h_km.Layer = "A-FLOR"
            h_km.PatternScale = 300.0
            loop_k = win32com.client.VARIANT(pythoncom.VT_ARRAY | pythoncom.VT_DISPATCH, [km_floor_pl])
            h_km.AppendOuterLoop(loop_k)
            h_km.Evaluate()
        except Exception:
            pass

        # Teras Depan tile pattern (600x600 mm NET pattern)
        teras_pl = add_rect_to(ms, 1900.0, 3000.0, 4500.0, 4500.0, "A-FLOR")
        add_line_to(ms, 1900.0, 3300.0, 4500.0, 3300.0, "A-FLOR")
        try:
            import win32com.client, pythoncom
            h_teras = ms.AddHatch(0, "NET", False)
            h_teras.Layer = "A-FLOR"
            h_teras.PatternScale = 600.0
            loop_t = win32com.client.VARIANT(pythoncom.VT_ARRAY | pythoncom.VT_DISPATCH, [teras_pl])
            h_teras.AppendOuterLoop(loop_t)
            h_teras.Evaluate()
        except Exception:
            pass

        add_line_to(ms, 0.0, 100.0, 3000.0, 100.0, "A-FLOR")
        add_line_to(ms, 0.0, 12600.0, 6000.0, 12600.0, "A-FLOR")

        # -------------------------------------------------------------
        # 9. CENTERED ROOM ANNOTATIONS (A-ANNO-TEXT)
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
            add_centered_text_to(ms, name, cx, cy + 120.0, 200.0, "A-ANNO-TEXT")
            add_centered_text_to(ms, f"Luas: {area} | FFL: {ffl}", cx, cy - 100.0, 130.0, "A-ANNO-TEXT")

        # -------------------------------------------------------------
        # 10. SCALED HIERARCHICAL DIMENSIONS (A-DIMS-EXT & A-DIMS-INT)
        # True 1:100 scale, oblique slash ticks, 3-tier clear zones
        # -------------------------------------------------------------
        print("Placing native 1:100 scaled hierarchical dimensions...")
        def add_dim_scaled(x1, y1, x2, y2, dx, dy, rot, layer, override=""):
            ext1 = cad_bridge.point3(x1, y1)
            ext2 = cad_bridge.point3(x2, y2)
            line_pt = cad_bridge.point3(dx, dy)
            ent = ms.AddDimRotated(ext1, ext2, line_pt, float(rot))
            ent.Layer = layer
            try:
                ent.TextHeight = 180.0
                ent.ArrowheadSize = 120.0
                ent.Arrowhead1Type = 12  # acArrowOblique
                ent.Arrowhead2Type = 12
                ent.ExtensionLineExtend = 120.0
                ent.ExtensionLineOffset = 150.0
                ent.TextGap = 60.0
                ent.VerticalTextPosition = 1
                ent.DimensionLineColor = 3
                ent.ExtensionLineColor = 3
                ent.TextColor = 2
            except Exception:
                pass
            if override:
                ent.TextOverride = str(override)
            return ent

        # BOTTOM (South)
        add_dim_scaled(0.0, 0.0, 3000.0, 0.0, 1500.0, -800.0, 0.0, "A-DIMS-EXT", "3000")
        add_dim_scaled(3000.0, 0.0, 6000.0, 0.0, 4500.0, -800.0, 0.0, "A-DIMS-EXT", "3000")
        add_dim_scaled(0.0, 0.0, 3000.0, 0.0, 1500.0, -1600.0, 0.0, "A-DIMS-EXT", "3000")
        add_dim_scaled(3000.0, 0.0, 4500.0, 0.0, 3750.0, -1600.0, 0.0, "A-DIMS-EXT", "1500")
        add_dim_scaled(4500.0, 0.0, 6000.0, 0.0, 5250.0, -1600.0, 0.0, "A-DIMS-EXT", "1500")
        add_dim_scaled(0.0, 0.0, 6000.0, 0.0, 3000.0, -2400.0, 0.0, "A-DIMS-EXT", "6000")

        # TOP (North)
        add_dim_scaled(0.0, 14000.0, 3000.0, 14000.0, 1500.0, 14800.0, 0.0, "A-DIMS-EXT", "3000")
        add_dim_scaled(3000.0, 14000.0, 6000.0, 14000.0, 4500.0, 14800.0, 0.0, "A-DIMS-EXT", "3000")
        add_dim_scaled(0.0, 14000.0, 3000.0, 14000.0, 1500.0, 15600.0, 0.0, "A-DIMS-EXT", "3000")
        add_dim_scaled(3000.0, 14000.0, 6000.0, 14000.0, 4500.0, 15600.0, 0.0, "A-DIMS-EXT", "3000")
        add_dim_scaled(0.0, 14000.0, 6000.0, 14000.0, 3000.0, 16400.0, 0.0, "A-DIMS-EXT", "6000")

        # LEFT (West)
        add_dim_scaled(0.0, 0.0, 0.0, 4500.0, -800.0, 2250.0, 1.570796, "A-DIMS-EXT", "4500")
        add_dim_scaled(0.0, 4500.0, 0.0, 8000.0, -800.0, 6250.0, 1.570796, "A-DIMS-EXT", "3500")
        add_dim_scaled(0.0, 8000.0, 0.0, 10500.0, -800.0, 9250.0, 1.570796, "A-DIMS-EXT", "2500")
        add_dim_scaled(0.0, 10500.0, 0.0, 12500.0, -800.0, 11500.0, 1.570796, "A-DIMS-EXT", "2000")
        add_dim_scaled(0.0, 12500.0, 0.0, 14000.0, -800.0, 13250.0, 1.570796, "A-DIMS-EXT", "1500")
        add_dim_scaled(0.0, 0.0, 0.0, 4500.0, -1600.0, 2250.0, 1.570796, "A-DIMS-EXT", "4500")
        add_dim_scaled(0.0, 4500.0, 0.0, 12500.0, -1600.0, 8500.0, 1.570796, "A-DIMS-EXT", "8000")
        add_dim_scaled(0.0, 12500.0, 0.0, 14000.0, -1600.0, 13250.0, 1.570796, "A-DIMS-EXT", "1500")
        add_dim_scaled(0.0, 0.0, 0.0, 14000.0, -2400.0, 7000.0, 1.570796, "A-DIMS-EXT", "14000")

        # RIGHT (East)
        add_dim_scaled(6000.0, 0.0, 6000.0, 3000.0, 6800.0, 1500.0, 1.570796, "A-DIMS-EXT", "3000")
        add_dim_scaled(6000.0, 3000.0, 6000.0, 4500.0, 6800.0, 3750.0, 1.570796, "A-DIMS-EXT", "1500")
        add_dim_scaled(6000.0, 4500.0, 6000.0, 8000.0, 6800.0, 6250.0, 1.570796, "A-DIMS-EXT", "3500")
        add_dim_scaled(6000.0, 8000.0, 6000.0, 10000.0, 6800.0, 9000.0, 1.570796, "A-DIMS-EXT", "2000")
        add_dim_scaled(6000.0, 10000.0, 6000.0, 12500.0, 6800.0, 11250.0, 1.570796, "A-DIMS-EXT", "2500")
        add_dim_scaled(6000.0, 12500.0, 6000.0, 14000.0, 6800.0, 13250.0, 1.570796, "A-DIMS-EXT", "1500")
        add_dim_scaled(6000.0, 4500.0, 6000.0, 12500.0, 7600.0, 8500.0, 1.570796, "A-DIMS-EXT", "8000")
        add_dim_scaled(6000.0, 0.0, 6000.0, 14000.0, 8400.0, 7000.0, 1.570796, "A-DIMS-EXT", "14000")

        # INTERIOR DIMENSIONS
        add_dim_scaled(3000.0, 4500.0, 6000.0, 4500.0, 4500.0, 4750.0, 0.0, "A-DIMS-INT", "3000")
        add_dim_scaled(3000.0, 4500.0, 3000.0, 8000.0, 3250.0, 6250.0, 1.570796, "A-DIMS-INT", "3500")
        add_dim_scaled(4500.0, 8000.0, 6000.0, 8000.0, 5250.0, 8250.0, 0.0, "A-DIMS-INT", "1500")
        add_dim_scaled(4500.0, 8000.0, 4500.0, 10000.0, 4750.0, 9000.0, 1.570796, "A-DIMS-INT", "2000")
        add_dim_scaled(3000.0, 10000.0, 6000.0, 10000.0, 4500.0, 10250.0, 0.0, "A-DIMS-INT", "3000")
        add_dim_scaled(3000.0, 10000.0, 3000.0, 12500.0, 3250.0, 11250.0, 1.570796, "A-DIMS-INT", "2500")
        add_dim_scaled(0.0, 7500.0, 3000.0, 7500.0, 1500.0, 7500.0, 0.0, "A-DIMS-INT", "3000")

        # -------------------------------------------------------------
        # 11. THIN DASHED STRUCTURAL GRID & BUBBLES (A-GRID & A-ANNO-SYMB)
        # Lines STOP at bubble edges (no line piercing!)
        # -------------------------------------------------------------
        print("Drafting clean dashed structural grid lines (CENTER2, 0.09 mm) and bubbles...")
        grid_x = [("1", 0.0), ("2", 3000.0), ("3", 4500.0), ("4", 6000.0)]
        grid_y = [("A", 0.0), ("B", 3000.0), ("C", 4500.0), ("D", 8000.0), ("E", 10000.0), ("F", 12500.0), ("G", 14000.0)]

        y_bot_grid = -3200.0
        y_top_grid = 17200.0
        x_left_grid = -3200.0
        x_right_grid = 9200.0
        bubble_rad = 300.0

        for tag, gx in grid_x:
            # Vertical Grid line (CENTER2 linetype)
            add_line_to(ms, gx, y_bot_grid, gx, y_top_grid, "A-GRID")
            # Bottom Bubble
            add_circle_to(ms, gx, y_bot_grid - bubble_rad, bubble_rad, "A-ANNO-SYMB")
            add_centered_text_to(ms, tag, gx, y_bot_grid - bubble_rad, 220.0, "A-ANNO-SYMB")
            # Top Bubble
            add_circle_to(ms, gx, y_top_grid + bubble_rad, bubble_rad, "A-ANNO-SYMB")
            add_centered_text_to(ms, tag, gx, y_top_grid + bubble_rad, 220.0, "A-ANNO-SYMB")

        for tag, gy in grid_y:
            # Horizontal Grid line (CENTER2 linetype)
            add_line_to(ms, x_left_grid, gy, x_right_grid, gy, "A-GRID")
            # Left Bubble
            add_circle_to(ms, x_left_grid - bubble_rad, gy, bubble_rad, "A-ANNO-SYMB")
            add_centered_text_to(ms, tag, x_left_grid - bubble_rad, gy, 220.0, "A-ANNO-SYMB")
            # Right Bubble
            add_circle_to(ms, x_right_grid + bubble_rad, gy, bubble_rad, "A-ANNO-SYMB")
            add_centered_text_to(ms, tag, x_right_grid + bubble_rad, gy, 220.0, "A-ANNO-SYMB")

        # -------------------------------------------------------------
        # 12. TITLE BLOCK & NORTH ARROW
        # -------------------------------------------------------------
        print("Drafting architectural title block and North arrow...")
        title_cx = 3000.0
        title_y = -4400.0
        add_centered_text_to(ms, "DENAH RUMAH TINGGAL TYPE 50 / 84", title_cx, title_y, 350.0, "A-ANNO-TEXT")
        add_centered_text_to(ms, "SKALA 1 : 100   |   SATUAN : MILIMETER (mm)", title_cx, title_y - 450.0, 200.0, "A-ANNO-TEXT")
        add_centered_text_to(ms, "STANDAR ARSITEKTUR & AIA CAD LAYER STANDARDS", title_cx, title_y - 750.0, 160.0, "A-ANNO-TEXT")
        add_line_to(ms, 500.0, title_y - 950.0, 5500.0, title_y - 950.0, "A-ANNO-TEXT")
        add_line_to(ms, 500.0, title_y - 980.0, 5500.0, title_y - 980.0, "A-ANNO-TEXT")

        # North Arrow placed at top-right (9500, 16000)
        na_x = 9500.0
        na_y = 16000.0
        add_circle_to(ms, na_x, na_y, 350.0, "A-ANNO-SYMB")
        add_line_to(ms, na_x, na_y - 350.0, na_x, na_y + 350.0, "A-ANNO-SYMB")
        add_line_to(ms, na_x, na_y + 350.0, na_x - 100.0, na_y + 120.0, "A-ANNO-SYMB")
        add_line_to(ms, na_x, na_y + 350.0, na_x + 100.0, na_y + 120.0, "A-ANNO-SYMB")
        add_line_to(ms, na_x - 100.0, na_y + 120.0, na_x + 100.0, na_y + 120.0, "A-ANNO-SYMB")
        add_centered_text_to(ms, "U", na_x, na_y + 480.0, 220.0, "A-ANNO-SYMB")

        # -------------------------------------------------------------
        # 13. REGEN & ZOOM EXTENTS
        # -------------------------------------------------------------
        print("Regenerating drawing and fitting zoom extents...")
        try:
            doc.SendCommand("UCSICON Noorigin\n")
            doc.Regen(1)
            app.ZoomExtents()
        except Exception:
            pass

        print("=" * 75)
        print("PROFESSIONAL ARCHITECTURAL DRAFTING COMPLETE!")
        print(f"Total ModelSpace Entities: {ms.Count}")
        print(f"Total Block Definitions in Library: {doc.Blocks.Count}")
        print("=" * 75)

if __name__ == "__main__":
    import threading
    t = threading.Thread(target=run)
    t.start()
    t.join()
