"""
generate_mep_and_architectural_set.py
Comprehensive Architectural & MEP Working Drawing Generator for ZWCAD 2023.
Type 50 / 84 Residential House (6x14 m suburban lot).

Includes:
1. DENAH ARSITEKTUR (Type 50/84) with ANSI31 wall hatch, modular blocks, 1:100 dims, grid bubbles, section cuts
2. DENAH INSTALASI LISTRIK & TITIK LAMPU (Lighting, Switches, Outlets, MCB Box, Wiring, Legend)
3. DENAH INSTALASI AIR BERSIH (PDAM Meter, Toren 500L + Booster, PVC AW 3/4" & 1/2", Valves, Legend)
4. DENAH INSTALASI AIR KOTOR & AIR BEKAS (Black Water 4", Bio Septic Tank, Sumur Resapan, Grey Water 3", Bak Kontrol, Grease Trap, Legend)
5. TAMPAK DEPAN, TAMPAK BELAKANG, TAMPAK SAMPING KANAN (Minimalist modern facade, parapets, roof pitch, level markers)
6. POTONGAN A-A (Transverse) & POTONGAN B-B (Longitudinal) (Pondasi batu kali, sloof, dinding bata, ringbalk, plafond, kuda-kuda baja ringan)
"""

from __future__ import annotations

import math
import sys
from pathlib import Path

# Paths setup
for p in (Path(r"C:\AIGRAV"), Path(r"C:\ZWCAD-MCP")):
    if str(p) not in sys.path:
        sys.path.insert(0, str(p))

import cad_bridge


def run():
    print("=" * 80)
    print("GENERATING COMPREHENSIVE ARCHITECTURAL & MEP DRAWINGS IN ZWCAD 2023")
    print("=" * 80)

    with cad_bridge.cad_context() as (app, _):
        # 1. Target Drawing1.dwg explicitly
        target_doc = None
        for i in range(app.Documents.Count):
            d = app.Documents.Item(i)
            if "drawing1" in d.Name.lower():
                target_doc = d
                break
        if not target_doc:
            raise RuntimeError("Drawing1.dwg is not open in ZWCAD!")

        target_doc.Activate()
        doc = target_doc
        ms = doc.ModelSpace
        print(f"Connected to: {app.Caption}")
        print(f"Target Document: {doc.Name} (Initial count: {ms.Count})")

        # 2. Fast Clear Drawing1.dwg
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

        # 3. Load Linetypes and Set System Variables
        for lt_name in ["CENTER2", "DASHED", "HIDDEN"]:
            try:
                doc.Linetypes.Load(lt_name, "zwcad.lin")
            except Exception:
                pass

        try:
            doc.SetVariable("LWDISPLAY", 1)    # Turn on on-screen lineweights!
            doc.SetVariable("LTSCALE", 15.0)    # Crisp dashed lines (300mm repetition)
            doc.SetVariable("MEASUREMENT", 1)  # Metric
        except Exception:
            pass

        # 4. Standard AIA CAD Layers
        layers_def = [
            ("A-GRID", 1, "CENTER2", 9),             # Red, CENTER2, 0.09 mm (Thin dashed grid)
            ("S-COLS", 7, "Continuous", 35),         # White, 0.35 mm
            ("S-FNDN", 6, "Continuous", 35),         # Magenta, 0.35 mm (Pondasi & Sloof)
            ("S-ROOF", 3, "Continuous", 25),         # Green, 0.25 mm (Rangka atap)
            ("A-WALL-FULL", 4, "Continuous", 40),    # Cyan, 0.40 mm (Cut wall outline)
            ("A-WALL-PATS", 8, "Continuous", 9),     # Dark Gray, 0.09 mm (Brick ANSI31 Hatch)
            ("A-WALL-DIMMED", 8, "Continuous", 13),  # Dark Gray, 0.13 mm (MEP background wall)
            ("A-DOOR", 3, "Continuous", 25),         # Green, 0.25 mm
            ("A-GLAZ", 4, "Continuous", 20),         # Cyan, 0.20 mm
            ("A-FLOR", 9, "Continuous", 15),         # Light Gray, 0.15 mm
            ("A-PLBG", 5, "Continuous", 25),         # Blue, 0.25 mm
            ("A-FURN", 8, "Continuous", 15),         # Dark Gray, 0.15 mm
            ("A-ELEV", 7, "Continuous", 25),         # White, 0.25 mm
            ("A-ELEV-PATS", 8, "Continuous", 9),     # Dark Gray, 0.09 mm
            ("A-DIMS-EXT", 3, "Continuous", 18),     # Green, 0.18 mm
            ("A-DIMS-INT", 2, "Continuous", 18),     # Yellow, 0.18 mm
            ("A-ANNO-TEXT", 2, "Continuous", 25),    # Yellow, 0.25 mm
            ("A-ANNO-SYMB", 1, "Continuous", 25),    # Red, 0.25 mm
            # MEP Layers
            ("E-LITE", 2, "Continuous", 25),         # Yellow, 0.25 mm (MEP Lighting)
            ("E-POWR", 1, "Continuous", 25),         # Red, 0.25 mm (MEP Receptacles & MCB)
            ("E-CKTS", 3, "DASHED", 18),             # Green, DASHED, 0.18 mm (MEP Wiring loops)
            ("P-WATR-COLD", 4, "Continuous", 35),    # Cyan, 0.35 mm (Clean Water PVC AW 3/4")
            ("P-WATR-DIST", 5, "Continuous", 25),    # Blue, 0.25 mm (Clean Water PVC AW 1/2")
            ("P-SOIL", 1, "Continuous", 40),         # Red, 0.40 mm (Black Water PVC AW 4")
            ("P-WAST", 6, "Continuous", 35),         # Magenta, 0.35 mm (Grey Water PVC D 3")
            ("P-VENT", 3, "DASHED", 20),             # Green, DASHED, 0.20 mm (Vent Pipe 1.5")
            ("P-STOR", 5, "Continuous", 30),         # Blue, 0.30 mm (Rainwater / Got)
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

        print(f"Configured {len(layers_def)} AIA CAD layers.")

        # Primitives & Helpers
        def add_line(space, x1, y1, x2, y2, layer):
            p1 = cad_bridge.point3(x1, y1)
            p2 = cad_bridge.point3(x2, y2)
            ent = space.AddLine(p1, p2)
            ent.Layer = layer
            return ent

        def add_polyline(space, pts, closed, layer):
            flat = []
            for p in pts:
                flat.extend([float(p[0]), float(p[1])])
            import win32com.client, pythoncom
            arr = win32com.client.VARIANT(pythoncom.VT_ARRAY | pythoncom.VT_R8, flat)
            ent = space.AddLightWeightPolyline(arr)
            ent.Closed = bool(closed)
            ent.Layer = layer
            return ent

        def add_rect(space, x1, y1, x2, y2, layer):
            pts = [[x1, y1], [x2, y1], [x2, y2], [x1, y2]]
            return add_polyline(space, pts, True, layer)

        def add_circle(space, cx, cy, r, layer):
            c = cad_bridge.point3(cx, cy)
            ent = space.AddCircle(c, float(r))
            ent.Layer = layer
            return ent

        def add_arc(space, cx, cy, r, sa, ea, layer):
            c = cad_bridge.point3(cx, cy)
            ent = space.AddArc(c, float(r), float(sa), float(ea))
            ent.Layer = layer
            return ent

        def add_text(space, txt, cx, cy, h, layer, align=10, rot=0.0):
            pt = cad_bridge.point3(cx, cy)
            ent = space.AddText(str(txt), pt, float(h))
            ent.Alignment = int(align)
            ent.TextAlignmentPoint = pt
            ent.Rotation = float(rot)
            ent.Layer = layer
            return ent

        def add_hatch_to(space, boundary_pl, pat_name, pat_scale, layer, angle=0.0):
            try:
                import win32com.client, pythoncom
                hatch = space.AddHatch(0, pat_name, False)
                hatch.Layer = layer
                hatch.PatternScale = float(pat_scale)
                if angle != 0.0:
                    hatch.PatternAngle = float(angle)
                loop = win32com.client.VARIANT(pythoncom.VT_ARRAY | pythoncom.VT_DISPATCH, [boundary_pl])
                hatch.AppendOuterLoop(loop)
                hatch.Evaluate()
                return hatch
            except Exception:
                return None

        def add_dim(x1, y1, x2, y2, dx, dy, rot, layer, override=""):
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

        def add_title_block(cx, cy, title, scale="1 : 100", no="01"):
            # Title bubble & text block
            add_circle(ms, cx - 1800.0, cy, 320.0, "A-ANNO-SYMB")
            add_text(ms, no, cx - 1800.0, cy, 280.0, "A-ANNO-SYMB", align=10)
            add_text(ms, title, cx + 200.0, cy + 120.0, 320.0, "A-ANNO-TEXT", align=10)
            add_text(ms, f"SKALA  {scale}", cx + 200.0, cy - 180.0, 180.0, "A-ANNO-TEXT", align=10)
            add_line(ms, cx - 2200.0, cy - 350.0, cx + 2600.0, cy - 350.0, "A-ANNO-TEXT")
            add_line(ms, cx - 2200.0, cy - 380.0, cx + 2600.0, cy - 380.0, "A-ANNO-TEXT")

        # -------------------------------------------------------------
        # 5. DEFINE ARCHITECTURAL & MEP BLOCKS LIBRARY
        # -------------------------------------------------------------
        print("Defining comprehensive block library...")

        # A. Columns (150x150 mm)
        blk_col = get_or_create_block("BLK_KOLOM_15X15")
        cp = add_rect(blk_col, -75.0, -75.0, 75.0, 75.0, "S-COLS")
        add_hatch_to(blk_col, cp, "SOLID", 1.0, "S-COLS")

        # B. Doors
        b_d1 = get_or_create_block("BLK_DOOR_D1_HORIZ_900")
        add_rect(b_d1, -25.0, -50.0, 25.0, 50.0, "A-DOOR")
        add_rect(b_d1, 875.0, -50.0, 925.0, 50.0, "A-DOOR")
        add_rect(b_d1, 0.0, 0.0, 35.0, 900.0, "A-DOOR")
        add_arc(b_d1, 0.0, 0.0, 900.0, 0.0, math.pi / 2.0, "A-DOOR")
        add_text(b_d1, "D1", 450.0, 200.0, 150.0, "A-DOOR")

        b_d2 = get_or_create_block("BLK_DOOR_D2_VERT_800")
        add_rect(b_d2, -50.0, -25.0, 50.0, 25.0, "A-DOOR")
        add_rect(b_d2, -50.0, 775.0, 50.0, 825.0, "A-DOOR")
        add_rect(b_d2, 0.0, 0.0, 800.0, 35.0, "A-DOOR")
        add_arc(b_d2, 0.0, 0.0, 800.0, 0.0, math.pi / 2.0, "A-DOOR")
        add_text(b_d2, "D2", 200.0, 400.0, 150.0, "A-DOOR")

        b_d3 = get_or_create_block("BLK_DOOR_D3_VERT_700")
        add_rect(b_d3, -50.0, -25.0, 50.0, 25.0, "A-DOOR")
        add_rect(b_d3, -50.0, 675.0, 50.0, 725.0, "A-DOOR")
        add_rect(b_d3, 0.0, 0.0, 700.0, 35.0, "A-DOOR")
        add_arc(b_d3, 0.0, 0.0, 700.0, 0.0, math.pi / 2.0, "A-DOOR")
        add_text(b_d3, "D3", 200.0, 350.0, 150.0, "A-DOOR")

        b_d4 = get_or_create_block("BLK_DOOR_D4_HORIZ_800")
        add_rect(b_d4, -25.0, -50.0, 25.0, 50.0, "A-DOOR")
        add_rect(b_d4, 775.0, -50.0, 825.0, 50.0, "A-DOOR")
        add_rect(b_d4, 0.0, 0.0, 35.0, 800.0, "A-DOOR")
        add_arc(b_d4, 0.0, 0.0, 800.0, 0.0, math.pi / 2.0, "A-DOOR")
        add_text(b_d4, "D4", 400.0, 200.0, 150.0, "A-DOOR")

        # C. Windows
        def define_window(b_name, w, tag, leaves=2):
            b = get_or_create_block(b_name)
            w_h = w / 2.0
            add_rect(b, -w_h, -50.0, -w_h + 50.0, 50.0, "A-GLAZ")
            add_rect(b, w_h - 50.0, -50.0, w_h, 50.0, "A-GLAZ")
            add_line(b, -w_h, -50.0, w_h, -50.0, "A-GLAZ")
            add_line(b, -w_h, 50.0, w_h, 50.0, "A-GLAZ")
            add_line(b, -w_h + 50.0, -10.0, w_h - 50.0, -10.0, "A-GLAZ")
            add_line(b, -w_h + 50.0, 10.0, w_h - 50.0, 10.0, "A-GLAZ")
            if leaves == 2:
                add_rect(b, -25.0, -50.0, 25.0, 50.0, "A-GLAZ")
            add_text(b, tag, 0.0, 180.0, 150.0, "A-GLAZ")

        define_window("BLK_WIN_W1_1400", 1400.0, "W1", 2)
        define_window("BLK_WIN_W2_1200", 1200.0, "W2", 2)
        define_window("BLK_WIN_W3_1200", 1200.0, "W3", 2)
        define_window("BLK_WIN_W4_900", 900.0, "W4", 1)

        # D. Sanitary Fixtures
        blk_closet = get_or_create_block("BLK_SAN_CLOSET")
        add_rect(blk_closet, -250.0, -200.0, 250.0, 0.0, "A-PLBG")
        add_rect(blk_closet, -200.0, 0.0, 200.0, 420.0, "A-PLBG")
        add_circle(blk_closet, 0.0, 220.0, 120.0, "A-PLBG")
        add_circle(blk_closet, 0.0, -100.0, 25.0, "A-PLBG")

        blk_shower = get_or_create_block("BLK_SAN_SHOWER")
        add_line(blk_shower, 0.0, 0.0, 900.0, 0.0, "A-PLBG")
        add_line(blk_shower, 0.0, 0.0, 0.0, 900.0, "A-PLBG")
        add_circle(blk_shower, 450.0, 450.0, 120.0, "A-PLBG")
        add_line(blk_shower, 450.0, 450.0, 900.0, 450.0, "A-PLBG")

        blk_fd = get_or_create_block("BLK_SAN_DRAIN")
        add_rect(blk_fd, -100.0, -100.0, 100.0, 100.0, "A-PLBG")
        add_circle(blk_fd, 0.0, 0.0, 75.0, "A-PLBG")
        add_text(blk_fd, "FD", 0.0, 180.0, 100.0, "A-PLBG")

        blk_sink = get_or_create_block("BLK_SAN_SINK")
        add_rect(blk_sink, -400.0, -225.0, 400.0, 225.0, "A-PLBG")
        add_rect(blk_sink, -360.0, -180.0, -40.0, 180.0, "A-PLBG")
        add_rect(blk_sink, 40.0, -180.0, 360.0, 180.0, "A-PLBG")
        add_circle(blk_sink, 0.0, 190.0, 25.0, "A-PLBG")

        # E. Furniture Blocks
        blk_mbed = get_or_create_block("BLK_FURN_MASTER_BED")
        add_rect(blk_mbed, 0.0, 0.0, 1600.0, 2000.0, "A-FURN")
        add_rect(blk_mbed, -50.0, 1950.0, 1650.0, 2050.0, "A-FURN")
        add_rect(blk_mbed, 100.0, 1550.0, 650.0, 1900.0, "A-FURN")
        add_rect(blk_mbed, 950.0, 1550.0, 1500.0, 1900.0, "A-FURN")
        add_line(blk_mbed, 0.0, 600.0, 1600.0, 600.0, "A-FURN")
        add_rect(blk_mbed, -500.0, 1550.0, -50.0, 2000.0, "A-FURN")
        add_rect(blk_mbed, 1650.0, 1550.0, 2100.0, 2000.0, "A-FURN")

        blk_sbed = get_or_create_block("BLK_FURN_SINGLE_BED")
        add_rect(blk_sbed, 0.0, 0.0, 1200.0, 2000.0, "A-FURN")
        add_rect(blk_sbed, -30.0, 1950.0, 1230.0, 2030.0, "A-FURN")
        add_rect(blk_sbed, 200.0, 1550.0, 1000.0, 1900.0, "A-FURN")
        add_line(blk_sbed, 0.0, 600.0, 1200.0, 600.0, "A-FURN")

        blk_ward = get_or_create_block("BLK_FURN_WARDROBE")
        add_rect(blk_ward, 0.0, 0.0, 1400.0, 600.0, "A-FURN")
        add_line(blk_ward, 0.0, 0.0, 1400.0, 600.0, "A-FURN")
        add_line(blk_ward, 0.0, 600.0, 1400.0, 0.0, "A-FURN")

        blk_study = get_or_create_block("BLK_FURN_STUDY_SET")
        add_rect(blk_study, 0.0, 0.0, 1000.0, 500.0, "A-FURN")
        add_circle(blk_study, 500.0, -220.0, 180.0, "A-FURN")

        blk_living = get_or_create_block("BLK_FURN_LIVING_SET")
        add_rect(blk_living, 0.0, 0.0, 850.0, 2000.0, "A-FURN")
        for i in range(3):
            add_rect(blk_living, 150.0, i * 650.0 + 50.0, 800.0, (i + 1) * 650.0 - 50.0, "A-FURN")
        add_rect(blk_living, 1100.0, 550.0, 1600.0, 1450.0, "A-FURN")
        add_rect(blk_living, 2200.0, 300.0, 2550.0, 1700.0, "A-FURN")
        add_text(blk_living, "TV", 2375.0, 1000.0, 120.0, "A-FURN")

        blk_dining = get_or_create_block("BLK_FURN_DINING_SET")
        add_rect(blk_dining, 0.0, 0.0, 1200.0, 800.0, "A-FURN")
        add_rect(blk_dining, 150.0, -350.0, 500.0, -50.0, "A-FURN")
        add_rect(blk_dining, 700.0, -350.0, 1050.0, -50.0, "A-FURN")
        add_rect(blk_dining, 150.0, 850.0, 500.0, 1150.0, "A-FURN")
        add_rect(blk_dining, 700.0, 850.0, 1050.0, 1150.0, "A-FURN")

        blk_counter = get_or_create_block("BLK_FURN_KITCHEN_COUNTER")
        add_rect(blk_counter, 0.0, 0.0, 2850.0, 550.0, "A-FURN")
        add_rect(blk_counter, 575.0, 75.0, 1275.0, 495.0, "A-FURN")
        add_circle(blk_counter, 775.0, 285.0, 110.0, "A-FURN")
        add_circle(blk_counter, 1075.0, 285.0, 110.0, "A-FURN")

        blk_car = get_or_create_block("BLK_FURN_CAR")
        add_rect(blk_car, 0.0, 0.0, 1700.0, 4000.0, "A-FURN")
        add_line(blk_car, 100.0, 1100.0, 1600.0, 1100.0, "A-FURN")
        add_line(blk_car, 150.0, 2800.0, 1550.0, 2800.0, "A-FURN")
        add_line(blk_car, 100.0, 1100.0, 150.0, 2800.0, "A-FURN")
        add_line(blk_car, 1600.0, 1100.0, 1550.0, 2800.0, "A-FURN")
        add_text(blk_car, "CARPORT (1 MOBIL)", 850.0, 1950.0, 160.0, "A-FURN")

        # F. MEP Electrical Blocks
        blk_dl = get_or_create_block("BLK_ELEC_DOWNLIGHT")
        add_circle(blk_dl, 0.0, 0.0, 150.0, "E-LITE")
        add_line(blk_dl, -150.0, 0.0, 150.0, 0.0, "E-LITE")
        add_line(blk_dl, 0.0, -150.0, 0.0, 150.0, "E-LITE")

        blk_tl = get_or_create_block("BLK_ELEC_TL_LAMP")
        add_rect(blk_tl, -400.0, -80.0, 400.0, 80.0, "E-LITE")
        add_line(blk_tl, -400.0, -80.0, 400.0, 80.0, "E-LITE")
        add_line(blk_tl, -400.0, 80.0, 400.0, -80.0, "E-LITE")

        blk_wlamp = get_or_create_block("BLK_ELEC_WALL_LAMP")
        add_arc(blk_wlamp, 0.0, 0.0, 140.0, 0.0, math.pi, "E-LITE")
        add_line(blk_wlamp, -140.0, 0.0, 140.0, 0.0, "E-LITE")
        add_line(blk_wlamp, 0.0, 0.0, 0.0, 140.0, "E-LITE")

        blk_sw1 = get_or_create_block("BLK_ELEC_SW_SINGLE")
        add_circle(blk_sw1, 0.0, 0.0, 80.0, "E-POWR")
        add_line(blk_sw1, 0.0, 0.0, 120.0, 120.0, "E-POWR")
        add_circle(blk_sw1, 120.0, 120.0, 25.0, "E-POWR")

        blk_sw2 = get_or_create_block("BLK_ELEC_SW_DOUBLE")
        add_circle(blk_sw2, 0.0, 0.0, 80.0, "E-POWR")
        add_line(blk_sw2, 0.0, 0.0, 120.0, 120.0, "E-POWR")
        add_line(blk_sw2, 0.0, 0.0, -120.0, 120.0, "E-POWR")
        add_circle(blk_sw2, 120.0, 120.0, 25.0, "E-POWR")
        add_circle(blk_sw2, -120.0, 120.0, 25.0, "E-POWR")

        blk_sk = get_or_create_block("BLK_ELEC_OUTLET")
        add_arc(blk_sk, 0.0, 0.0, 120.0, 0.0, math.pi, "E-POWR")
        add_line(blk_sk, -120.0, 0.0, 120.0, 0.0, "E-POWR")
        add_line(blk_sk, -40.0, 0.0, -40.0, 80.0, "E-POWR")
        add_line(blk_sk, 40.0, 0.0, 40.0, 80.0, "E-POWR")

        blk_sk_ac = get_or_create_block("BLK_ELEC_OUTLET_AC")
        add_arc(blk_sk_ac, 0.0, 0.0, 120.0, 0.0, math.pi, "E-POWR")
        add_line(blk_sk_ac, -120.0, 0.0, 120.0, 0.0, "E-POWR")
        add_line(blk_sk_ac, 0.0, 0.0, 0.0, 80.0, "E-POWR")
        add_text(blk_sk_ac, "AC", 0.0, 140.0, 90.0, "E-POWR")

        blk_mcb = get_or_create_block("BLK_ELEC_MCB_BOX")
        add_rect(blk_mcb, -200.0, -120.0, 200.0, 120.0, "E-POWR")
        add_line(blk_mcb, -200.0, -120.0, 200.0, 120.0, "E-POWR")
        add_line(blk_mcb, -200.0, 120.0, 200.0, -120.0, "E-POWR")
        add_text(blk_mcb, "MCB", 0.0, 180.0, 120.0, "E-POWR")

        # G. MEP Plumbing Blocks
        blk_pmeter = get_or_create_block("BLK_PLBG_METER")
        add_rect(blk_pmeter, -200.0, -120.0, 200.0, 120.0, "P-WATR-COLD")
        add_circle(blk_pmeter, 0.0, 0.0, 80.0, "P-WATR-COLD")
        add_text(blk_pmeter, "M", 0.0, 0.0, 100.0, "P-WATR-COLD")

        blk_toren = get_or_create_block("BLK_PLBG_TOREN")
        add_circle(blk_toren, 0.0, 0.0, 400.0, "P-WATR-COLD")
        add_circle(blk_toren, 0.0, 0.0, 360.0, "P-WATR-COLD")
        add_text(blk_toren, "TOREN 500L", 0.0, 0.0, 110.0, "P-WATR-COLD")

        blk_valve = get_or_create_block("BLK_PLBG_VALVE")
        add_polyline(blk_valve, [[-100.0, -60.0], [100.0, 60.0], [100.0, -60.0], [-100.0, 60.0]], True, "P-WATR-COLD")
        add_line(blk_valve, 0.0, 0.0, 0.0, 90.0, "P-WATR-COLD")
        add_line(blk_valve, -50.0, 90.0, 50.0, 90.0, "P-WATR-COLD")

        blk_septic = get_or_create_block("BLK_PLBG_SEPTIC_TANK")
        add_rect(blk_septic, -900.0, -500.0, 900.0, 500.0, "P-SOIL")
        add_line(blk_septic, 0.0, -500.0, 0.0, 500.0, "P-SOIL")
        add_circle(blk_septic, -450.0, 0.0, 200.0, "P-SOIL")
        add_circle(blk_septic, 450.0, 0.0, 200.0, "P-SOIL")
        add_text(blk_septic, "BIO-SEPTIC TANK", 0.0, 600.0, 150.0, "P-SOIL")

        blk_resapan = get_or_create_block("BLK_PLBG_RESAPAN")
        add_circle(blk_resapan, 0.0, 0.0, 400.0, "P-SOIL")
        add_circle(blk_resapan, 0.0, 0.0, 350.0, "P-SOIL")
        add_text(blk_resapan, "SUMUR RESAPAN", 0.0, 0.0, 110.0, "P-SOIL")

        blk_bk = get_or_create_block("BLK_PLBG_BAK_KONTROL")
        add_rect(blk_bk, -175.0, -175.0, 175.0, 175.0, "P-WAST")
        add_line(blk_bk, -175.0, -175.0, 175.0, 175.0, "P-WAST")
        add_line(blk_bk, -175.0, 175.0, 175.0, -175.0, "P-WAST")
        add_text(blk_bk, "BK", 0.0, 240.0, 120.0, "P-WAST")

        blk_gtrap = get_or_create_block("BLK_PLBG_GREASE_TRAP")
        add_rect(blk_gtrap, -200.0, -125.0, 200.0, 125.0, "P-WAST")
        add_line(blk_gtrap, -70.0, -125.0, -70.0, 125.0, "P-WAST")
        add_line(blk_gtrap, 70.0, -125.0, 70.0, 125.0, "P-WAST")
        add_text(blk_gtrap, "GREASE TRAP", 0.0, 180.0, 100.0, "P-WAST")

        print("Block definitions complete.")

        # Shared Wall Layout Definitions
        col_coords = [
            (0.0, 0.0), (3000.0, 0.0), (6000.0, 0.0),
            (6000.0, 3000.0),
            (0.0, 4500.0), (3000.0, 4500.0), (6000.0, 4500.0),
            (0.0, 8000.0), (3000.0, 8000.0), (4500.0, 8000.0), (6000.0, 8000.0),
            (0.0, 10500.0), (3000.0, 10000.0), (4500.0, 10000.0), (6000.0, 10000.0),
            (0.0, 12500.0), (3000.0, 12500.0), (6000.0, 12500.0),
            (0.0, 14000.0), (3000.0, 14000.0), (6000.0, 14000.0),
        ]

        wall_segments = [
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

        def draw_dimmed_plan_base(ox, oy):
            """Draw lightweight background plan for MEP drawings."""
            w_h = 75.0
            for cx, cy in col_coords:
                insert_block("BLK_KOLOM_15X15", ox + cx, oy + cy, layer="A-WALL-DIMMED")

            for x1, y1, x2, y2 in wall_segments:
                dx = x2 - x1
                dy = y2 - y1
                L = math.hypot(dx, dy)
                if L < 5.0:
                    continue
                ux = dx / L
                uy = dy / L
                nx = -uy * w_h
                ny = ux * w_h
                pts = [
                    [ox + x1 + nx, oy + y1 + ny],
                    [ox + x2 + nx, oy + y2 + ny],
                    [ox + x2 - nx, oy + y2 - ny],
                    [ox + x1 - nx, oy + y1 - ny]
                ]
                add_polyline(ms, pts, True, "A-WALL-DIMMED")

            # Dimmed doors & windows
            insert_block("BLK_DOOR_D1_HORIZ_900", ox + 1900.0, oy + 4500.0, layer="A-WALL-DIMMED")
            insert_block("BLK_DOOR_D2_VERT_800", ox + 3000.0, oy + 7100.0, layer="A-WALL-DIMMED")
            insert_block("BLK_DOOR_D2_VERT_800", ox + 3000.0, oy + 10100.0, layer="A-WALL-DIMMED")
            insert_block("BLK_DOOR_D3_VERT_700", ox + 4500.0, oy + 8200.0, layer="A-WALL-DIMMED")
            insert_block("BLK_DOOR_D4_HORIZ_800", ox + 500.0, oy + 12500.0, layer="A-WALL-DIMMED")

            insert_block("BLK_WIN_W1_1400", ox + 4500.0, oy + 4500.0, layer="A-WALL-DIMMED")
            insert_block("BLK_WIN_W2_1200", ox + 4500.0, oy + 12500.0, layer="A-WALL-DIMMED")
            insert_block("BLK_WIN_W3_1200", ox + 1000.0, oy + 4500.0, layer="A-WALL-DIMMED")
            insert_block("BLK_WIN_W4_900", ox + 2150.0, oy + 12500.0, layer="A-WALL-DIMMED")

            # Sanitary background
            insert_block("BLK_SAN_CLOSET", ox + 5400.0, oy + 8300.0, layer="A-WALL-DIMMED")
            insert_block("BLK_SAN_SHOWER", ox + 5100.0, oy + 9100.0, layer="A-WALL-DIMMED")
            insert_block("BLK_SAN_DRAIN", ox + 4800.0, oy + 9600.0, layer="A-WALL-DIMMED")
            insert_block("BLK_SAN_SINK", ox + 2150.0, oy + 12200.0, layer="A-WALL-DIMMED")

            # Room labels (small)
            lbls = [
                ("CARPORT", 1500.0, 2200.0),
                ("TERAS", 3200.0, 3750.0),
                ("R. TAMU", 1500.0, 6200.0),
                ("R. MAKAN", 1500.0, 8500.0),
                ("DAPUR", 1800.0, 11250.0),
                ("K. TIDUR 1", 4500.0, 6250.0),
                ("KM/WC", 5000.0, 8900.0),
                ("K. TIDUR 2", 4500.0, 11250.0),
                ("SERVICE", 3000.0, 13300.0),
            ]
            for txt, lx, ly in lbls:
                add_text(ms, txt, ox + lx, oy + ly, 140.0, "A-ANNO-TEXT")

            # Grid lines (thin dashed)
            add_line(ms, ox + 0.0, oy - 1200.0, ox + 0.0, oy + 15200.0, "A-GRID")
            add_line(ms, ox + 3000.0, oy - 1200.0, ox + 3000.0, oy + 15200.0, "A-GRID")
            add_line(ms, ox + 6000.0, oy - 1200.0, ox + 6000.0, oy + 15200.0, "A-GRID")
            add_line(ms, ox - 1200.0, oy + 0.0, ox + 7200.0, oy + 0.0, "A-GRID")
            add_line(ms, ox - 1200.0, oy + 4500.0, ox + 7200.0, oy + 4500.0, "A-GRID")
            add_line(ms, ox - 1200.0, oy + 8000.0, ox + 7200.0, oy + 8000.0, "A-GRID")
            add_line(ms, ox - 1200.0, oy + 12500.0, ox + 7200.0, oy + 12500.0, "A-GRID")
            add_line(ms, ox - 1200.0, oy + 14000.0, ox + 7200.0, oy + 14000.0, "A-GRID")

            # Outer boundary dimensions
            add_dim(ox + 0.0, oy + 0.0, ox + 6000.0, oy + 0.0, ox + 3000.0, oy - 800.0, 0.0, "A-DIMS-EXT", "6000")
            add_dim(ox + 0.0, oy + 0.0, ox + 0.0, oy + 14000.0, ox - 800.0, oy + 7000.0, 1.570796, "A-DIMS-EXT", "14000")

        # =============================================================
        # DRAWING 1: DENAH ARSITEKTUR (Origin X=0, Y=0)
        # =============================================================
        print("Drafting Drawing 1: Denah Arsitektur...")
        # 1. Columns
        for cx, cy in col_coords:
            insert_block("BLK_KOLOM_15X15", cx, cy, layer="S-COLS")

        # 2. Doors & Windows
        insert_block("BLK_DOOR_D1_HORIZ_900", 1900.0, 4500.0, layer="A-DOOR")
        insert_block("BLK_DOOR_D2_VERT_800", 3000.0, 7100.0, layer="A-DOOR")
        insert_block("BLK_DOOR_D2_VERT_800", 3000.0, 10100.0, layer="A-DOOR")
        insert_block("BLK_DOOR_D3_VERT_700", 4500.0, 8200.0, layer="A-DOOR")
        insert_block("BLK_DOOR_D4_HORIZ_800", 500.0, 12500.0, layer="A-DOOR")

        insert_block("BLK_WIN_W1_1400", 4500.0, 4500.0, layer="A-GLAZ")
        insert_block("BLK_WIN_W2_1200", 4500.0, 12500.0, layer="A-GLAZ")
        insert_block("BLK_WIN_W3_1200", 1000.0, 4500.0, layer="A-GLAZ")
        insert_block("BLK_WIN_W4_900", 2150.0, 12500.0, layer="A-GLAZ")

        # 3. Sanitary
        insert_block("BLK_SAN_CLOSET", 5400.0, 8300.0, layer="A-PLBG")
        insert_block("BLK_SAN_SHOWER", 5100.0, 9100.0, layer="A-PLBG")
        insert_block("BLK_SAN_DRAIN", 4800.0, 9600.0, layer="A-PLBG")
        insert_block("BLK_SAN_SINK", 2150.0, 12200.0, layer="A-PLBG")

        # 4. Furniture
        insert_block("BLK_FURN_MASTER_BED", 3950.0, 5600.0, layer="A-FURN")
        insert_block("BLK_FURN_WARDROBE", 4400.0, 4600.0, layer="A-FURN")
        insert_block("BLK_FURN_SINGLE_BED", 4600.0, 10350.0, layer="A-FURN")
        insert_block("BLK_FURN_STUDY_SET", 3200.0, 11900.0, layer="A-FURN")
        insert_block("BLK_FURN_WARDROBE", 3200.0, 10400.0, sx=0.85, layer="A-FURN")
        insert_block("BLK_FURN_LIVING_SET", 350.0, 5200.0, layer="A-FURN")
        insert_block("BLK_FURN_DINING_SET", 900.0, 8400.0, layer="A-FURN")
        insert_block("BLK_FURN_KITCHEN_COUNTER", 75.0, 11875.0, layer="A-FURN")
        insert_block("BLK_FURN_CAR", 650.0, 300.0, layer="A-FURN")

        # 5. Cut Walls (150 mm) & ANSI31 Brick Hatch (scale 12.0)
        w_h = 75.0
        for x1, y1, x2, y2 in wall_segments:
            dx = x2 - x1
            dy = y2 - y1
            L = math.hypot(dx, dy)
            if L < 5.0:
                continue
            ux = dx / L
            uy = dy / L
            nx = -uy * w_h
            ny = ux * w_h
            pts = [
                [x1 + nx, y1 + ny],
                [x2 + nx, y2 + ny],
                [x2 - nx, y2 - ny],
                [x1 - nx, y1 - ny]
            ]
            w_pl = add_polyline(ms, pts, True, "A-WALL-FULL")
            add_hatch_to(ms, w_pl, "ANSI31", 12.0, "A-WALL-PATS")

        # 6. Floor Textures
        km_pl = add_rect(ms, 4575.0, 8075.0, 5925.0, 9925.0, "A-FLOR")
        add_hatch_to(ms, km_pl, "NET", 250.0, "A-FLOR")

        teras_pl = add_rect(ms, 1900.0, 3000.0, 4500.0, 4500.0, "A-FLOR")
        add_hatch_to(ms, teras_pl, "NET", 400.0, "A-FLOR")

        car_pl = add_rect(ms, 0.0, 0.0, 3000.0, 4500.0, "A-FLOR")
        add_hatch_to(ms, car_pl, "AR-B816", 20.0, "A-FLOR")

        # 7. Room Annotations
        rooms = [
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
        for name, area, ffl, cx, cy in rooms:
            add_text(ms, name, cx, cy + 120.0, 200.0, "A-ANNO-TEXT")
            add_text(ms, f"Luas: {area} | FFL: {ffl}", cx, cy - 100.0, 130.0, "A-ANNO-TEXT")

        # 8. Hierarchical Dimensions
        # Bottom
        add_dim(0.0, 0.0, 3000.0, 0.0, 1500.0, -800.0, 0.0, "A-DIMS-EXT", "3000")
        add_dim(3000.0, 0.0, 6000.0, 0.0, 4500.0, -800.0, 0.0, "A-DIMS-EXT", "3000")
        add_dim(0.0, 0.0, 6000.0, 0.0, 3000.0, -1800.0, 0.0, "A-DIMS-EXT", "6000")
        # Top
        add_dim(0.0, 14000.0, 3000.0, 14000.0, 1500.0, 14800.0, 0.0, "A-DIMS-EXT", "3000")
        add_dim(3000.0, 14000.0, 6000.0, 14000.0, 4500.0, 14800.0, 0.0, "A-DIMS-EXT", "3000")
        add_dim(0.0, 14000.0, 6000.0, 14000.0, 3000.0, 15800.0, 0.0, "A-DIMS-EXT", "6000")
        # Left
        add_dim(0.0, 0.0, 0.0, 4500.0, -800.0, 2250.0, 1.570796, "A-DIMS-EXT", "4500")
        add_dim(0.0, 4500.0, 0.0, 8000.0, -800.0, 6250.0, 1.570796, "A-DIMS-EXT", "3500")
        add_dim(0.0, 8000.0, 0.0, 12500.0, -800.0, 10250.0, 1.570796, "A-DIMS-EXT", "4500")
        add_dim(0.0, 12500.0, 0.0, 14000.0, -800.0, 13250.0, 1.570796, "A-DIMS-EXT", "1500")
        add_dim(0.0, 0.0, 0.0, 14000.0, -1800.0, 7000.0, 1.570796, "A-DIMS-EXT", "14000")
        # Right
        add_dim(6000.0, 0.0, 6000.0, 4500.0, 6800.0, 2250.0, 1.570796, "A-DIMS-EXT", "4500")
        add_dim(6000.0, 4500.0, 6000.0, 8000.0, 6800.0, 6250.0, 1.570796, "A-DIMS-EXT", "3500")
        add_dim(6000.0, 8000.0, 6000.0, 10000.0, 6800.0, 9000.0, 1.570796, "A-DIMS-EXT", "2000")
        add_dim(6000.0, 10000.0, 6000.0, 12500.0, 6800.0, 11250.0, 1.570796, "A-DIMS-EXT", "2500")
        add_dim(6000.0, 12500.0, 6000.0, 14000.0, 6800.0, 13250.0, 1.570796, "A-DIMS-EXT", "1500")
        add_dim(6000.0, 0.0, 6000.0, 14000.0, 7800.0, 7000.0, 1.570796, "A-DIMS-EXT", "14000")

        # 9. As Grid & Modular Bubbles
        grid_x = [("1", 0.0), ("2", 3000.0), ("3", 4500.0), ("4", 6000.0)]
        grid_y = [("A", 0.0), ("B", 3000.0), ("C", 4500.0), ("D", 8000.0), ("E", 10000.0), ("F", 12500.0), ("G", 14000.0)]
        y_bg = -2400.0
        y_tg = 16400.0
        x_lg = -2400.0
        x_rg = 8400.0
        b_r = 280.0

        for tag, gx in grid_x:
            add_line(ms, gx, y_bg, gx, y_tg, "A-GRID")
            # Bottom bubble
            add_circle(ms, gx, y_bg - b_r, b_r, "A-ANNO-SYMB")
            add_text(ms, tag, gx, y_bg - b_r, 220.0, "A-ANNO-SYMB")
            # Top bubble
            add_circle(ms, gx, y_tg + b_r, b_r, "A-ANNO-SYMB")
            add_text(ms, tag, gx, y_tg + b_r, 220.0, "A-ANNO-SYMB")

        for tag, gy in grid_y:
            add_line(ms, x_lg, gy, x_rg, gy, "A-GRID")
            # Left bubble
            add_circle(ms, x_lg - b_r, gy, b_r, "A-ANNO-SYMB")
            add_text(ms, tag, x_lg - b_r, gy, 220.0, "A-ANNO-SYMB")
            # Right bubble
            add_circle(ms, x_rg + b_r, gy, b_r, "A-ANNO-SYMB")
            add_text(ms, tag, x_rg + b_r, gy, 220.0, "A-ANNO-SYMB")

        # Section Cut Line Markers on Denah
        # Potongan A-A (cut at Y=6200 across living & master bed)
        add_line(ms, -1200.0, 6200.0, 7200.0, 6200.0, "A-ANNO-SYMB")
        add_circle(ms, -1500.0, 6200.0, 250.0, "A-ANNO-SYMB")
        add_text(ms, "A", -1500.0, 6200.0, 220.0, "A-ANNO-SYMB")
        add_line(ms, -1500.0, 6450.0, -1500.0, 6800.0, "A-ANNO-SYMB")
        add_line(ms, -1500.0, 6800.0, -1400.0, 6650.0, "A-ANNO-SYMB")

        add_circle(ms, 7500.0, 6200.0, 250.0, "A-ANNO-SYMB")
        add_text(ms, "A", 7500.0, 6200.0, 220.0, "A-ANNO-SYMB")
        add_line(ms, 7500.0, 6450.0, 7500.0, 6800.0, "A-ANNO-SYMB")
        add_line(ms, 7500.0, 6800.0, 7400.0, 6650.0, "A-ANNO-SYMB")

        # Potongan B-B (cut at X=1500 along length)
        add_line(ms, 1500.0, -1200.0, 1500.0, 15200.0, "A-ANNO-SYMB")
        add_circle(ms, 1500.0, -1500.0, 250.0, "A-ANNO-SYMB")
        add_text(ms, "B", 1500.0, -1500.0, 220.0, "A-ANNO-SYMB")
        add_circle(ms, 1500.0, 15500.0, 250.0, "A-ANNO-SYMB")
        add_text(ms, "B", 1500.0, 15500.0, 220.0, "A-ANNO-SYMB")

        # North Arrow & Title Block
        add_circle(ms, 8500.0, 15000.0, 350.0, "A-ANNO-SYMB")
        add_line(ms, 8500.0, 14650.0, 8500.0, 15350.0, "A-ANNO-SYMB")
        add_line(ms, 8500.0, 15350.0, 8400.0, 15120.0, "A-ANNO-SYMB")
        add_line(ms, 8500.0, 15350.0, 8600.0, 15120.0, "A-ANNO-SYMB")
        add_text(ms, "U", 8500.0, 15480.0, 200.0, "A-ANNO-SYMB")

        add_title_block(3000.0, -3600.0, "DENAH ARSITEKTUR TYPE 50 / 84", "1 : 100", "01")

        # =============================================================
        # DRAWING 2: DENAH INSTALASI LISTRIK & TITIK LAMPU (Origin X=12000, Y=0)
        # =============================================================
        print("Drafting Drawing 2: Instalasi Listrik & Titik Lampu...")
        ox_e = 12000.0
        oy_e = 0.0
        draw_dimmed_plan_base(ox_e, oy_e)

        # 1. Lighting Fixtures
        # Carport outbow lamp
        insert_block("BLK_ELEC_WALL_LAMP", ox_e + 3000.0, oy_e + 2250.0, layer="E-LITE")
        # Teras Depan downlight 9W
        insert_block("BLK_ELEC_DOWNLIGHT", ox_e + 3200.0, oy_e + 3750.0, layer="E-LITE")
        # R. Tamu LED Downlight 12W (2 units)
        insert_block("BLK_ELEC_DOWNLIGHT", ox_e + 1500.0, oy_e + 5500.0, layer="E-LITE")
        insert_block("BLK_ELEC_DOWNLIGHT", ox_e + 1500.0, oy_e + 7000.0, layer="E-LITE")
        # R. Makan Downlight 9W
        insert_block("BLK_ELEC_DOWNLIGHT", ox_e + 1500.0, oy_e + 8500.0, layer="E-LITE")
        # Dapur TL Lamp / LED Tube 18W
        insert_block("BLK_ELEC_TL_LAMP", ox_e + 1500.0, oy_e + 11250.0, layer="E-LITE")
        # K. Tidur Utama Downlight 12W
        insert_block("BLK_ELEC_DOWNLIGHT", ox_e + 4500.0, oy_e + 6250.0, layer="E-LITE")
        # KM / WC Downlight Waterproof 9W
        insert_block("BLK_ELEC_DOWNLIGHT", ox_e + 5250.0, oy_e + 9000.0, layer="E-LITE")
        # K. Tidur 2 Downlight 12W
        insert_block("BLK_ELEC_DOWNLIGHT", ox_e + 4500.0, oy_e + 11250.0, layer="E-LITE")
        # Taman Belakang wall lamp outdoor
        insert_block("BLK_ELEC_WALL_LAMP", ox_e + 3000.0, oy_e + 13300.0, layer="E-LITE")

        # 2. Switches
        # Main entrance double switch (Teras + R. Tamu)
        insert_block("BLK_ELEC_SW_DOUBLE", ox_e + 2850.0, oy_e + 4650.0, layer="E-POWR")
        # Living room double switch (R. Tamu 2 + R. Makan)
        insert_block("BLK_ELEC_SW_DOUBLE", ox_e + 2850.0, oy_e + 7100.0, layer="E-POWR")
        # Master Bed single switch
        insert_block("BLK_ELEC_SW_SINGLE", ox_e + 3150.0, oy_e + 7250.0, layer="E-POWR")
        # Hallway switch near KM/WC (KM/WC + Hallway)
        insert_block("BLK_ELEC_SW_DOUBLE", ox_e + 4350.0, oy_e + 8300.0, layer="E-POWR")
        # Bed 2 single switch
        insert_block("BLK_ELEC_SW_SINGLE", ox_e + 3150.0, oy_e + 10250.0, layer="E-POWR")
        # Kitchen double switch (Dapur + Service Belakang)
        insert_block("BLK_ELEC_SW_DOUBLE", ox_e + 2850.0, oy_e + 10250.0, layer="E-POWR")

        # 3. Power Outlets (Stop Kontak)
        # Living Room TV outlet
        insert_block("BLK_ELEC_OUTLET", ox_e + 250.0, oy_e + 6200.0, layer="E-POWR")
        # Master Bed bedside outlets & AC outlet
        insert_block("BLK_ELEC_OUTLET", ox_e + 3800.0, oy_e + 7500.0, layer="E-POWR")
        insert_block("BLK_ELEC_OUTLET", ox_e + 5800.0, oy_e + 7500.0, layer="E-POWR")
        insert_block("BLK_ELEC_OUTLET_AC", ox_e + 5850.0, oy_e + 4650.0, layer="E-POWR")
        # Bed 2 desk outlet & AC outlet
        insert_block("BLK_ELEC_OUTLET", ox_e + 3300.0, oy_e + 12350.0, layer="E-POWR")
        insert_block("BLK_ELEC_OUTLET_AC", ox_e + 5850.0, oy_e + 12350.0, layer="E-POWR")
        # Dining room outlet (dispenser/fan)
        insert_block("BLK_ELEC_OUTLET", ox_e + 250.0, oy_e + 8500.0, layer="E-POWR")
        # Kitchen counter outlets (refrigerator, stove, rice cooker)
        insert_block("BLK_ELEC_OUTLET", ox_e + 200.0, oy_e + 11800.0, layer="E-POWR")
        insert_block("BLK_ELEC_OUTLET", ox_e + 1500.0, oy_e + 12350.0, layer="E-POWR")

        # 4. MCB Box
        insert_block("BLK_ELEC_MCB_BOX", ox_e + 2850.0, oy_e + 6500.0, layer="E-POWR")

        # 5. Wiring Runs (DASHED lines / conduits)
        # Switch to Teras lamp & Living lamp
        add_line(ms, ox_e + 2850.0, oy_e + 4650.0, ox_e + 3200.0, oy_e + 3750.0, "E-CKTS")
        add_line(ms, ox_e + 2850.0, oy_e + 4650.0, ox_e + 1500.0, oy_e + 5500.0, "E-CKTS")
        add_line(ms, ox_e + 1500.0, oy_e + 5500.0, ox_e + 1500.0, oy_e + 7000.0, "E-CKTS")
        # Living switch to Dining lamp
        add_line(ms, ox_e + 2850.0, oy_e + 7100.0, ox_e + 1500.0, oy_e + 7000.0, "E-CKTS")
        add_line(ms, ox_e + 2850.0, oy_e + 7100.0, ox_e + 1500.0, oy_e + 8500.0, "E-CKTS")
        # Master Bed switch to lamp
        add_line(ms, ox_e + 3150.0, oy_e + 7250.0, ox_e + 4500.0, oy_e + 6250.0, "E-CKTS")
        # KM/WC switch to lamp
        add_line(ms, ox_e + 4350.0, oy_e + 8300.0, ox_e + 5250.0, oy_e + 9000.0, "E-CKTS")
        # Bed 2 switch to lamp
        add_line(ms, ox_e + 3150.0, oy_e + 10250.0, ox_e + 4500.0, oy_e + 11250.0, "E-CKTS")
        # Kitchen switch to TL lamp & Service lamp
        add_line(ms, ox_e + 2850.0, oy_e + 10250.0, ox_e + 1500.0, oy_e + 11250.0, "E-CKTS")
        add_line(ms, ox_e + 2850.0, oy_e + 10250.0, ox_e + 3000.0, oy_e + 13300.0, "E-CKTS")
        # Home runs to MCB Box
        add_line(ms, ox_e + 2850.0, oy_e + 6500.0, ox_e + 1500.0, oy_e + 7000.0, "E-CKTS")
        add_line(ms, ox_e + 2850.0, oy_e + 6500.0, ox_e + 250.0, oy_e + 6200.0, "E-CKTS")

        # 6. Electrical Legend Table
        leg_x = ox_e + 7500.0
        leg_y = oy_e + 14000.0
        add_rect(ms, leg_x, leg_y - 4500.0, leg_x + 3600.0, leg_y, "A-ANNO-TEXT")
        add_rect(ms, leg_x, leg_y - 600.0, leg_x + 3600.0, leg_y, "A-ANNO-TEXT")
        add_text(ms, "LEGENDA INSTALASI LISTRIK", leg_x + 1800.0, leg_y - 300.0, 180.0, "A-ANNO-TEXT")

        items_elec = [
            ("BLK_ELEC_DOWNLIGHT", "Lampu Downlight LED 12W / 9W"),
            ("BLK_ELEC_TL_LAMP", "Lampu TL / LED Tube 18W"),
            ("BLK_ELEC_WALL_LAMP", "Lampu Dinding Outbow 9W"),
            ("BLK_ELEC_SW_SINGLE", "Saklar Tunggal (h=150 cm)"),
            ("BLK_ELEC_SW_DOUBLE", "Saklar Ganda / Seri (h=150 cm)"),
            ("BLK_ELEC_OUTLET", "Stop Kontak 200W (h=30 cm)"),
            ("BLK_ELEC_OUTLET_AC", "Stop Kontak Khusus AC (h=200 cm)"),
            ("BLK_ELEC_MCB_BOX", "Panel Hubung Bagi MCB 3 Group"),
        ]
        for idx, (b_name, desc) in enumerate(items_elec):
            iy = leg_y - 950.0 - idx * 450.0
            insert_block(b_name, leg_x + 350.0, iy, sx=0.8, sy=0.8, layer="E-POWR")
            add_text(ms, desc, leg_x + 800.0, iy, 140.0, "A-ANNO-TEXT", align=9)  # left aligned

        add_title_block(ox_e + 3000.0, oy_e - 3600.0, "DENAH INSTALASI LISTRIK & TITIK LAMPU", "1 : 100", "02")

        # =============================================================
        # DRAWING 3: DENAH INSTALASI AIR BERSIH (Origin X=24000, Y=0)
        # =============================================================
        print("Drafting Drawing 3: Instalasi Air Bersih...")
        ox_w = 24000.0
        oy_w = 0.0
        draw_dimmed_plan_base(ox_w, oy_w)

        # 1. Sources & Storage
        # PDAM Water Meter at front boundary
        insert_block("BLK_PLBG_METER", ox_w + 600.0, oy_w + 200.0, layer="P-WATR-COLD")
        insert_block("BLK_PLBG_VALVE", ox_w + 1000.0, oy_w + 200.0, layer="P-WATR-COLD")
        # Overhead Water Tank (Toren 500L) + Booster Pump in service yard
        insert_block("BLK_PLBG_TOREN", ox_w + 800.0, oy_w + 13400.0, layer="P-WATR-COLD")
        insert_block("BLK_PLBG_VALVE", ox_w + 1300.0, oy_w + 13400.0, layer="P-WATR-COLD")

        # 2. Piping Route
        # PDAM Main supply line PVC AW 3/4" to Toren
        add_line(ms, ox_w + 600.0, oy_w + 200.0, ox_w + 200.0, oy_w + 200.0, "P-WATR-COLD")
        add_line(ms, ox_w + 200.0, oy_w + 200.0, ox_w + 200.0, oy_w + 13400.0, "P-WATR-COLD")
        add_line(ms, ox_w + 200.0, oy_w + 13400.0, ox_w + 400.0, oy_w + 13400.0, "P-WATR-COLD")

        # Front garden tap branch from meter
        add_line(ms, ox_w + 600.0, oy_w + 200.0, ox_w + 600.0, oy_w + 800.0, "P-WATR-DIST")
        add_circle(ms, ox_w + 600.0, oy_w + 800.0, 60.0, "P-WATR-DIST")
        add_text(ms, "KRAN TAMAN", ox_w + 1200.0, oy_w + 800.0, 110.0, "A-ANNO-TEXT")

        # Distribution from Toren / Booster (PVC AW 3/4" manifold)
        # To Kitchen sink
        add_line(ms, ox_w + 1300.0, oy_w + 13400.0, ox_w + 2150.0, oy_w + 13400.0, "P-WATR-COLD")
        add_line(ms, ox_w + 2150.0, oy_w + 13400.0, ox_w + 2150.0, oy_w + 12350.0, "P-WATR-DIST")
        add_circle(ms, ox_w + 2150.0, oy_w + 12350.0, 60.0, "P-WATR-DIST")
        add_text(ms, "SINK DAPUR", ox_w + 2750.0, oy_w + 12350.0, 110.0, "A-ANNO-TEXT")

        # To Service yard washing tap
        add_line(ms, ox_w + 2150.0, oy_w + 13400.0, ox_w + 3500.0, oy_w + 13400.0, "P-WATR-DIST")
        add_circle(ms, ox_w + 3500.0, oy_w + 13400.0, 60.0, "P-WATR-DIST")
        add_text(ms, "KRAN CUCI", ox_w + 4100.0, oy_w + 13400.0, 110.0, "A-ANNO-TEXT")

        # Main feeder to KM/WC running along ceiling/spine (PVC AW 3/4")
        add_line(ms, ox_w + 2150.0, oy_w + 13400.0, ox_w + 2900.0, oy_w + 13400.0, "P-WATR-COLD")
        add_line(ms, ox_w + 2900.0, oy_w + 13400.0, ox_w + 2900.0, oy_w + 9800.0, "P-WATR-COLD")
        add_line(ms, ox_w + 2900.0, oy_w + 9800.0, ox_w + 4500.0, oy_w + 9800.0, "P-WATR-COLD")

        # KM/WC gate valve
        insert_block("BLK_PLBG_VALVE", ox_w + 4650.0, oy_w + 9800.0, layer="P-WATR-DIST")

        # KM/WC fixtures branch (PVC AW 1/2")
        # Shower
        add_line(ms, ox_w + 4650.0, oy_w + 9800.0, ox_w + 5100.0, oy_w + 9800.0, "P-WATR-DIST")
        add_line(ms, ox_w + 5100.0, oy_w + 9800.0, ox_w + 5100.0, oy_w + 9200.0, "P-WATR-DIST")
        add_circle(ms, ox_w + 5100.0, oy_w + 9200.0, 60.0, "P-WATR-DIST")
        add_text(ms, "SHOWER", ox_w + 5550.0, oy_w + 9200.0, 110.0, "A-ANNO-TEXT")

        # Closet & Jet Washer
        add_line(ms, ox_w + 5100.0, oy_w + 9800.0, ox_w + 5700.0, oy_w + 9800.0, "P-WATR-DIST")
        add_line(ms, ox_w + 5700.0, oy_w + 9800.0, ox_w + 5700.0, oy_w + 8300.0, "P-WATR-DIST")
        add_line(ms, ox_w + 5700.0, oy_w + 8300.0, ox_w + 5500.0, oy_w + 8300.0, "P-WATR-DIST")
        add_circle(ms, ox_w + 5500.0, oy_w + 8300.0, 60.0, "P-WATR-DIST")
        add_text(ms, "KLOSET", ox_w + 5200.0, oy_w + 8000.0, 110.0, "A-ANNO-TEXT")

        # Annotations on pipes
        add_text(ms, "PIPA PVC AW Dia 3/4\" (DARI PDAM)", ox_w + 900.0, oy_w + 6500.0, 120.0, "A-ANNO-TEXT", rot=1.570796)
        add_text(ms, "PIPA PVC AW Dia 1/2\" DISTRIBUSI", ox_w + 3500.0, oy_w + 9950.0, 110.0, "A-ANNO-TEXT")

        # Clean Water Legend
        leg_w_x = ox_w + 7500.0
        leg_w_y = oy_w + 14000.0
        add_rect(ms, leg_w_x, leg_w_y - 4200.0, leg_w_x + 3600.0, leg_w_y, "A-ANNO-TEXT")
        add_rect(ms, leg_w_x, leg_w_y - 600.0, leg_w_x + 3600.0, leg_w_y, "A-ANNO-TEXT")
        add_text(ms, "LEGENDA AIR BERSIH", leg_w_x + 1800.0, leg_w_y - 300.0, 180.0, "A-ANNO-TEXT")

        items_water = [
            ("BLK_PLBG_METER", "Meteran Air PDAM"),
            ("BLK_PLBG_TOREN", "Toren Air 500L + Booster Pump"),
            ("BLK_PLBG_VALVE", "Gate Valve / Stop Kran"),
            ("LINE_34", "Pipa Utama PVC AW Dia 3/4\""),
            ("LINE_12", "Pipa Cabang PVC AW Dia 1/2\""),
            ("TAP_PT", "Titik Kran Air Bersih"),
        ]
        for idx, (b_name, desc) in enumerate(items_water):
            iy = leg_w_y - 950.0 - idx * 550.0
            if "BLK" in b_name:
                insert_block(b_name, leg_w_x + 350.0, iy, sx=0.6, sy=0.6, layer="P-WATR-COLD")
            elif "LINE" in b_name:
                add_line(ms, leg_w_x + 150.0, iy, leg_w_x + 550.0, iy, "P-WATR-COLD")
            elif "TAP" in b_name:
                add_circle(ms, leg_w_x + 350.0, iy, 80.0, "P-WATR-DIST")
            add_text(ms, desc, leg_w_x + 800.0, iy, 140.0, "A-ANNO-TEXT", align=9)

        add_title_block(ox_w + 3000.0, oy_w - 3600.0, "DENAH INSTALASI AIR BERSIH", "1 : 100", "03")

        # =============================================================
        # DRAWING 4: DENAH AIR KOTOR, AIR BEKAS & DRAINASE (Origin X=36000, Y=0)
        # =============================================================
        print("Drafting Drawing 4: Instalasi Air Kotor & Air Bekas...")
        ox_s = 36000.0
        oy_s = 0.0
        draw_dimmed_plan_base(ox_s, oy_s)

        # 1. Treatment & Inspection Units
        # Bio Septic Tank (Carport/front garden)
        insert_block("BLK_PLBG_SEPTIC_TANK", ox_s + 1500.0, oy_s + 1500.0, layer="P-SOIL")
        # Sumur Resapan Air Kotor
        insert_block("BLK_PLBG_RESAPAN", ox_s + 1500.0, oy_s - 1000.0, layer="P-SOIL")
        # Grease Trap (under kitchen sink)
        insert_block("BLK_PLBG_GREASE_TRAP", ox_s + 2150.0, oy_s + 11800.0, layer="P-WAST")
        # Inspection Boxes (Bak Kontrol)
        insert_block("BLK_PLBG_BAK_KONTROL", ox_s + 4500.0, oy_s + 7800.0, layer="P-WAST")  # BK 1 (KM/WC)
        insert_block("BLK_PLBG_BAK_KONTROL", ox_s + 400.0, oy_s + 11800.0, layer="P-WAST")   # BK 2 (Kitchen/Service)
        insert_block("BLK_PLBG_BAK_KONTROL", ox_s + 400.0, oy_s + 4500.0, layer="P-WAST")    # BK 3 (Mid junction)
        insert_block("BLK_PLBG_BAK_KONTROL", ox_s + 400.0, oy_s + 200.0, layer="P-WAST")     # BK 4 (Front outlet)

        # 2. Black Water Line (Air Kotor / Tinja - Pipa PVC AW Dia 4")
        # Closet (5400, 8300) south to Septic Tank
        add_line(ms, ox_s + 5400.0, oy_s + 8300.0, ox_s + 5400.0, oy_s + 7600.0, "P-SOIL")
        add_line(ms, ox_s + 5400.0, oy_s + 7600.0, ox_s + 2900.0, oy_s + 7600.0, "P-SOIL")
        add_line(ms, ox_s + 2900.0, oy_s + 7600.0, ox_s + 2900.0, oy_s + 2500.0, "P-SOIL")
        add_line(ms, ox_s + 2900.0, oy_s + 2500.0, ox_s + 1500.0, oy_s + 2500.0, "P-SOIL")
        add_line(ms, ox_s + 1500.0, oy_s + 2500.0, ox_s + 1500.0, oy_s + 2000.0, "P-SOIL")

        # Septic Tank overflow to Sumur Resapan
        add_line(ms, ox_s + 1500.0, oy_s + 1000.0, ox_s + 1500.0, oy_s + -600.0, "P-SOIL")

        # Vent Pipe Dia 1.5"
        add_line(ms, ox_s + 1500.0, oy_s + 2000.0, ox_s + 2800.0, oy_s + 2000.0, "P-VENT")
        add_circle(ms, ox_s + 2800.0, oy_s + 2000.0, 75.0, "P-VENT")
        add_text(ms, "PIPA HAWA Dia 1.5\"", ox_s + 3600.0, oy_s + 2000.0, 110.0, "A-ANNO-TEXT")

        # 3. Grey Water Line (Air Bekas - Pipa PVC D/AW Dia 3")
        # KM/WC Floor drain & sink to BK 1
        add_line(ms, ox_s + 4800.0, oy_s + 9600.0, ox_s + 4800.0, oy_s + 8500.0, "P-WAST")
        add_line(ms, ox_s + 4800.0, oy_s + 8500.0, ox_s + 4500.0, oy_s + 7975.0, "P-WAST")

        # BK 1 to BK 3 (slope 1%)
        add_line(ms, ox_s + 4500.0, oy_s + 7625.0, ox_s + 4500.0, oy_s + 7200.0, "P-WAST")
        add_line(ms, ox_s + 4500.0, oy_s + 7200.0, ox_s + 575.0, oy_s + 4500.0, "P-WAST")

        # Kitchen Sink to Grease Trap to BK 2
        add_line(ms, ox_s + 2150.0, oy_s + 12200.0, ox_s + 2150.0, oy_s + 11925.0, "P-WAST")
        add_line(ms, ox_s + 1950.0, oy_s + 11800.0, ox_s + 575.0, oy_s + 11800.0, "P-WAST")

        # Service yard drain to BK 2
        add_line(ms, ox_s + 1000.0, oy_s + 13400.0, ox_s + 400.0, oy_s + 13400.0, "P-WAST")
        add_line(ms, ox_s + 400.0, oy_s + 13400.0, ox_s + 400.0, oy_s + 11975.0, "P-WAST")

        # BK 2 to BK 3 to BK 4 (along party wall)
        add_line(ms, ox_s + 400.0, oy_s + 11625.0, ox_s + 400.0, oy_s + 4675.0, "P-WAST")
        add_line(ms, ox_s + 400.0, oy_s + 4325.0, ox_s + 400.0, oy_s + 375.0, "P-WAST")

        # BK 4 discharge to City Drain / Saluran Kota
        add_line(ms, ox_s + 400.0, oy_s + 25.0, ox_s + 400.0, oy_s - 1200.0, "P-STOR")
        add_text(ms, "KE SALURAN DRAINASE KOTA", ox_s + 1500.0, oy_s - 1200.0, 130.0, "A-ANNO-TEXT")

        # Pipe Callouts
        add_text(ms, "PIPA PVC AW Dia 4\" AIR KOTOR (TINJA) - KEMIRINGAN 2%", ox_s + 4200.0, oy_s + 5500.0, 120.0, "A-ANNO-TEXT", rot=1.570796)
        add_text(ms, "PIPA PVC D Dia 3\" AIR BEKAS - KEMIRINGAN 1%", ox_s + 700.0, oy_s + 8500.0, 120.0, "A-ANNO-TEXT", rot=1.570796)

        # Drainage Legend Table
        leg_s_x = ox_s + 7500.0
        leg_s_y = oy_s + 14000.0
        add_rect(ms, leg_s_x, leg_s_y - 4200.0, leg_s_x + 3600.0, leg_s_y, "A-ANNO-TEXT")
        add_rect(ms, leg_s_x, leg_s_y - 600.0, leg_s_x + 3600.0, leg_s_y, "A-ANNO-TEXT")
        add_text(ms, "LEGENDA AIR KOTOR & BEKAS", leg_s_x + 1800.0, leg_s_y - 300.0, 170.0, "A-ANNO-TEXT")

        items_drain = [
            ("BLK_PLBG_SEPTIC_TANK", "Bio-Septic Tank Kapasitas 1000L"),
            ("BLK_PLBG_RESAPAN", "Sumur Resapan Air Kotor Dia 80cm"),
            ("BLK_PLBG_BAK_KONTROL", "Bak Kontrol Pas. Bata 35x35 cm"),
            ("BLK_PLBG_GREASE_TRAP", "Perangkap Lemak (Grease Trap)"),
            ("LINE_SOIL", "Pipa Air Kotor PVC AW Dia 4\" (S=2%)"),
            ("LINE_WAST", "Pipa Air Bekas PVC D Dia 3\" (S=1%)"),
        ]
        for idx, (b_name, desc) in enumerate(items_drain):
            iy = leg_s_y - 950.0 - idx * 550.0
            if "BLK" in b_name:
                insert_block(b_name, leg_s_x + 350.0, iy, sx=0.5, sy=0.5, layer="P-WAST")
            elif "LINE" in b_name:
                ly = "P-SOIL" if "SOIL" in b_name else "P-WAST"
                add_line(ms, leg_s_x + 150.0, iy, leg_s_x + 550.0, iy, ly)
            add_text(ms, desc, leg_s_x + 800.0, iy, 140.0, "A-ANNO-TEXT", align=9)

        add_title_block(ox_s + 3000.0, oy_s - 3600.0, "DENAH INSTALASI AIR KOTOR & AIR BEKAS", "1 : 100", "04")

        # =============================================================
        # DRAWING 5: TAMPAK DEPAN (Front Elevation) - Origin X=0, Y=22000
        # =============================================================
        print("Drafting Drawing 5: Tampak Depan...")
        ox_td = 0.0
        oy_td = 22000.0

        # Ground line (GL -300) & FFL (±0.00)
        add_line(ms, ox_td - 1500.0, oy_td - 300.0, ox_td + 7500.0, oy_td - 300.0, "A-ELEV")
        add_line(ms, ox_td + 0.0, oy_td + 0.0, ox_td + 6000.0, oy_td + 0.0, "A-ELEV")

        # Front Facade Outline
        # Carport canopy (X: 0 to 3000, h=3200)
        add_rect(ms, ox_td + 0.0, oy_td + 0.0, ox_td + 150.0, oy_td + 3200.0, "A-ELEV")  # Column
        add_rect(ms, ox_td - 200.0, oy_td + 3100.0, ox_td + 3200.0, oy_td + 3300.0, "A-ELEV")  # Canopy beam
        add_line(ms, ox_td + 0.0, oy_td + 3300.0, ox_td + 3000.0, oy_td + 3450.0, "A-ELEV")  # Sloped tempered glass/spandek

        # Main House Facade (X: 0 to 6000, Ringbalk at 3600, Parapet at 4200, Roof peak at 6500)
        add_rect(ms, ox_td + 0.0, oy_td + 0.0, ox_td + 6000.0, oy_td + 3600.0, "A-ELEV")

        # Master Bedroom Feature Wall (X: 3000 to 6000) with Parapet (h=4200)
        add_rect(ms, ox_td + 2950.0, oy_td + 0.0, ox_td + 6050.0, oy_td + 4200.0, "A-ELEV")

        # Large Master Bed Window W1 (X: 3800 to 5200, h: 600 to 2400)
        add_rect(ms, ox_td + 3800.0, oy_td + 600.0, ox_td + 5200.0, oy_td + 2400.0, "A-GLAZ")
        add_rect(ms, ox_td + 3850.0, oy_td + 650.0, ox_td + 4475.0, oy_td + 2350.0, "A-GLAZ")
        add_rect(ms, ox_td + 4525.0, oy_td + 650.0, ox_td + 5150.0, oy_td + 2350.0, "A-GLAZ")

        # Main Door D1 (X: 1900 to 2800, h: 0 to 2400)
        add_rect(ms, ox_td + 1900.0, oy_td + 0.0, ox_td + 2800.0, oy_td + 2400.0, "A-DOOR")
        add_rect(ms, ox_td + 1950.0, oy_td + 50.0, ox_td + 2750.0, oy_td + 2350.0, "A-DOOR")
        add_line(ms, ox_td + 2650.0, oy_td + 1050.0, ox_td + 2650.0, oy_td + 1250.0, "A-DOOR")  # Handle

        # Living Room Window W3 (X: 400 to 1600, h: 600 to 2400)
        add_rect(ms, ox_td + 400.0, oy_td + 600.0, ox_td + 1600.0, oy_td + 2400.0, "A-GLAZ")
        add_rect(ms, ox_td + 450.0, oy_td + 650.0, ox_td + 1550.0, oy_td + 2350.0, "A-GLAZ")

        # Roof Triangle Profile (Gable / Pelana sopi-sopi rising from 3600 to 6500)
        roof_pts = [
            [ox_td + 0.0, oy_td + 3600.0],
            [ox_td + 3000.0, oy_td + 6500.0],
            [ox_td + 6000.0, oy_td + 3600.0],
            [ox_td + 6000.0, oy_td + 4200.0],
            [ox_td + 3000.0, oy_td + 6500.0],
            [ox_td + 0.0, oy_td + 4200.0]
        ]
        add_polyline(ms, roof_pts, True, "A-ELEV")

        # Texture Hatch on Accent Wall (Travertine / Wood slat texture)
        accent_pl = add_rect(ms, ox_td + 3000.0, oy_td + 2600.0, ox_td + 6000.0, oy_td + 4200.0, "A-ELEV-PATS")
        add_hatch_to(ms, accent_pl, "ANSI31", 20.0, "A-ELEV-PATS")

        # Elevation Level Markers
        def add_level_marker(x, y, label):
            add_line(ms, x - 300.0, y, x + 600.0, y, "A-ANNO-SYMB")
            add_polyline(ms, [[x, y], [x + 150.0, y + 150.0], [x - 150.0, y + 150.0]], True, "A-ANNO-SYMB")
            add_text(ms, label, x + 200.0, y + 80.0, 140.0, "A-ANNO-TEXT", align=9)

        add_level_marker(ox_td - 800.0, oy_td - 300.0, "ELV. -0.30 MUKA TANAH")
        add_level_marker(ox_td - 800.0, oy_td + 0.0, "ELV. ±0.00 MUKA LANTAI")
        add_level_marker(ox_td - 800.0, oy_td + 2400.0, "ELV. +2.40 TOP KUSEN")
        add_level_marker(ox_td - 800.0, oy_td + 3600.0, "ELV. +3.60 RING BALK")
        add_level_marker(ox_td - 800.0, oy_td + 4200.0, "ELV. +4.20 TOP PARAPET")
        add_level_marker(ox_td - 800.0, oy_td + 6500.0, "ELV. +6.50 TOP BUBUNGAN")

        add_dim(ox_td + 0.0, oy_td + 0.0, ox_td + 6000.0, oy_td + 0.0, ox_td + 3000.0, oy_td - 1000.0, 0.0, "A-DIMS-EXT", "6000")
        add_title_block(ox_td + 3000.0, oy_td - 2500.0, "TAMPAK DEPAN", "1 : 100", "05")

        # =============================================================
        # DRAWING 6: TAMPAK BELAKANG (Rear Elevation) - Origin X=12000, Y=22000
        # =============================================================
        print("Drafting Drawing 6: Tampak Belakang...")
        ox_tb = 12000.0
        oy_tb = 22000.0

        add_line(ms, ox_tb - 1000.0, oy_tb - 300.0, ox_tb + 7000.0, oy_tb - 300.0, "A-ELEV")
        add_line(ms, ox_tb + 0.0, oy_tb + 0.0, ox_tb + 6000.0, oy_tb + 0.0, "A-ELEV")

        # Rear facade main wall (h=3600)
        add_rect(ms, ox_tb + 0.0, oy_tb + 0.0, ox_tb + 6000.0, oy_tb + 3600.0, "A-ELEV")

        # Service door D4 (X: 500 to 1300, h: 2100)
        add_rect(ms, ox_tb + 500.0, oy_tb + 0.0, ox_tb + 1300.0, oy_tb + 2100.0, "A-DOOR")
        add_rect(ms, ox_tb + 550.0, oy_tb + 50.0, ox_tb + 1250.0, oy_tb + 2050.0, "A-DOOR")

        # Kitchen window W4 (X: 1700 to 2600, h: 1200 to 2100)
        add_rect(ms, ox_tb + 1700.0, oy_tb + 1200.0, ox_tb + 2600.0, oy_tb + 2100.0, "A-GLAZ")

        # Bedroom 2 window W2 (X: 3900 to 5100, h: 600 to 2400)
        add_rect(ms, ox_tb + 3900.0, oy_tb + 600.0, ox_tb + 5100.0, oy_tb + 2400.0, "A-GLAZ")

        # Roof triangle profile
        rear_roof = [
            [ox_tb + 0.0, oy_tb + 3600.0],
            [ox_tb + 3000.0, oy_tb + 6500.0],
            [ox_tb + 6000.0, oy_tb + 3600.0]
        ]
        add_polyline(ms, rear_roof, True, "A-ELEV")

        # Boundary wall in front of rear yard (h=2500)
        add_rect(ms, ox_tb + 0.0, oy_tb - 150.0, ox_tb + 6000.0, oy_tb + 2350.0, "A-ELEV")
        add_text(ms, "DINDING PAGAR BELAKANG (h=2.50m)", ox_tb + 3000.0, oy_tb + 1200.0, 150.0, "A-ANNO-TEXT")

        add_level_marker(ox_tb - 600.0, oy_tb - 300.0, "ELV. -0.30")
        add_level_marker(ox_tb - 600.0, oy_tb + 0.0, "ELV. ±0.00")
        add_level_marker(ox_tb - 600.0, oy_tb + 3600.0, "ELV. +3.60")
        add_level_marker(ox_tb - 600.0, oy_tb + 6500.0, "ELV. +6.50")

        add_dim(ox_tb + 0.0, oy_tb + 0.0, ox_tb + 6000.0, oy_tb + 0.0, ox_tb + 3000.0, oy_tb - 1000.0, 0.0, "A-DIMS-EXT", "6000")
        add_title_block(ox_tb + 3000.0, oy_tb - 2500.0, "TAMPAK BELAKANG", "1 : 100", "06")

        # =============================================================
        # DRAWING 7: TAMPAK SAMPING KANAN (Side Elevation) - Origin X=24000, Y=22000
        # =============================================================
        print("Drafting Drawing 7: Tampak Samping Kanan...")
        ox_ts = 24000.0
        oy_ts = 22000.0

        # Ground line 14m length
        add_line(ms, ox_ts - 1000.0, oy_ts - 300.0, ox_ts + 15000.0, oy_ts - 300.0, "A-ELEV")
        add_line(ms, ox_ts + 0.0, oy_ts + 0.0, ox_ts + 14000.0, oy_ts + 0.0, "A-ELEV")

        # Front Carport & Porch area (Y: 0 to 4500)
        add_rect(ms, ox_ts + 0.0, oy_ts + 0.0, ox_ts + 4500.0, oy_ts + 3200.0, "A-ELEV")

        # Main side wall body (Y: 4500 to 12500, h=3600 to 4200)
        add_rect(ms, ox_ts + 4500.0, oy_ts + 0.0, ox_ts + 12500.0, oy_ts + 3600.0, "A-ELEV")
        # Parapet / Sopi-sopi peak at Y=8500 (h=6500)
        side_roof = [
            [ox_ts + 4500.0, oy_ts + 3600.0],
            [ox_ts + 8500.0, oy_ts + 6500.0],
            [ox_ts + 12500.0, oy_ts + 3600.0]
        ]
        add_polyline(ms, side_roof, True, "A-ELEV")

        # Roof genteng hatch on side slope
        side_hatch_pl = add_polyline(ms, [
            [ox_ts + 4500.0, oy_ts + 3600.0],
            [ox_ts + 8500.0, oy_ts + 6500.0],
            [ox_ts + 12500.0, oy_ts + 3600.0],
            [ox_ts + 12500.0, oy_ts + 3300.0],
            [ox_ts + 4500.0, oy_ts + 3300.0]
        ], True, "A-ELEV-PATS")
        add_hatch_to(ms, side_hatch_pl, "ANSI31", 25.0, "A-ELEV-PATS")

        # Rear boundary wall (Y: 12500 to 14000, h=2500)
        add_rect(ms, ox_ts + 12500.0, oy_ts + 0.0, ox_ts + 14000.0, oy_ts + 2500.0, "A-ELEV")

        # Minimalist horizontal wall groove lines (tali air)
        for hg in [1000.0, 1800.0, 2600.0, 3400.0]:
            add_line(ms, ox_ts + 4500.0, oy_ts + hg, ox_ts + 12500.0, oy_ts + hg, "A-ELEV-PATS")

        # Level markers
        add_level_marker(ox_ts - 600.0, oy_ts - 300.0, "ELV. -0.30")
        add_level_marker(ox_ts - 600.0, oy_ts + 0.0, "ELV. ±0.00")
        add_level_marker(ox_ts - 600.0, oy_ts + 3600.0, "ELV. +3.60")
        add_level_marker(ox_ts - 600.0, oy_ts + 6500.0, "ELV. +6.50")

        # Dimensions along length
        add_dim(ox_ts + 0.0, oy_ts + 0.0, ox_ts + 4500.0, oy_ts + 0.0, ox_ts + 2250.0, oy_ts - 1000.0, 0.0, "A-DIMS-EXT", "4500")
        add_dim(ox_ts + 4500.0, oy_ts + 0.0, ox_ts + 12500.0, oy_ts + 0.0, ox_ts + 8500.0, oy_ts - 1000.0, 0.0, "A-DIMS-EXT", "8000")
        add_dim(ox_ts + 12500.0, oy_ts + 0.0, ox_ts + 14000.0, oy_ts + 0.0, ox_ts + 13250.0, oy_ts - 1000.0, 0.0, "A-DIMS-EXT", "1500")
        add_dim(ox_ts + 0.0, oy_ts + 0.0, ox_ts + 14000.0, oy_ts + 0.0, ox_ts + 7000.0, oy_ts - 1800.0, 0.0, "A-DIMS-EXT", "14000")

        add_title_block(ox_ts + 7000.0, oy_ts - 2600.0, "TAMPAK SAMPING KANAN", "1 : 100", "07")

        # =============================================================
        # DRAWING 8: POTONGAN A-A (Transverse Section) - Origin X=0, Y=34000
        # Skala 1 : 100 - High Precision Detailing
        # =============================================================
        print("Drafting Drawing 8: Potongan A-A (High-Precision Detailed)...")
        ox_pa = 0.0
        oy_pa = 34000.0

        # Helpers for callout leaders with landing shelf
        def add_callout(px, py, text, off_x=800.0, off_y=400.0, layer="A-ANNO-TEXT"):
            ex = px + off_x
            ey = py + off_y
            shelf = 450.0 if off_x >= 0 else -450.0
            add_line(ms, px, py, ex, ey, "A-ANNO-SYMB")
            add_line(ms, ex, ey, ex + shelf, ey, "A-ANNO-SYMB")
            add_circle(ms, px, py, 25.0, "A-ANNO-SYMB")
            align = 9 if off_x >= 0 else 11
            tx = ex + (50.0 if off_x >= 0 else -50.0)
            add_text(ms, text, tx, ey + 60.0, 130.0, layer, align=align)

        # 1. Tanah Dasar & Muka Tanah
        add_line(ms, ox_pa - 1500.0, oy_pa - 300.0, ox_pa + 7500.0, oy_pa - 300.0, "A-FLOR")

        # 2. Detailed Substructure & Foundations at X=0, 3000, 6000
        for fx in [0.0, 3000.0, 6000.0]:
            # Galian Tanah (Excavation Trench) with 1:5 slope
            trench_pts = [
                [ox_pa + fx - 700.0, oy_pa - 300.0],
                [ox_pa + fx - 550.0, oy_pa - 1000.0],
                [ox_pa + fx + 550.0, oy_pa - 1000.0],
                [ox_pa + fx + 700.0, oy_pa - 300.0]
            ]
            add_polyline(ms, trench_pts, False, "S-FNDN")

            # Lapisan Pasir Urug Bawah Pondasi (100 mm)
            pu_pl = add_rect(ms, ox_pa + fx - 500.0, oy_pa - 1000.0, ox_pa + fx + 500.0, oy_pa - 900.0, "S-FNDN")
            add_hatch_to(ms, pu_pl, "AR-SAND", 10.0, "S-FNDN")

            # Aanstamping (Batu Kosong 200 mm) with individual oblong stones
            as_pl = add_rect(ms, ox_pa + fx - 450.0, oy_pa - 900.0, ox_pa + fx + 450.0, oy_pa - 700.0, "S-FNDN")
            add_hatch_to(ms, as_pl, "GRAVEL", 20.0, "S-FNDN")
            # Draw individual rounded stone shapes
            st_x = ox_pa + fx - 400.0
            while st_x <= ox_pa + fx + 400.0:
                add_polyline(ms, [
                    [st_x - 35.0, oy_pa - 800.0],
                    [st_x - 25.0, oy_pa - 715.0],
                    [st_x, oy_pa - 705.0],
                    [st_x + 25.0, oy_pa - 715.0],
                    [st_x + 35.0, oy_pa - 800.0],
                    [st_x + 25.0, oy_pa - 885.0],
                    [st_x, oy_pa - 895.0],
                    [st_x - 25.0, oy_pa - 885.0]
                ], True, "S-FNDN")
                st_x += 80.0

            # Pondasi Batu Kali Trapesium (300 mm top, 700 mm bottom, 600 mm height)
            f_pts = [
                [ox_pa + fx - 150.0, oy_pa - 100.0],
                [ox_pa + fx + 150.0, oy_pa - 100.0],
                [ox_pa + fx + 350.0, oy_pa - 700.0],
                [ox_pa + fx - 350.0, oy_pa - 700.0]
            ]
            f_pl = add_polyline(ms, f_pts, True, "S-FNDN")
            add_hatch_to(ms, f_pl, "GRAVEL", 15.0, "S-FNDN")

            # Sloof Beton Bertulang 150x200 mm
            sl_pl = add_rect(ms, ox_pa + fx - 75.0, oy_pa - 100.0, ox_pa + fx + 75.0, oy_pa + 100.0, "S-FNDN")
            # Sengkang begel (100x150 mm)
            add_rect(ms, ox_pa + fx - 50.0, oy_pa - 75.0, ox_pa + fx + 50.0, oy_pa + 75.0, "S-FNDN")
            # 4 Tulangan Utama Dia 10 mm (Solid dots)
            for bx, by in [(-45.0, -70.0), (45.0, -70.0), (45.0, 70.0), (-45.0, 70.0)]:
                bc = add_circle(ms, ox_pa + fx + bx, oy_pa + by, 5.0, "S-FNDN")
                add_hatch_to(ms, bc, "SOLID", 1.0, "S-FNDN")

            # Dinding Trasraam Kedap Air (1:2) setinggi 300 mm di atas sloof
            tr_pl = add_rect(ms, ox_pa + fx - 75.0, oy_pa + 100.0, ox_pa + fx + 75.0, oy_pa + 400.0, "A-WALL-FULL")
            add_hatch_to(ms, tr_pl, "ANSI31", 6.0, "A-WALL-PATS")

            # Dinding Bata Merah 150 mm (Plester aci 20 mm + bata 110 mm) dari +400 sampai +3600
            cw_pl = add_rect(ms, ox_pa + fx - 75.0, oy_pa + 400.0, ox_pa + fx + 75.0, oy_pa + 3600.0, "A-WALL-FULL")
            add_hatch_to(ms, cw_pl, "ANSI31", 12.0, "A-WALL-PATS")
            # Garis plesteran aci (20 mm)
            add_line(ms, ox_pa + fx - 55.0, oy_pa + 400.0, ox_pa + fx - 55.0, oy_pa + 3600.0, "A-WALL-FULL")
            add_line(ms, ox_pa + fx + 55.0, oy_pa + 400.0, ox_pa + fx + 55.0, oy_pa + 3600.0, "A-WALL-FULL")

            # Ringbalk Beton Bertulang 150x200 mm at +3600 to +3800
            rb_pl = add_rect(ms, ox_pa + fx - 75.0, oy_pa + 3600.0, ox_pa + fx + 75.0, oy_pa + 3800.0, "S-FNDN")
            add_rect(ms, ox_pa + fx - 50.0, oy_pa + 3625.0, ox_pa + fx + 50.0, oy_pa + 3775.0, "S-FNDN")
            for bx, by in [(-45.0, 3630.0), (45.0, 3630.0), (45.0, 3770.0), (-45.0, 3770.0)]:
                rbc = add_circle(ms, ox_pa + fx + bx, oy_pa + by, 5.0, "S-FNDN")
                add_hatch_to(ms, rbc, "SOLID", 1.0, "S-FNDN")

            # Angkur Baja Dia 12 mm menghubungkan ringbalk ke tumpuan kuda-kuda
            add_line(ms, ox_pa + fx, oy_pa + 3620.0, ox_pa + fx, oy_pa + 3840.0, "S-ROOF")
            add_line(ms, ox_pa + fx - 25.0, oy_pa + 3620.0, ox_pa + fx, oy_pa + 3620.0, "S-ROOF")
            add_rect(ms, ox_pa + fx - 15.0, oy_pa + 3810.0, ox_pa + fx + 15.0, oy_pa + 3825.0, "S-ROOF")

        # 3. Lapisan Lantai Interior (Tanah urug, pasir urug 50mm, rabat 50mm, spesi 30mm, granit 10mm)
        for r_x1, r_x2, ffl_h in [(75.0, 2925.0, 0.0), (3075.0, 5925.0, 30.0)]:
            # Pasir urug (50 mm)
            pu_fl = add_rect(ms, ox_pa + r_x1, oy_pa - 90.0 + ffl_h, ox_pa + r_x2, oy_pa - 40.0 + ffl_h, "A-FLOR")
            add_hatch_to(ms, pu_fl, "AR-SAND", 8.0, "A-FLOR")
            # Rabat beton (50 mm)
            rb_fl = add_rect(ms, ox_pa + r_x1, oy_pa - 40.0 + ffl_h, ox_pa + r_x2, oy_pa + ffl_h - 10.0, "A-FLOR")
            # Granit tile 600x600x10 mm + spesi
            add_rect(ms, ox_pa + r_x1, oy_pa + ffl_h - 10.0, ox_pa + r_x2, oy_pa + ffl_h, "A-FLOR")
            # Tile joints every 600 mm
            tj = ox_pa + r_x1 + 600.0
            while tj < ox_pa + r_x2:
                add_line(ms, tj, oy_pa + ffl_h - 10.0, tj, oy_pa + ffl_h, "A-FLOR")
                tj += 600.0
            # Plin granit 100 mm
            add_rect(ms, ox_pa + r_x1, oy_pa + ffl_h, ox_pa + r_x1 + 15.0, oy_pa + ffl_h + 100.0, "A-FLOR")
            add_rect(ms, ox_pa + r_x2 - 15.0, oy_pa + ffl_h, ox_pa + r_x2, oy_pa + ffl_h + 100.0, "A-FLOR")

        # 4. Detail Plafond / Ceiling Structure (Elevasi +3400 mm)
        for c_x1, c_x2 in [(75.0, 2925.0), (3075.0, 5925.0)]:
            # Gypsum board 9 mm (double line)
            add_line(ms, ox_pa + c_x1, oy_pa + 3400.0, ox_pa + c_x2, oy_pa + 3400.0, "A-FLOR")
            add_line(ms, ox_pa + c_x1, oy_pa + 3409.0, ox_pa + c_x2, oy_pa + 3409.0, "A-FLOR")
            # Cornice list 50x50 mm
            add_polyline(ms, [[ox_pa + c_x1, oy_pa + 3400.0], [ox_pa + c_x1 + 50.0, oy_pa + 3400.0], [ox_pa + c_x1, oy_pa + 3350.0]], True, "A-FLOR")
            add_polyline(ms, [[ox_pa + c_x2, oy_pa + 3400.0], [ox_pa + c_x2 - 50.0, oy_pa + 3400.0], [ox_pa + c_x2, oy_pa + 3350.0]], True, "A-FLOR")
            # Hollow 40x40 mm main runners every 600 mm
            hx = ox_pa + c_x1 + 300.0
            step_idx = 0
            while hx < ox_pa + c_x2 - 100.0:
                add_rect(ms, hx - 20.0, oy_pa + 3409.0, hx + 20.0, oy_pa + 3449.0, "S-ROOF")
                add_rect(ms, hx - 10.0, oy_pa + 3449.0, hx + 10.0, oy_pa + 3489.0, "S-ROOF")
                # Hanger wire every 1200 mm
                if step_idx % 2 == 1:
                    add_line(ms, hx, oy_pa + 3489.0, hx, oy_pa + 3800.0, "S-ROOF")
                    # Turnbuckle clamp
                    add_rect(ms, hx - 10.0, oy_pa + 3620.0, hx + 10.0, oy_pa + 3660.0, "S-ROOF")
                hx += 600.0
                step_idx += 1

        # 5. Kuda-Kuda Baja Ringan Canal C75.75 & Penutup Atap Genteng Beton Flat
        # Bottom Chord C75x35 (double line 75 mm depth at +3800 to +3875)
        add_rect(ms, ox_pa + 0.0, oy_pa + 3800.0, ox_pa + 6000.0, oy_pa + 3875.0, "S-ROOF")

        # Top Chords C75x35 (Kemiringan 30 deg, tebal 75 mm)
        left_tc = [
            [ox_pa - 400.0, oy_pa + 3570.0],
            [ox_pa + 3000.0, oy_pa + 6500.0],
            [ox_pa + 3000.0, oy_pa + 6413.0],
            [ox_pa - 400.0, oy_pa + 3483.0]
        ]
        add_polyline(ms, left_tc, True, "S-ROOF")

        right_tc = [
            [ox_pa + 6400.0, oy_pa + 3570.0],
            [ox_pa + 3000.0, oy_pa + 6500.0],
            [ox_pa + 3000.0, oy_pa + 6413.0],
            [ox_pa + 6400.0, oy_pa + 3483.0]
        ]
        add_polyline(ms, right_tc, True, "S-ROOF")

        # King Post C75x35 vertikal di tengah (X=3000)
        add_rect(ms, ox_pa + 2962.5, oy_pa + 3875.0, ox_pa + 3037.5, oy_pa + 6413.0, "S-ROOF")

        # Diagonal Struts C75x35 (X=1000, 2000, 4000, 5000)
        for dx in [1000.0, 2000.0, 4000.0, 5000.0]:
            dist_c = abs(dx - 3000.0)
            dy_top = 6413.0 - dist_c * math.tan(math.radians(30))
            add_line(ms, ox_pa + dx - 25.0, oy_pa + 3875.0, ox_pa + dx - 25.0, oy_pa + dy_top, "S-ROOF")
            add_line(ms, ox_pa + dx + 25.0, oy_pa + 3875.0, ox_pa + dx + 25.0, oy_pa + dy_top, "S-ROOF")

        # Gusset Plates (Plat buhul simpul) & Baut Sekrup
        for nx, ny in [(3000.0, 6420.0), (3000.0, 3875.0), (0.0, 3800.0), (6000.0, 3800.0), (1500.0, 3875.0), (4500.0, 3875.0)]:
            add_circle(ms, ox_pa + nx, oy_pa + ny, 70.0, "S-ROOF")
            for sa in [0, 90, 180, 270]:
                srad = math.radians(sa)
                add_circle(ms, ox_pa + nx + 40.0 * math.cos(srad), oy_pa + ny + 40.0 * math.sin(srad), 6.0, "S-ROOF")

        # Reng U 32x0.45 mm & Genteng Beton Flat Bersusun setiap 300 mm
        hyp_len = math.hypot(3400.0, 2930.0)
        num_tiles = int(hyp_len / 290.0)
        cos30 = math.cos(math.radians(30))
        sin30 = math.sin(math.radians(30))

        # Left slope tiles & reng
        for i in range(num_tiles):
            dist_s = i * 290.0
            tx = ox_pa + 3000.0 - dist_s * cos30
            ty = oy_pa + 6500.0 - dist_s * sin30
            add_rect(ms, tx - 16.0, ty + 10.0, tx + 16.0, ty + 35.0, "S-ROOF")
            add_line(ms, tx - 40.0, ty + 40.0, tx + 260.0, ty + 40.0, "S-ROOF")
            add_line(ms, tx + 260.0, ty + 40.0, tx + 260.0, ty + 65.0, "S-ROOF")

        # Right slope tiles & reng
        for i in range(num_tiles):
            dist_s = i * 290.0
            tx = ox_pa + 3000.0 + dist_s * cos30
            ty = oy_pa + 6500.0 - dist_s * sin30
            add_rect(ms, tx - 16.0, ty + 10.0, tx + 16.0, ty + 35.0, "S-ROOF")
            add_line(ms, tx - 260.0, ty + 40.0, tx + 40.0, ty + 40.0, "S-ROOF")
            add_line(ms, tx - 260.0, ty + 40.0, tx - 260.0, ty + 65.0, "S-ROOF")

        # Nok Bubungan Flat di Puncak (+6500 mm)
        add_polyline(ms, [
            [ox_pa + 2700.0, oy_pa + 6520.0],
            [ox_pa + 3000.0, oy_pa + 6670.0],
            [ox_pa + 3300.0, oy_pa + 6520.0],
            [ox_pa + 3260.0, oy_pa + 6490.0],
            [ox_pa + 3000.0, oy_pa + 6620.0],
            [ox_pa + 2740.0, oy_pa + 6490.0]
        ], True, "S-ROOF")

        # Lisplank GRC Overstek di Eaves (Overhang 800 mm)
        add_rect(ms, ox_pa - 410.0, oy_pa + 3450.0, ox_pa - 390.0, oy_pa + 3650.0, "S-ROOF")
        add_rect(ms, ox_pa - 425.0, oy_pa + 3450.0, ox_pa - 410.0, oy_pa + 3550.0, "S-ROOF")
        add_rect(ms, ox_pa + 6390.0, oy_pa + 3450.0, ox_pa + 6410.0, oy_pa + 3650.0, "S-ROOF")
        add_rect(ms, ox_pa + 6410.0, oy_pa + 3450.0, ox_pa + 6425.0, oy_pa + 3550.0, "S-ROOF")

        # 6. Interior Door Elevation visible in section (Door D2 in master bed)
        add_rect(ms, ox_pa + 3075.0, oy_pa + 30.0, ox_pa + 3875.0, oy_pa + 2130.0, "A-DOOR")
        add_line(ms, ox_pa + 3800.0, oy_pa + 980.0, ox_pa + 3800.0, oy_pa + 1180.0, "A-DOOR")

        # 7. Material Specification Callouts with Architectural Leaders
        add_callout(ox_pa + 3000.0, oy_pa + 6650.0, "NOK GENTENG FLAT + ADUKAN 1:3 + FLASHBAND", -1200.0, 500.0)
        add_callout(ox_pa + 1800.0, oy_pa + 5800.0, "GENTENG BETON FLAT FINISH CAT (CISANGKAN)", -1200.0, 300.0)
        add_callout(ox_pa + 2200.0, oy_pa + 5200.0, "RENG BAJA RINGAN U 32x0.45 JARAK 30 CM", -1400.0, 0.0)
        add_callout(ox_pa + 1400.0, oy_pa + 4700.0, "KUDA-KUDA BAJA RINGAN CANAL C75.75", -1400.0, -200.0)
        add_callout(ox_pa + -400.0, oy_pa + 3550.0, "LISPLANK GRC GANDA 2x9 MM (LEBAR 20+10 CM)", -800.0, -300.0)
        add_callout(ox_pa + 0.0, oy_pa + 3700.0, "RING BALK BETON 15/20 (4 DIA 10, BEGEL DIA 6-150)", -1200.0, -400.0)
        add_callout(ox_pa + 1500.0, oy_pa + 3400.0, "PLAFOND GYPSUM 9 MM + HOLLOW GALVANIS 40x40", 1200.0, -300.0)
        add_callout(ox_pa + 1500.0, oy_pa + 3600.0, "PENGGANTUNG KAWAT DIA 4 MM + TURNBUCKLE", 1200.0, 0.0)
        add_callout(ox_pa + 6000.0, oy_pa + 2200.0, "DINDING BATA MERAH ADUKAN 1:4 PLESTER ACI", 800.0, 300.0)
        add_callout(ox_pa + 6000.0, oy_pa + 250.0, "DINDING TRASRAAM KEDAP AIR 1:2 (h=30 CM)", 800.0, 0.0)
        add_callout(ox_pa + 4500.0, oy_pa + 0.0, "LANTAI GRANIT TILE 60x60 CM + SPESI 3 CM", -1200.0, -500.0)
        add_callout(ox_pa + 6000.0, oy_pa + 0.0, "SLOOF BETON 15/20 (4 DIA 10, BEGEL DIA 6-150)", 800.0, -300.0)
        add_callout(ox_pa + 6000.0, oy_pa - 450.0, "PONDASI BATU KALI BELAH 1:4 (30/70 CM)", 800.0, -500.0)
        add_callout(ox_pa + 6000.0, oy_pa - 800.0, "AANSTAMPING / BATU KOSONG (t=20 CM)", 800.0, -700.0)
        add_callout(ox_pa + 6000.0, oy_pa - 950.0, "PASIR URUG PADAT (t=10 CM)", 800.0, -900.0)

        # Level markers
        add_level_marker(ox_pa - 800.0, oy_pa - 1000.0, "ELV. -1.00 DASAR GALIAN")
        add_level_marker(ox_pa - 800.0, oy_pa - 300.0, "ELV. -0.30 MUKA TANAH")
        add_level_marker(ox_pa - 800.0, oy_pa + 0.0, "ELV. ±0.00 MUKA LANTAI")
        add_level_marker(ox_pa - 800.0, oy_pa + 3400.0, "ELV. +3.40 PLAFOND")
        add_level_marker(ox_pa - 800.0, oy_pa + 3600.0, "ELV. +3.60 RING BALK")
        add_level_marker(ox_pa - 800.0, oy_pa + 6500.0, "ELV. +6.50 BUBUNGAN")

        # Dimensions
        add_dim(ox_pa + 0.0, oy_pa + 0.0, ox_pa + 3000.0, oy_pa + 0.0, ox_pa + 1500.0, oy_pa - 1400.0, 0.0, "A-DIMS-EXT", "3000")
        add_dim(ox_pa + 3000.0, oy_pa + 0.0, ox_pa + 6000.0, oy_pa + 0.0, ox_pa + 4500.0, oy_pa - 1400.0, 0.0, "A-DIMS-EXT", "3000")
        add_dim(ox_pa + 0.0, oy_pa + 0.0, ox_pa + 6000.0, oy_pa + 0.0, ox_pa + 3000.0, oy_pa - 2200.0, 0.0, "A-DIMS-EXT", "6000")

        add_title_block(ox_pa + 3000.0, oy_pa - 3200.0, "POTONGAN A - A", "1 : 100", "08")

        # =============================================================
        # DRAWING 9: POTONGAN B-B (Longitudinal Section) - Origin X=16000, Y=34000
        # Skala 1 : 100 - High Precision Detailing
        # =============================================================
        print("Drafting Drawing 9: Potongan B-B (High-Precision Detailed)...")
        ox_pb = 16000.0
        oy_pb = 34000.0

        # Ground line (15m length)
        add_line(ms, ox_pb - 1000.0, oy_pb - 300.0, ox_pb + 15000.0, oy_pb - 300.0, "A-FLOR")

        # Step Floor Profiles
        # Carport (-0.20)
        add_rect(ms, ox_pb + 0.0, oy_pb - 200.0, ox_pb + 3000.0, oy_pb - 150.0, "A-FLOR")
        # Teras (-0.05)
        add_rect(ms, ox_pb + 3000.0, oy_pb - 50.0, ox_pb + 4500.0, oy_pb - 10.0, "A-FLOR")
        # R. Tamu & R. Makan (±0.00)
        add_rect(ms, ox_pb + 4500.0, oy_pb - 10.0, ox_pb + 10000.0, oy_pb + 0.0, "A-FLOR")
        # Dapur (-0.02)
        add_rect(ms, ox_pb + 10000.0, oy_pb - 30.0, ox_pb + 12500.0, oy_pb - 20.0, "A-FLOR")
        # Taman Belakang (-0.15)
        add_rect(ms, ox_pb + 12500.0, oy_pb - 160.0, ox_pb + 14000.0, oy_pb - 150.0, "A-FLOR")

        # Foundations at cross axes Y=4500 (Front wall), Y=12500 (Rear wall), Y=14000 (Boundary wall)
        for fy in [4500.0, 12500.0, 14000.0]:
            # Pasir urug
            pu_pl = add_rect(ms, ox_pb + fy - 500.0, oy_pb - 1000.0, ox_pb + fy + 500.0, oy_pb - 900.0, "S-FNDN")
            add_hatch_to(ms, pu_pl, "AR-SAND", 10.0, "S-FNDN")

            # Aanstamping
            as_pl = add_rect(ms, ox_pb + fy - 450.0, oy_pb - 900.0, ox_pb + fy + 450.0, oy_pb - 700.0, "S-FNDN")
            add_hatch_to(ms, as_pl, "GRAVEL", 20.0, "S-FNDN")

            # Stone Footing
            f_pts = [
                [ox_pb + fy - 150.0, oy_pb - 100.0],
                [ox_pb + fy + 150.0, oy_pb - 100.0],
                [ox_pb + fy + 350.0, oy_pb - 700.0],
                [ox_pb + fy - 350.0, oy_pb - 700.0]
            ]
            f_pl = add_polyline(ms, f_pts, True, "S-FNDN")
            add_hatch_to(ms, f_pl, "GRAVEL", 15.0, "S-FNDN")

            # Sloof 150x200 mm with rebars
            sl_pl = add_rect(ms, ox_pb + fy - 75.0, oy_pb - 100.0, ox_pb + fy + 75.0, oy_pb + 100.0, "S-FNDN")
            add_rect(ms, ox_pb + fy - 50.0, oy_pb - 75.0, ox_pb + fy + 50.0, oy_pb + 75.0, "S-FNDN")
            for bx, by in [(-45.0, -70.0), (45.0, -70.0), (45.0, 70.0), (-45.0, 70.0)]:
                bc = add_circle(ms, ox_pb + fy + bx, oy_pb + by, 5.0, "S-FNDN")
                add_hatch_to(ms, bc, "SOLID", 1.0, "S-FNDN")

            # Trasraam 300 mm
            tr_pl = add_rect(ms, ox_pb + fy - 75.0, oy_pb + 100.0, ox_pb + fy + 75.0, oy_pb + 400.0, "A-WALL-FULL")
            add_hatch_to(ms, tr_pl, "ANSI31", 6.0, "A-WALL-PATS")

            # Cut wall & Ringbalk
            top_w = 2500.0 if fy == 14000.0 else 3600.0
            cw_pl = add_rect(ms, ox_pb + fy - 75.0, oy_pb + 400.0, ox_pb + fy + 75.0, oy_pb + top_w, "A-WALL-FULL")
            add_hatch_to(ms, cw_pl, "ANSI31", 12.0, "A-WALL-PATS")

            if fy != 14000.0:
                rb_pl = add_rect(ms, ox_pb + fy - 75.0, oy_pb + 3600.0, ox_pb + fy + 75.0, oy_pb + 3800.0, "S-FNDN")
                add_rect(ms, ox_pb + fy - 50.0, oy_pb + 3625.0, ox_pb + fy + 50.0, oy_pb + 3775.0, "S-FNDN")
                for bx, by in [(-45.0, 3630.0), (45.0, 3630.0), (45.0, 3770.0), (-45.0, 3770.0)]:
                    rbc = add_circle(ms, ox_pb + fy + bx, oy_pb + by, 5.0, "S-FNDN")
                    add_hatch_to(ms, rbc, "SOLID", 1.0, "S-FNDN")

        # Longitudinal Roof Profile with Purlins (Gording) & Trusses
        add_polyline(ms, [
            [ox_pb + 4500.0, oy_pb + 3800.0],
            [ox_pb + 8500.0, oy_pb + 6500.0],
            [ox_pb + 12500.0, oy_pb + 3800.0],
            [ox_pb + 4500.0, oy_pb + 3800.0]
        ], True, "S-ROOF")

        # Longitudinal Gording Canal C75 ganda at slope intervals
        for gy in [5500.0, 6500.0, 7500.0, 8500.0, 9500.0, 10500.0, 11500.0]:
            dist_apex = abs(gy - 8500.0)
            g_height = 6500.0 - dist_apex * math.tan(math.radians(30))
            add_rect(ms, ox_pb + gy - 35.0, oy_pb + g_height - 75.0, ox_pb + gy + 35.0, oy_pb + g_height, "S-ROOF")

        # Longitudinal Ceiling Structure (Gypsum 9 mm + Hollow runners every 600 mm)
        add_line(ms, ox_pb + 4575.0, oy_pb + 3400.0, ox_pb + 12425.0, oy_pb + 3400.0, "A-FLOR")
        add_line(ms, ox_pb + 4575.0, oy_pb + 3409.0, ox_pb + 12425.0, oy_pb + 3409.0, "A-FLOR")
        cy_step = ox_pb + 4575.0 + 300.0
        while cy_step < ox_pb + 12425.0:
            add_rect(ms, cy_step - 20.0, oy_pb + 3409.0, cy_step + 20.0, oy_pb + 3449.0, "S-ROOF")
            add_line(ms, cy_step, oy_pb + 3449.0, cy_step, oy_pb + 3800.0, "S-ROOF")
            cy_step += 600.0

        # Interior Elevations in Section:
        # Door D1 elevation at Y=4500
        add_rect(ms, ox_pb + 4575.0, oy_pb + 0.0, ox_pb + 5475.0, oy_pb + 2100.0, "A-DOOR")
        add_line(ms, ox_pb + 5350.0, oy_pb + 950.0, ox_pb + 5350.0, oy_pb + 1150.0, "A-DOOR")

        # Kitchen Counter & Splashback (Y: 11950 to 12425)
        add_rect(ms, ox_pb + 11950.0, oy_pb - 20.0, ox_pb + 12425.0, oy_pb + 850.0, "A-FURN")
        add_rect(ms, ox_pb + 11950.0, oy_pb + 850.0, ox_pb + 12425.0, oy_pb + 1450.0, "A-FLOR")
        add_line(ms, ox_pb + 11950.0, oy_pb + 1150.0, ox_pb + 12425.0, oy_pb + 1150.0, "A-FLOR")

        # Service Door D4 at Y=12500
        add_rect(ms, ox_pb + 11500.0, oy_pb - 20.0, ox_pb + 12300.0, oy_pb + 2080.0, "A-DOOR")

        # Carport Canopy (Y: 0 to 4500, h=3200)
        add_rect(ms, ox_pb + 0.0, oy_pb - 200.0, ox_pb + 150.0, oy_pb + 3000.0, "A-ELEV")
        add_line(ms, ox_pb + 0.0, oy_pb + 3000.0, ox_pb + 4500.0, oy_pb + 3300.0, "A-ELEV")

        # Room names in section
        add_text(ms, "CARPORT", ox_pb + 1500.0, oy_pb + 500.0, 180.0, "A-ANNO-TEXT")
        add_text(ms, "TERAS", ox_pb + 3750.0, oy_pb + 500.0, 180.0, "A-ANNO-TEXT")
        add_text(ms, "R. TAMU & KELUARGA", ox_pb + 6250.0, oy_pb + 500.0, 180.0, "A-ANNO-TEXT")
        add_text(ms, "R. MAKAN", ox_pb + 8500.0, oy_pb + 500.0, 180.0, "A-ANNO-TEXT")
        add_text(ms, "DAPUR", ox_pb + 11250.0, oy_pb + 500.0, 180.0, "A-ANNO-TEXT")
        add_text(ms, "TAMAN BELAKANG", ox_pb + 13250.0, oy_pb + 500.0, 180.0, "A-ANNO-TEXT")

        # Level markers
        add_level_marker(ox_pb - 600.0, oy_pb - 300.0, "ELV. -0.30")
        add_level_marker(ox_pb - 600.0, oy_pb + 0.0, "ELV. ±0.00")
        add_level_marker(ox_pb - 600.0, oy_pb + 3400.0, "ELV. +3.40")
        add_level_marker(ox_pb - 600.0, oy_pb + 6500.0, "ELV. +6.50")

        # Dimensions
        add_dim(ox_pb + 0.0, oy_pb + 0.0, ox_pb + 4500.0, oy_pb + 0.0, ox_pb + 2250.0, oy_pb - 1400.0, 0.0, "A-DIMS-EXT", "4500")
        add_dim(ox_pb + 4500.0, oy_pb + 0.0, ox_pb + 12500.0, oy_pb + 0.0, ox_pb + 8500.0, oy_pb - 1400.0, 0.0, "A-DIMS-EXT", "8000")
        add_dim(ox_pb + 12500.0, oy_pb + 0.0, ox_pb + 14000.0, oy_pb + 0.0, ox_pb + 13250.0, oy_pb - 1400.0, 0.0, "A-DIMS-EXT", "1500")
        add_dim(ox_pb + 0.0, oy_pb + 0.0, ox_pb + 14000.0, oy_pb + 0.0, ox_pb + 7000.0, oy_pb - 2200.0, 0.0, "A-DIMS-EXT", "14000")

        add_title_block(ox_pb + 7000.0, oy_pb - 3200.0, "POTONGAN B - B", "1 : 100", "09")

        # =============================================================
        # DRAWING 10: DETAIL 1 - DETAIL PONDASI & SLOOF (Origin X=48000, Y=34000)
        # Skala 1 : 20 - Construction Detail
        # =============================================================
        print("Drafting Drawing 10: Detail 1 (Pondasi & Sloof 1:20)...")
        ox_d1 = 48000.0
        oy_d1 = 34000.0

        def add_callout_20(px, py, text, off_x=500.0, off_y=250.0, layer="A-ANNO-TEXT"):
            ex = px + off_x
            ey = py + off_y
            shelf = 350.0 if off_x >= 0 else -350.0
            add_line(ms, px, py, ex, ey, "A-ANNO-SYMB")
            add_line(ms, ex, ey, ex + shelf, ey, "A-ANNO-SYMB")
            add_circle(ms, px, py, 15.0, "A-ANNO-SYMB")
            align = 9 if off_x >= 0 else 11
            tx = ex + (40.0 if off_x >= 0 else -40.0)
            add_text(ms, text, tx, ey + 40.0, 100.0, layer, align=align)

        # Border Box Detail 1
        add_rect(ms, ox_d1 - 1800.0, oy_d1 - 1800.0, ox_d1 + 2200.0, oy_d1 + 1800.0, "A-ANNO-TEXT")

        # Ground line
        add_line(ms, ox_d1 - 1500.0, oy_d1 - 300.0, ox_d1 + 1500.0, oy_d1 - 300.0, "A-FLOR")

        # Excavation slope
        add_polyline(ms, [
            [ox_d1 - 800.0, oy_d1 - 300.0],
            [ox_d1 - 600.0, oy_d1 - 1000.0],
            [ox_d1 + 600.0, oy_d1 - 1000.0],
            [ox_d1 + 800.0, oy_d1 - 300.0]
        ], False, "S-FNDN")

        # Pasir Urug 100 mm (ox_d1 - 500 to +500, Y: -1000 to -900)
        pu1 = add_rect(ms, ox_d1 - 500.0, oy_d1 - 1000.0, ox_d1 + 500.0, oy_d1 - 900.0, "S-FNDN")
        add_hatch_to(ms, pu1, "AR-SAND", 5.0, "S-FNDN")

        # Aanstamping 200 mm with packed stones (ox_d1 - 450 to +450, Y: -900 to -700)
        as1 = add_rect(ms, ox_d1 - 450.0, oy_d1 - 900.0, ox_d1 + 450.0, oy_d1 - 700.0, "S-FNDN")
        add_hatch_to(ms, as1, "GRAVEL", 10.0, "S-FNDN")
        for sx in [-380.0, -280.0, -180.0, -80.0, 20.0, 120.0, 220.0, 320.0]:
            add_circle(ms, ox_d1 + sx, oy_d1 - 800.0, 42.0, "S-FNDN")

        # Pondasi Batu Kali 300/700x600 mm (Y: -700 to -100)
        f_d1_pts = [
            [ox_d1 - 150.0, oy_d1 - 100.0],
            [ox_d1 + 150.0, oy_d1 - 100.0],
            [ox_d1 + 350.0, oy_d1 - 700.0],
            [ox_d1 - 350.0, oy_d1 - 700.0]
        ]
        f_d1 = add_polyline(ms, f_d1_pts, True, "S-FNDN")
        add_hatch_to(ms, f_d1, "GRAVEL", 8.0, "S-FNDN")

        # Sloof Beton Bertulang 150x200 mm (Y: -100 to +100)
        add_rect(ms, ox_d1 - 75.0, oy_d1 - 100.0, ox_d1 + 75.0, oy_d1 + 100.0, "S-FNDN")
        add_rect(ms, ox_d1 - 50.0, oy_d1 - 75.0, ox_d1 + 50.0, oy_d1 + 75.0, "S-FNDN")
        for bx, by in [(-45.0, -70.0), (45.0, -70.0), (45.0, 70.0), (-45.0, 70.0)]:
            c_rb = add_circle(ms, ox_d1 + bx, oy_d1 + by, 5.0, "S-FNDN")
            add_hatch_to(ms, c_rb, "SOLID", 1.0, "S-FNDN")

        # Trasraam 1:2 (Y: +100 to +400, h=300 mm)
        tr1 = add_rect(ms, ox_d1 - 75.0, oy_d1 + 100.0, ox_d1 + 75.0, oy_d1 + 400.0, "A-WALL-FULL")
        add_hatch_to(ms, tr1, "ANSI31", 5.0, "A-WALL-PATS")

        # Dinding Bata 150 mm (Y: +400 to +900)
        bw1 = add_rect(ms, ox_d1 - 75.0, oy_d1 + 400.0, ox_d1 + 75.0, oy_d1 + 900.0, "A-WALL-FULL")
        add_hatch_to(ms, bw1, "ANSI31", 8.0, "A-WALL-PATS")
        add_line(ms, ox_d1 - 55.0, oy_d1 + 400.0, ox_d1 - 55.0, oy_d1 + 900.0, "A-WALL-FULL")
        add_line(ms, ox_d1 + 55.0, oy_d1 + 400.0, ox_d1 + 55.0, oy_d1 + 900.0, "A-WALL-FULL")

        # Floor layers at right side (X: +75 to +1100)
        add_rect(ms, ox_d1 + 75.0, oy_d1 - 100.0, ox_d1 + 1100.0, oy_d1 - 50.0, "A-FLOR")
        add_rect(ms, ox_d1 + 75.0, oy_d1 - 50.0, ox_d1 + 1100.0, oy_d1 - 10.0, "A-FLOR")
        add_rect(ms, ox_d1 + 75.0, oy_d1 - 10.0, ox_d1 + 1100.0, oy_d1 + 0.0, "A-FLOR")
        # Plin 100mm
        add_rect(ms, ox_d1 + 75.0, oy_d1 + 0.0, ox_d1 + 90.0, oy_d1 + 100.0, "A-FLOR")

        # Callouts Detail 1
        add_callout_20(ox_d1 + 75.0, oy_d1 + 600.0, "DINDING BATA MERAH ADUKAN 1:4", 300.0, 150.0)
        add_callout_20(ox_d1 + 75.0, oy_d1 + 250.0, "DINDING TRASRAAM KEDAP AIR 1:2 (h=30 CM)", 300.0, 100.0)
        add_callout_20(ox_d1 + 100.0, oy_d1 + 50.0, "PLIN GRANIT 10 CM + HOMOGENEOUS TILE 60x60 CM", 300.0, 50.0)
        add_callout_20(ox_d1 + 75.0, oy_d1 + 0.0, "SLOOF BETON 150x200 MM (4 DIA 10, BEGEL DIA 6-150)", -450.0, 150.0)
        add_callout_20(ox_d1 - 50.0, oy_d1 - 50.0, "SELIMUT BETON (CONCRETE COVER) = 25 MM", -450.0, 50.0)
        add_callout_20(ox_d1 - 200.0, oy_d1 - 400.0, "PONDASI BATU KALI BELAH (1:4)", -450.0, -100.0)
        add_callout_20(ox_d1 - 300.0, oy_d1 - 800.0, "AANSTAMPING / BATU KOSONG (t=20 CM)", -450.0, -150.0)
        add_callout_20(ox_d1 - 300.0, oy_d1 - 950.0, "PASIR URUG PADAT (t=10 CM)", -450.0, -200.0)

        add_title_block(ox_d1 + 200.0, oy_d1 - 1450.0, "DETAIL 1 : PONDASI & SLOOF", "1 : 20", "10")

        # =============================================================
        # DRAWING 11: DETAIL 2 - RINGBALK, KUDA-KUDA & PLAFOND (Origin X=48000, Y=22000)
        # Skala 1 : 20 - Construction Detail
        # =============================================================
        print("Drafting Drawing 11: Detail 2 (Ringbalk, Kuda-kuda & Plafond 1:20)...")
        ox_d2 = 48000.0
        oy_d2 = 22000.0

        add_rect(ms, ox_d2 - 1800.0, oy_d2 - 1800.0, ox_d2 + 2200.0, oy_d2 + 1800.0, "A-ANNO-TEXT")

        # Wall cut (X: -75 to +75, Y: -600 to 0)
        add_rect(ms, ox_d2 - 75.0, oy_d2 - 600.0, ox_d2 + 75.0, oy_d2 + 0.0, "A-WALL-FULL")
        # Ringbalk 150x200 mm (Y: 0 to +200)
        add_rect(ms, ox_d2 - 75.0, oy_d2 + 0.0, ox_d2 + 75.0, oy_d2 + 200.0, "S-FNDN")
        add_rect(ms, ox_d2 - 50.0, oy_d2 + 25.0, ox_d2 + 50.0, oy_d2 + 175.0, "S-FNDN")
        for bx, by in [(-45.0, 30.0), (45.0, 30.0), (45.0, 170.0), (-45.0, 170.0)]:
            c_rb = add_circle(ms, ox_d2 + bx, oy_d2 + by, 5.0, "S-FNDN")
            add_hatch_to(ms, c_rb, "SOLID", 1.0, "S-FNDN")

        # Steel anchor bolt Dia 12 mm
        add_line(ms, ox_d2, oy_d2 + 20.0, ox_d2, oy_d2 + 250.0, "S-ROOF")
        add_rect(ms, ox_d2 - 15.0, oy_d2 + 215.0, ox_d2 + 15.0, oy_d2 + 230.0, "S-ROOF")

        # Light gauge steel base bracket & bottom chord C75x35
        add_rect(ms, ox_d2 - 75.0, oy_d2 + 200.0, ox_d2 + 800.0, oy_d2 + 275.0, "S-ROOF")

        # Sloped Top Chord C75x35 (30 deg)
        tc_d2 = [
            [ox_d2 - 500.0, oy_d2 + 20.0],
            [ox_d2 + 800.0, oy_d2 + 770.0],
            [ox_d2 + 800.0, oy_d2 + 695.0],
            [ox_d2 - 500.0, oy_d2 - 55.0]
        ]
        add_polyline(ms, tc_d2, True, "S-ROOF")

        # Gusset plate with screws
        add_circle(ms, ox_d2 + 50.0, oy_d2 + 280.0, 60.0, "S-ROOF")
        for sa in [0, 90, 180, 270]:
            srad = math.radians(sa)
            add_circle(ms, ox_d2 + 50.0 + 35.0 * math.cos(srad), oy_d2 + 280.0 + 35.0 * math.sin(srad), 5.0, "S-ROOF")

        # Lisplank GRC ganda (200x9 and 100x9 mm) at eaves
        add_rect(ms, ox_d2 - 510.0, oy_d2 - 100.0, ox_d2 - 490.0, oy_d2 + 100.0, "S-ROOF")
        add_rect(ms, ox_d2 - 525.0, oy_d2 - 100.0, ox_d2 - 510.0, oy_d2 + 0.0, "S-ROOF")

        # Battens (Reng U 32x0.45 mm) & Flat Tiles
        for r_i in range(4):
            rx = ox_d2 - 350.0 + r_i * 260.0 * math.cos(math.radians(30))
            ry = oy_d2 + 30.0 + r_i * 260.0 * math.sin(math.radians(30))
            add_rect(ms, rx - 16.0, ry + 30.0, rx + 16.0, ry + 55.0, "S-ROOF")
            add_line(ms, rx - 30.0, ry + 60.0, rx + 240.0, ry + 60.0, "S-ROOF")
            add_line(ms, rx + 240.0, ry + 60.0, rx + 240.0, ry + 85.0, "S-ROOF")

        # Ceiling Structure (Gypsum 9 mm + Hollow 40x40 + Cornice)
        add_line(ms, ox_d2 + 75.0, oy_d2 - 200.0, ox_d2 + 1100.0, oy_d2 - 200.0, "A-FLOR")
        add_line(ms, ox_d2 + 75.0, oy_d2 - 191.0, ox_d2 + 1100.0, oy_d2 - 191.0, "A-FLOR")
        # Cornice 50x50 mm
        add_polyline(ms, [[ox_d2 + 75.0, oy_d2 - 200.0], [ox_d2 + 125.0, oy_d2 - 200.0], [ox_d2 + 75.0, oy_d2 - 250.0]], True, "A-FLOR")
        # Hollow 40x40 main runner
        add_rect(ms, ox_d2 + 250.0, oy_d2 - 191.0, ox_d2 + 290.0, oy_d2 - 151.0, "S-ROOF")
        add_rect(ms, ox_d2 + 650.0, oy_d2 - 191.0, ox_d2 + 690.0, oy_d2 - 151.0, "S-ROOF")
        # Hanger wire with turnbuckle
        add_line(ms, ox_d2 + 270.0, oy_d2 - 151.0, ox_d2 + 270.0, oy_d2 + 200.0, "S-ROOF")
        add_rect(ms, ox_d2 + 260.0, oy_d2 - 30.0, ox_d2 + 280.0, oy_d2 + 10.0, "S-ROOF")

        # Callouts Detail 2
        add_callout_20(ox_d2 - 500.0, oy_d2 + 50.0, "LISPLANK GRC GANDA 2x9 MM (LEBAR 20+10 CM)", -400.0, 100.0)
        add_callout_20(ox_d2 - 200.0, oy_d2 + 250.0, "GENTENG BETON FLAT + RENG U 32x0.45", -400.0, 200.0)
        add_callout_20(ox_d2 + 50.0, oy_d2 + 350.0, "PLAT BUHUL GUSSET + 6 SEKRUP SELF-DRILLING", -400.0, 300.0)
        add_callout_20(ox_d2 + 0.0, oy_d2 + 150.0, "RING BALK BETON 15/20 (4 DIA 10, BEGEL DIA 6-150)", -400.0, -100.0)
        add_callout_20(ox_d2, oy_d2 + 215.0, "ANGKUR BAJA DIA 12 MM L=30 CM", -400.0, -200.0)
        add_callout_20(ox_d2 + 125.0, oy_d2 - 200.0, "LIST CORNICE GYPSUM 50x50 MM", 350.0, -150.0)
        add_callout_20(ox_d2 + 270.0, oy_d2 - 191.0, "RANGKA HOLLOW GALVANIS 40x40 MM", 350.0, -80.0)
        add_callout_20(ox_d2 + 270.0, oy_d2 + 50.0, "PENGGANTUNG KAWAT DIA 4 MM + TURNBUCKLE", 350.0, 50.0)
        add_callout_20(ox_d2 + 400.0, oy_d2 + 250.0, "KUDA-KUDA BAJA RINGAN CANAL C75.75", 350.0, 150.0)

        add_title_block(ox_d2 + 200.0, oy_d2 - 1450.0, "DETAIL 2 : RINGBALK & PLAFOND", "1 : 20", "11")

        # =============================================================
        # DRAWING 12: DETAIL 3 - BUBUNGAN / NOK ATAP (Origin X=48000, Y=10000)
        # Skala 1 : 20 - Construction Detail
        # =============================================================
        print("Drafting Drawing 12: Detail 3 (Nok Bubungan Atap 1:20)...")
        ox_d3 = 48000.0
        oy_d3 = 10000.0

        add_rect(ms, ox_d3 - 1800.0, oy_d3 - 1800.0, ox_d3 + 2200.0, oy_d3 + 1800.0, "A-ANNO-TEXT")

        # Apex meeting of Left & Right Top Chords C75x35
        left_tc_d3 = [
            [ox_d3 - 900.0, oy_d3 - 520.0],
            [ox_d3, oy_d3],
            [ox_d3, oy_d3 - 87.0],
            [ox_d3 - 900.0, oy_d3 - 607.0]
        ]
        add_polyline(ms, left_tc_d3, True, "S-ROOF")

        right_tc_d3 = [
            [ox_d3 + 900.0, oy_d3 - 520.0],
            [ox_d3, oy_d3],
            [ox_d3, oy_d3 - 87.0],
            [ox_d3 + 900.0, oy_d3 - 607.0]
        ]
        add_polyline(ms, right_tc_d3, True, "S-ROOF")

        # King post below apex
        add_rect(ms, ox_d3 - 37.5, oy_d3 - 800.0, ox_d3 + 37.5, oy_d3 - 87.0, "S-ROOF")

        # Apex Gusset Plate
        add_circle(ms, ox_d3, oy_d3 - 50.0, 80.0, "S-ROOF")
        for sa in [0, 60, 120, 180, 240, 300]:
            srad = math.radians(sa)
            add_circle(ms, ox_d3 + 50.0 * math.cos(srad), oy_d3 - 50.0 + 50.0 * math.sin(srad), 6.0, "S-ROOF")

        # Purlins (Gording nok ganda C75)
        add_rect(ms, ox_d3 - 80.0, oy_d3 + 10.0, ox_d3 - 5.0, oy_d3 + 85.0, "S-ROOF")
        add_rect(ms, ox_d3 + 5.0, oy_d3 + 10.0, ox_d3 + 80.0, oy_d3 + 85.0, "S-ROOF")

        # Ridge board (papan nok 20x100 mm)
        add_rect(ms, ox_d3 - 10.0, oy_d3 + 85.0, ox_d3 + 10.0, oy_d3 + 185.0, "S-ROOF")

        # Waterproofing Flashband membrane
        add_polyline(ms, [
            [ox_d3 - 350.0, oy_d3 - 80.0],
            [ox_d3 - 100.0, oy_d3 + 80.0],
            [ox_d3, oy_d3 + 195.0],
            [ox_d3 + 100.0, oy_d3 + 80.0],
            [ox_d3 + 350.0, oy_d3 - 80.0]
        ], False, "S-ROOF")

        # Cement Mortar Bed (Adukan 1:3 pengikat nok)
        mortar_pts = [
            [ox_d3 - 280.0, oy_d3 - 40.0],
            [ox_d3 - 80.0, oy_d3 + 90.0],
            [ox_d3, oy_d3 + 180.0],
            [ox_d3 + 80.0, oy_d3 + 90.0],
            [ox_d3 + 280.0, oy_d3 - 40.0],
            [ox_d3 + 220.0, oy_d3 - 60.0],
            [ox_d3, oy_d3 + 100.0],
            [ox_d3 - 220.0, oy_d3 - 60.0]
        ]
        m_pl = add_polyline(ms, mortar_pts, True, "S-ROOF")
        add_hatch_to(ms, m_pl, "AR-SAND", 4.0, "S-ROOF")

        # Ridge Cap Tile (Genteng Bubungan Beton Flat)
        ridge_pts = [
            [ox_d3 - 300.0, oy_d3 + 0.0],
            [ox_d3 - 120.0, oy_d3 + 140.0],
            [ox_d3, oy_d3 + 230.0],
            [ox_d3 + 120.0, oy_d3 + 140.0],
            [ox_d3 + 300.0, oy_d3 + 0.0],
            [ox_d3 + 280.0, oy_d3 - 25.0],
            [ox_d3, oy_d3 + 190.0],
            [ox_d3 - 280.0, oy_d3 - 25.0]
        ]
        add_polyline(ms, ridge_pts, True, "S-ROOF")

        # Stepped tiles entering underneath
        for side in [-1, 1]:
            for st_i in range(3):
                tx = ox_d3 + side * (260.0 + st_i * 240.0 * math.cos(math.radians(30)))
                ty = oy_d3 - 30.0 - st_i * 240.0 * math.sin(math.radians(30))
                add_rect(ms, tx - 16.0, ty + 20.0, tx + 16.0, ty + 45.0, "S-ROOF")
                add_line(ms, tx - 30.0 * side, ty + 50.0, tx + 200.0 * side, ty + 50.0, "S-ROOF")

        # Callouts Detail 3
        add_callout_20(ox_d3, oy_d3 + 220.0, "GENTENG BUBUNGAN / NOK BETON FLAT", 350.0, 150.0)
        add_callout_20(ox_d3, oy_d3 + 160.0, "PAPAN DREMPEL NOK 20x100 MM", 350.0, 80.0)
        add_callout_20(ox_d3 + 100.0, oy_d3 + 90.0, "ADUKAN PEREKAT SEMEN 1 PC : 3 PS", 350.0, 0.0)
        add_callout_20(ox_d3 + 200.0, oy_d3 + 20.0, "LEMBARAN WATERPROOFING FLASHBAND", 350.0, -80.0)
        add_callout_20(ox_d3 + 40.0, oy_d3 + 45.0, "GORDING NOK BAJA RINGAN C75 GANDA", -400.0, 150.0)
        add_callout_20(ox_d3, oy_d3 - 50.0, "PLAT BUHUL APEX + SEKRUP SELF-DRILLING", -400.0, 50.0)
        add_callout_20(ox_d3 - 400.0, oy_d3 - 250.0, "TOP CHORD BAJA RINGAN CANAL C75.75", -400.0, -80.0)
        add_callout_20(ox_d3, oy_d3 - 400.0, "KING POST BAJA RINGAN C75.75", -400.0, -180.0)

        add_title_block(ox_d3 + 200.0, oy_d3 - 1450.0, "DETAIL 3 : BUBUNGAN / NOK ATAP", "1 : 20", "12")

        # -------------------------------------------------------------
        # 10. REGENERATE & ZOOM EXTENTS
        # -------------------------------------------------------------
        print("Regenerating drawing and fitting zoom extents...")
        try:
            doc.SendCommand("UCSICON Noorigin\n")
            doc.Regen(1)
            app.ZoomExtents()
        except Exception:
            pass

        print("=" * 80)
        print("COMPLETE ARCHITECTURAL & MEP PRODUCTION DRAWINGS GENERATED SUCCESSFULLY!")
        print(f"Total ModelSpace Entities: {ms.Count}")
        print(f"Total Block Definitions in Library: {doc.Blocks.Count}")
        print("=" * 80)


if __name__ == "__main__":
    import threading
    t = threading.Thread(target=run)
    t.start()
    t.join()
