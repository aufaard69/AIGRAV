"""
patch_and_run_details.py
Updates generate_mep_and_architectural_set.py with high-precision detailing for:
1. Potongan A-A (Melintang) - Full foundations, aanstamping, sloof rebars, trasraam, ceiling hollow & hangers, truss C75.75, tiles, callouts.
2. Potongan B-B (Memanjang) - Step floors, foundations, sloof, purlins, ceiling, interior doors, kitchen counter, canopy.
3. Detail 1: Detail Pondasi & Sloof (Skala 1:20) at (48000, 34000).
4. Detail 2: Detail Ringbalk, Kuda-kuda & Plafond (Skala 1:20) at (48000, 22000).
5. Detail 3: Detail Bubungan / Nok Atap (Skala 1:20) at (48000, 10000).
Then executes the updated generator in ZWCAD 2023.
"""

from pathlib import Path
import re
import sys
import subprocess

TARGET_FILE = Path(r"C:\AIGRAV\generate_mep_and_architectural_set.py")
content = TARGET_FILE.read_text(encoding="utf-8")

# Define the start and end anchors for the replacement
START_MARKER = '        # =============================================================\n        # DRAWING 8: POTONGAN A-A (Transverse Section) - Origin X=0, Y=34000'
END_MARKER = '        # -------------------------------------------------------------\n        # 10. REGENERATE & ZOOM EXTENTS'

start_pos = content.find(START_MARKER)
end_pos = content.find(END_MARKER)

if start_pos == -1 or end_pos == -1:
    print(f"Error: Markers not found! start_pos={start_pos}, end_pos={end_pos}")
    sys.exit(1)

NEW_SECTIONS_CODE = '''        # =============================================================
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
            tr_pl = add_rect(ms, ox_pa + fx - 75.0, oy_pa + 100.0, ox_pa + fx + 400.0, "A-WALL-FULL")
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
'''

# Perform replacement
new_content = content[:start_pos] + NEW_SECTIONS_CODE + "\n" + content[end_pos:]
TARGET_FILE.write_text(new_content, encoding="utf-8")
print("Successfully patched generate_mep_and_architectural_set.py!")
print(f"Old size: {len(content)} chars -> New size: {len(new_content)} chars")
