"""
Generate Authentic High-Precision CAD/BIM Detail Engineering Section Drawing:
Burj Khalifa Tower Peak & Telescopic Spire (+567.30m to +828.00m)
Standard A1 Landscape Sheet (3840 x 2400 px, 300 DPI equivalent)
"""

import math
import os
from PIL import Image, ImageDraw, ImageFont

def create_spire_engineering_drawing(output_path):
    # 1. Canvas Setup: 3840 x 2400 (Standard 16:10 / ISO A1 proportion)
    W, H = 3840, 2400
    img = Image.new("RGB", (W, H), (255, 255, 255))
    draw = ImageDraw.Draw(img)

    # 2. Fonts Resolution
    font_path = "C:\\Windows\\Fonts\\arial.ttf"
    font_bold_path = "C:\\Windows\\Fonts\\arialbd.ttf"
    font_mono_path = "C:\\Windows\\Fonts\\consola.ttf"

    f_title = ImageFont.truetype(font_bold_path, 34)
    f_sub = ImageFont.truetype(font_bold_path, 24)
    f_header = ImageFont.truetype(font_bold_path, 20)
    f_body = ImageFont.truetype(font_path, 16)
    f_body_bold = ImageFont.truetype(font_bold_path, 16)
    f_dim = ImageFont.truetype(font_mono_path, 15)
    f_small = ImageFont.truetype(font_path, 13)
    f_micro = ImageFont.truetype(font_path, 11)

    # CAD Colors
    C_BORDER = (40, 45, 55)
    C_GRID = (160, 165, 175)
    C_STEEL = (30, 35, 45)
    C_CONCRETE = (110, 115, 120)
    C_GLASS = (120, 175, 215)
    C_JACK = (215, 110, 20)
    C_COPPER = (195, 95, 45)
    C_BEACON = (220, 30, 30)
    C_GRATING = (140, 145, 150)
    C_DIM = (0, 102, 178) # CAD Cyan-Blue
    C_DATUM = (180, 20, 20) # Red
    C_NOTE = (50, 50, 50)
    C_BG_BOX = (248, 250, 252)

    # 3. Outer Border & Grid Coordinates
    m_left, m_top, m_right, m_bottom = 60, 50, 60, 50
    draw.rectangle([m_left, m_top, W - m_right, H - m_bottom], outline=C_BORDER, width=4)
    draw.rectangle([m_left + 12, m_top + 12, W - m_right - 12, H - m_bottom - 12], outline=C_BORDER, width=2)

    # Coordinate Grid Marks (1..8 top/bottom, A..F left/right)
    grid_cols = 8
    col_w = (W - m_left - m_right - 24) / grid_cols
    for i in range(1, grid_cols):
        gx = m_left + 12 + i * col_w
        draw.line([gx, m_top, gx, m_top + 12], fill=C_BORDER, width=2)
        draw.line([gx, H - m_bottom - 12, gx, H - m_bottom], fill=C_BORDER, width=2)
        draw.text((gx - col_w * 0.5 - 6, m_top + 1), str(i), fill=C_BORDER, font=f_small)
        draw.text((gx - col_w * 0.5 - 6, H - m_bottom + 1), str(i), fill=C_BORDER, font=f_small)
    draw.text((W - m_right - 12 - col_w * 0.5 - 6, m_top + 1), str(grid_cols), fill=C_BORDER, font=f_small)
    draw.text((W - m_right - 12 - col_w * 0.5 - 6, H - m_bottom + 1), str(grid_cols), fill=C_BORDER, font=f_small)

    grid_rows = 6
    row_h = (H - m_top - m_bottom - 24) / grid_rows
    row_labels = ["A", "B", "C", "D", "E", "F"]
    for j in range(1, grid_rows):
        gy = m_top + 12 + j * row_h
        draw.line([m_left, gy, m_left + 12, gy], fill=C_BORDER, width=2)
        draw.line([W - m_right - 12, gy, W - m_right, gy], fill=C_BORDER, width=2)
        draw.text((m_left + 2, gy - row_h * 0.5 - 7), row_labels[j-1], fill=C_BORDER, font=f_small)
        draw.text((W - m_right + 3, gy - row_h * 0.5 - 7), row_labels[j-1], fill=C_BORDER, font=f_small)
    draw.text((m_left + 2, H - m_bottom - 12 - row_h * 0.5 - 7), row_labels[-1], fill=C_BORDER, font=f_small)
    draw.text((W - m_right + 3, H - m_bottom - 12 - row_h * 0.5 - 7), row_labels[-1], fill=C_BORDER, font=f_small)

    # =========================================================================
    # 4. MAIN VIEWPORT: SECTION 01 - VERTICAL CROSS SECTION (TOWER PEAK & SPIRE)
    # =========================================================================
    vp_x0, vp_y0 = 100, 80
    vp_w, vp_h = 1350, 2220
    draw.rectangle([vp_x0, vp_y0, vp_x0 + vp_w, vp_y0 + vp_h], fill=(255, 255, 255), outline=C_BORDER, width=2)

    # View Title Header Banner
    draw.rectangle([vp_x0, vp_y0, vp_x0 + vp_w, vp_y0 + 50], fill=(240, 243, 248), outline=C_BORDER, width=2)
    draw.text((vp_x0 + 20, vp_y0 + 12), "SECTION 01: VERTICAL CROSS SECTION THROUGH TOWER AXIS & TELESCOPIC SPIRE", fill=C_BORDER, font=f_header)
    draw.text((vp_x0 + vp_w - 180, vp_y0 + 14), "SCALE 1 : 200", fill=C_DIM, font=f_sub)

    # Mapping elevations (+560m to +832m = 272m range) to Canvas Y
    # Top of spire (+828m) at y = 200, bottom (+560m) at y = 2150
    # dy = 1950 px for 272 m -> scale ~ 7.169 px/meter
    z_min = 558.0
    z_max = 832.0
    def z_to_y(z):
        return 2150.0 - (z - z_min) * (1950.0 / (z_max - z_min))

    cx = vp_x0 + 640 # Tower centerline in main viewport

    # Draw Centerline
    y_top_cl = z_to_y(832.0) - 15
    y_bot_cl = z_to_y(558.0) + 15
    # Dash-dot pattern
    curr_y = y_top_cl
    while curr_y < y_bot_cl:
        draw.line([cx, curr_y, cx, min(curr_y + 35, y_bot_cl)], fill=(180, 40, 40), width=1)
        curr_y += 45
        draw.line([cx, curr_y, cx, min(curr_y + 6, y_bot_cl)], fill=(180, 40, 40), width=2)
        curr_y += 15

    # Centerline Symbol at Top and Bottom
    for cy_sym in [y_top_cl - 12, y_bot_cl + 12]:
        draw.ellipse([cx - 10, cy_sym - 10, cx + 10, cy_sym + 10], outline=(180, 40, 40), width=1)
        draw.line([cx - 14, cy_sym, cx + 14, cy_sym], fill=(180, 40, 40), width=1)
        draw.line([cx, cy_sym - 14, cx, cy_sym + 14], fill=(180, 40, 40), width=1)
        draw.text((cx + 12, cy_sym - 8), "CL", fill=(180, 40, 40), font=f_small)

    # Helper: Draw symmetrical section profile
    def draw_sym_poly(pts_half, fill_c, outline_c=C_STEEL, width=2):
        poly = []
        for x_m, z_m in pts_half:
            poly.append((cx + x_m * 14.0, z_to_y(z_m)))
        for x_m, z_m in reversed(pts_half):
            poly.append((cx - x_m * 14.0, z_to_y(z_m)))
        draw.polygon(poly, fill=fill_c, outline=outline_c)
        if width > 1:
            for i in range(len(poly)):
                draw.line([poly[i], poly[(i+1)%len(poly)]], fill=outline_c, width=width)

    # A. Level 154 - 156 Concrete Buttressed Core (Below +567.30m)
    # Concrete core profile: Half-width 14m at bottom, 12m at L156
    core_pts = [
        (13.0, 558.0),
        (12.5, 563.65),
        (12.0, 567.30),
        (0.0, 567.30),
        (0.0, 558.0)
    ]
    draw_sym_poly(core_pts, (230, 232, 235), C_BORDER, 3)

    # Concrete Hatching (Stipple Dots & Micro Triangles)
    y_c1, y_c2 = int(z_to_y(567.30)), int(z_to_y(558.0))
    for hy in range(y_c1 + 10, y_c2 - 10, 22):
        for hx in range(cx - 150, cx + 150, 30):
            if abs(hx - cx) > 15:
                draw.point((hx, hy), fill=(100, 105, 110))
                draw.point((hx + 1, hy), fill=(100, 105, 110))
                # Small triangle
                draw.polygon([(hx + 8, hy + 5), (hx + 13, hy + 5), (hx + 10, hy + 1)], fill=(120, 125, 130))

    # Core Inner Cavity (Central Corridor / Lift Hub)
    inner_cavity = [(3.2, 558.0), (3.2, 567.30), (0.0, 567.30), (0.0, 558.0)]
    draw_sym_poly(inner_cavity, (255, 255, 255), C_BORDER, 2)

    # B. Level 156 Steel Base Embedment Plates (t = 60mm at +567.30m)
    y_l156 = z_to_y(567.30)
    draw.rectangle([cx - 180, y_l156 - 4, cx - 40, y_l156 + 2], fill=(80, 85, 95), outline=C_STEEL, width=2)
    draw.rectangle([cx + 40, y_l156 - 4, cx + 180, y_l156 + 2], fill=(80, 85, 95), outline=C_STEEL, width=2)
    # Embedment Anchor Bolts into Concrete
    for ax in [cx - 160, cx - 120, cx - 80, cx - 50, cx + 50, cx + 80, cx + 120, cx + 160]:
        draw.line([ax, y_l156 + 2, ax, y_l156 + 32], fill=(60, 65, 70), width=3)
        draw.line([ax - 4, y_l156 + 32, ax + 4, y_l156 + 32], fill=(60, 65, 70), width=3)

    # C. Levels 156 to 160: Mechanical Belt 7 (+567.30m to +581.90m)
    # Cladding: Dark metallic spandrel louvers
    mech_envelope = [
        (12.0, 567.30),
        (11.2, 570.95),
        (10.5, 574.60),
        (9.8, 578.25),
        (9.0, 581.90)
    ]
    draw_sym_poly(mech_envelope, (235, 238, 242), C_BORDER, 2)

    # Mechanical Floors Slabs & Louver bands
    for fl_z in [570.95, 574.60, 578.25, 581.90]:
        fy = z_to_y(fl_z)
        draw.line([cx - 165, fy, cx + 165, fy], fill=C_BORDER, width=3)
        # Louver texture on outer wings
        for ly in range(int(fy) - 18, int(fy), 4):
            draw.line([cx - 160, ly, cx - 110, ly], fill=(90, 95, 105), width=1)
            draw.line([cx + 110, ly, cx + 160, ly], fill=(90, 95, 105), width=1)

    # Outrigger Steel Diagonal Trusses across L156 - L160
    y_156 = z_to_y(567.30)
    y_160 = z_to_y(581.90)
    draw.line([cx - 50, y_156, cx - 140, y_160], fill=(45, 50, 60), width=4)
    draw.line([cx - 140, y_156, cx - 50, y_160], fill=(45, 50, 60), width=4)
    draw.line([cx + 50, y_156, cx + 140, y_160], fill=(45, 50, 60), width=4)
    draw.line([cx + 140, y_156, cx + 50, y_160], fill=(45, 50, 60), width=4)

    # MEP Equipment Symbols (Pumps & Chillers)
    draw.rectangle([cx - 95, y_156 - 18, cx - 65, y_156 - 4], fill=(160, 170, 180), outline=C_BORDER, width=1)
    draw.rectangle([cx + 65, y_156 - 18, cx + 95, y_156 - 4], fill=(160, 170, 180), outline=C_BORDER, width=1)
    draw.text((cx - 92, y_156 - 17), "CHL", fill=C_BORDER, font=f_micro)
    draw.text((cx + 68, y_156 - 17), "PMP", fill=C_BORDER, font=f_micro)

    # D. Levels 160 to 163: Broadcast & Telecommunications Hub (+581.90m to +596.50m)
    telecom_envelope = [
        (9.0, 581.90),
        (8.2, 586.50),
        (7.5, 591.50),
        (6.8, 596.50)
    ]
    draw_sym_poly(telecom_envelope, (240, 246, 252), C_GLASS, 2)
    # Slabs for L161, L162, L163
    for fl_z in [586.50, 591.50, 596.50]:
        fy = z_to_y(fl_z)
        draw.line([cx - 110, fy, cx + 110, fy], fill=C_BORDER, width=3)
    # Glazing Mullions
    for mx in [-100, -60, -25, 25, 60, 100]:
        draw.line([cx + mx, z_to_y(581.90), cx + mx * 0.75, z_to_y(596.50)], fill=C_GLASS, width=1)

    # E. Level 163 to Spire Base (+596.50m to +630.00m): Telescopic Strand Jacking Chamber
    jack_envelope = [
        (6.8, 596.50),
        (5.5, 610.00),
        (4.5, 620.00),
        (4.0, 630.00)
    ]
    draw_sym_poly(jack_envelope, (245, 245, 248), C_STEEL, 2)

    # 4x 500T Hydraulic Strand Jacks on L163 Slab
    y_l163 = z_to_y(596.50)
    draw.rectangle([cx - 75, y_l163 - 28, cx - 45, y_l163 - 4], fill=C_JACK, outline=C_STEEL, width=2)
    draw.rectangle([cx + 45, y_l163 - 28, cx + 75, y_l163 - 4], fill=C_JACK, outline=C_STEEL, width=2)
    draw.text((cx - 72, y_l163 - 25), "500T", fill=(255, 255, 255), font=f_micro)
    draw.text((cx + 48, y_l163 - 25), "500T", fill=(255, 255, 255), font=f_micro)
    # Strand Cables extending upward
    draw.line([cx - 60, y_l163 - 28, cx - 35, z_to_y(630.00)], fill=(70, 75, 80), width=2)
    draw.line([cx + 60, y_l163 - 28, cx + 35, z_to_y(630.00)], fill=(70, 75, 80), width=2)
    # Jacking Grillage Transfer Beams
    draw.rectangle([cx - 85, y_l163 - 4, cx + 85, y_l163], fill=(80, 85, 95), outline=C_STEEL, width=1)

    # F. Telescopic Steel Spire - Lower Clad Mast (+630.0m to +700.0m)
    # Exterior Stainless Steel Cladding (D = 6.0m tapering to 4.5m)
    spire_tier1 = [
        (3.0, 630.00),
        (2.65, 665.00),
        (2.25, 700.00)
    ]
    draw_sym_poly(spire_tier1, (225, 230, 238), C_BORDER, 2)

    # Continuous Central Steel Pipe Column (D = 2.10m, R = 1.05m -> 14.7 px half width)
    # Running from L163 (+596.50m) up to +760.00m!
    pipe_y_bot = z_to_y(596.50)
    pipe_y_top = z_to_y(760.00)
    draw.rectangle([cx - 15, pipe_y_top, cx + 15, pipe_y_bot], fill=(175, 180, 190), outline=C_STEEL, width=2)
    # Pipe Center Hollow Bore
    draw.rectangle([cx - 9, pipe_y_top + 2, cx + 9, pipe_y_bot - 2], fill=(255, 255, 255), outline=(130, 135, 145), width=1)

    # Catwalk Platform 1 at +650.0m
    y_cat1 = z_to_y(650.00)
    draw.rectangle([cx - 38, y_cat1 - 2, cx + 38, y_cat1 + 2], fill=C_GRATING, outline=C_STEEL, width=1)
    # Handrails (H = 1.1m = 8px)
    draw.line([cx - 36, y_cat1 - 2, cx - 36, y_cat1 - 10], fill=C_STEEL, width=1)
    draw.line([cx + 36, y_cat1 - 2, cx + 36, y_cat1 - 10], fill=C_STEEL, width=1)
    draw.line([cx - 36, y_cat1 - 10, cx + 36, y_cat1 - 10], fill=C_STEEL, width=1)

    # Catwalk Platform 2 at +700.0m
    y_cat2 = z_to_y(700.00)
    draw.rectangle([cx - 32, y_cat2 - 2, cx + 32, y_cat2 + 2], fill=C_GRATING, outline=C_STEEL, width=1)
    draw.line([cx - 30, y_cat2 - 2, cx - 30, y_cat2 - 10], fill=C_STEEL, width=1)
    draw.line([cx + 30, y_cat2 - 2, cx + 30, y_cat2 - 10], fill=C_STEEL, width=1)
    draw.line([cx - 30, y_cat2 - 10, cx + 30, y_cat2 - 10], fill=C_STEEL, width=1)

    # Vertical Safety Cage Access Ladder inside Spire
    for ly in range(int(y_cat2), int(y_l163), 12):
        draw.line([cx + 4, ly, cx + 7, ly], fill=(50, 55, 65), width=1)
    draw.line([cx + 4, y_cat2, cx + 4, y_l163], fill=(50, 55, 65), width=1)
    draw.line([cx + 7, y_cat2, cx + 7, y_l163], fill=(50, 55, 65), width=1)

    # G. Upper Clad Spire & Tuned Mass Damper Zone (+700.0m to +760.0m)
    spire_tier2 = [
        (2.25, 700.00),
        (1.85, 730.00),
        (1.50, 760.00)
    ]
    draw_sym_poly(spire_tier2, (230, 235, 242), C_BORDER, 2)

    # Tuned Mass Damper (TMD) Pendulum Ballast Assembly (+726.0m to +730.0m)
    y_tmd_top = z_to_y(730.00)
    y_tmd_bot = z_to_y(726.00)
    # Suspension Cables
    draw.line([cx - 18, z_to_y(734.00), cx - 18, y_tmd_top], fill=(60, 60, 60), width=1)
    draw.line([cx + 18, z_to_y(734.00), cx + 18, y_tmd_top], fill=(60, 60, 60), width=1)
    # Cylindrical Steel Ballast Block
    draw.rectangle([cx - 24, y_tmd_top, cx - 15, y_tmd_bot], fill=(85, 90, 100), outline=C_STEEL, width=2)
    draw.rectangle([cx + 15, y_tmd_top, cx + 24, y_tmd_bot], fill=(85, 90, 100), outline=C_STEEL, width=2)
    draw.text((cx - 23, y_tmd_top + 8), "TMD", fill=(255, 255, 255), font=f_micro)

    # Catwalk Platform 3 at +750.0m
    y_cat3 = z_to_y(750.00)
    draw.rectangle([cx - 24, y_cat3 - 2, cx + 24, y_cat3 + 2], fill=C_GRATING, outline=C_STEEL, width=1)
    draw.line([cx - 22, y_cat3 - 10, cx + 22, y_cat3 - 10], fill=C_STEEL, width=1)

    # H. Open Lattice Structural Steel Mast (+760.0m to +810.0m)
    # Tubular chords and diagonal lacing
    spire_tier3 = [
        (1.50, 760.00),
        (1.10, 785.00),
        (0.75, 810.00)
    ]
    # Draw open truss lattice lines
    y_760 = z_to_y(760.00)
    y_810 = z_to_y(810.00)
    draw.line([cx - 21, y_760, cx - 10, y_810], fill=C_STEEL, width=3)
    draw.line([cx + 21, y_760, cx + 10, y_810], fill=C_STEEL, width=3)
    # Lattice X-bracing
    for lz in range(760, 810, 8):
        y_a = z_to_y(lz)
        y_b = z_to_y(lz + 8)
        w_a = 21.0 - (lz - 760) * (11.0 / 50.0)
        w_b = 21.0 - (lz + 8 - 760) * (11.0 / 50.0)
        draw.line([cx - w_a, y_a, cx + w_b, y_b], fill=(70, 75, 85), width=2)
        draw.line([cx + w_a, y_a, cx - w_b, y_b], fill=(70, 75, 85), width=2)
        draw.line([cx - w_a, y_a, cx + w_a, y_a], fill=(70, 75, 85), width=1)

    # I. Pinnacle Forged Steel Needle (+810.0m to +828.0m)
    y_828 = z_to_y(828.00)
    needle_pts = [(cx - 10, y_810), (cx + 10, y_810), (cx + 3, y_828), (cx - 3, y_828)]
    draw.polygon(needle_pts, fill=(190, 195, 205), outline=C_STEEL)
    draw.line([cx - 10, y_810, cx - 3, y_828], fill=C_STEEL, width=2)
    draw.line([cx + 10, y_810, cx + 3, y_828], fill=C_STEEL, width=2)

    # J. Pinnacle Aircraft Beacon & Lightning Arrester Rod (+828.0m to +831.5m)
    # Beacon sphere
    draw.ellipse([cx - 8, y_828 - 10, cx + 8, y_828 + 6], fill=C_BEACON, outline=C_STEEL, width=2)
    draw.point((cx - 3, y_828 - 4), fill=(255, 255, 255))
    # Lightning Rod (Solid Copper)
    y_831 = z_to_y(831.20)
    draw.line([cx, y_828 - 10, cx, y_831], fill=C_COPPER, width=3)
    draw.polygon([(cx - 3, y_831 + 4), (cx + 3, y_831 + 4), (cx, y_831)], fill=C_COPPER)

    # =========================================================================
    # 5. DATUM ELEVATION MARKERS & DIMENSION CHAINS (LEFT SIDE)
    # =========================================================================
    datum_levels = [
        (828.00, "+828.000 m", "TOP OF SPIRE PINNACLE (EL. +828.00)"),
        (810.00, "+810.000 m", "TOP OF OPEN LATTICE MAST (TIER 26)"),
        (760.00, "+760.000 m", "TOP OF CLAD SPIRE MAST (TIER 24)"),
        (750.00, "+750.000 m", "MAINTENANCE CATWALK PLATFORM 3"),
        (730.00, "+730.000 m", "TUNED MASS DAMPER (TMD) CENTERLINE"),
        (700.00, "+700.000 m", "MAINTENANCE CATWALK PLATFORM 2 (TIER 22)"),
        (650.00, "+650.000 m", "MAINTENANCE CATWALK PLATFORM 1 (TIER 20)"),
        (630.00, "+630.000 m", "SPIRE BASE COLLAR & JACKING HEAD"),
        (596.50, "+596.500 m", "LEVEL 163 - SPIRE JACKING BASE SLAB"),
        (591.50, "+591.500 m", "LEVEL 162 - TELECOM OPERATIONS DECK"),
        (586.50, "+586.500 m", "LEVEL 161 - TOP MECHANICAL PLANT"),
        (581.90, "+581.900 m", "LEVEL 160 - MECHANICAL BELT 7 TOP"),
        (567.30, "+567.300 m", "LEVEL 156 - CONCRETE-TO-STEEL TRANSITION"),
        (560.00, "+560.000 m", "LEVEL 154 - REINFORCED CONCRETE CORE")
    ]

    x_datum_head = vp_x0 + 60
    for z_elev, elev_str, name_str in datum_levels:
        ey = z_to_y(z_elev)
        # Extension dashed line across to tower
        draw.line([x_datum_head + 30, ey, cx - 180, ey], fill=(210, 215, 225), width=1)

        # Standard Target Elevation Symbol (Quarter-filled circle)
        r_head = 9
        draw.ellipse([x_datum_head - r_head, ey - r_head, x_datum_head + r_head, ey + r_head], outline=C_DATUM, width=1)
        draw.pieslice([x_datum_head - r_head, ey - r_head, x_datum_head + r_head, ey + r_head], 0, 90, fill=C_DATUM)
        draw.pieslice([x_datum_head - r_head, ey - r_head, x_datum_head + r_head, ey + r_head], 180, 270, fill=C_DATUM)
        draw.line([x_datum_head - r_head - 4, ey, x_datum_head + r_head + 25, ey], fill=C_DATUM, width=1)

        # Elevation Text
        draw.text((x_datum_head + r_head + 6, ey - 18), elev_str, fill=C_DATUM, font=f_dim)
        draw.text((x_datum_head + r_head + 6, ey + 1), name_str, fill=C_BORDER, font=f_small)

    # Vertical Dimension Chain Line
    x_dim_line = vp_x0 + 440
    draw.line([x_dim_line, z_to_y(560.00), x_dim_line, z_to_y(828.00)], fill=C_DIM, width=1)
    dim_segments = [
        (560.00, 567.30, "7 300"),
        (567.30, 581.90, "14 600"),
        (581.90, 586.50, "4 600"),
        (586.50, 591.50, "5 000"),
        (591.50, 596.50, "5 000"),
        (596.50, 630.00, "33 500"),
        (630.00, 700.00, "70 000"),
        (700.00, 760.00, "60 000"),
        (760.00, 810.00, "50 000"),
        (810.00, 828.00, "18 000")
    ]
    for z1, z2, d_text in dim_segments:
        y1, y2 = z_to_y(z1), z_to_y(z2)
        # Extension tick marks
        draw.line([x_dim_line - 15, y1, x_dim_line + 15, y1], fill=C_DIM, width=1)
        draw.line([x_dim_line - 15, y2, x_dim_line + 15, y2], fill=C_DIM, width=1)
        # 45-degree architectural ticks
        draw.line([x_dim_line - 5, y1 + 5, x_dim_line + 5, y1 - 5], fill=C_DIM, width=2)
        draw.line([x_dim_line - 5, y2 + 5, x_dim_line + 5, y2 - 5], fill=C_DIM, width=2)
        # Dimension text (vertical centered)
        mid_y = (y1 + y2) * 0.5
        draw.text((x_dim_line - 58, mid_y - 8), d_text, fill=C_DIM, font=f_dim)

    # Total Overall Dimension Chain Line
    x_total_dim = vp_x0 + 360
    y_bot_tot, y_top_tot = z_to_y(567.30), z_to_y(828.00)
    draw.line([x_total_dim, y_bot_tot, x_total_dim, y_top_tot], fill=C_DIM, width=1)
    draw.line([x_total_dim - 20, y_bot_tot, x_total_dim + 20, y_bot_tot], fill=C_DIM, width=1)
    draw.line([x_total_dim - 20, y_top_tot, x_total_dim + 20, y_top_tot], fill=C_DIM, width=1)
    draw.line([x_total_dim - 6, y_bot_tot + 6, x_total_dim + 6, y_bot_tot - 6], fill=C_DIM, width=2)
    draw.line([x_total_dim - 6, y_top_tot + 6, x_total_dim + 6, y_top_tot - 6], fill=C_DIM, width=2)
    draw.text((x_total_dim - 70, (y_bot_tot + y_top_tot) * 0.5 - 8), "260 700", fill=C_DIM, font=f_dim)
    draw.text((x_total_dim - 95, (y_bot_tot + y_top_tot) * 0.5 + 12), "(TOTAL SPIRE ZONE)", fill=C_DIM, font=f_micro)

    # Diameter Callouts on the Right of Tower
    diam_callouts = [
        (828.00, 100, "DIA 100 mm TIP"),
        (810.00, 750, "DIA 750 mm MAST"),
        (760.00, 1500, "DIA 1 500 mm PIPE"),
        (700.00, 4500, "DIA 4 500 mm CLAD"),
        (630.00, 6000, "DIA 6 000 mm BASE"),
        (596.50, 13600, "WIDTH 13 600 mm CORE"),
        (567.30, 24000, "WIDTH 24 000 mm BUTTRESS")
    ]
    for z_d, d_val, d_str in diam_callouts:
        dy = z_to_y(z_d)
        dx_start = cx + (d_val / 1000.0) * 7.0
        draw.line([dx_start, dy, cx + 220, dy], fill=C_DIM, width=1)
        draw.polygon([(dx_start, dy), (dx_start + 8, dy - 3), (dx_start + 8, dy + 3)], fill=C_DIM)
        draw.text((cx + 225, dy - 8), d_str, fill=C_DIM, font=f_dim)

    # =========================================================================
    # 6. DETAIL CALLOUT BUBBLES ON MAIN SECTION
    # =========================================================================
    def draw_callout_bubble(bx, by, r, label_text, sheet_text):
        draw.ellipse([bx - r, by - r, bx + r, by + r], outline=(200, 30, 30), width=2)
        draw.line([bx - r, by, bx + r, by], fill=(200, 30, 30), width=2)
        draw.text((bx - 12, by - r + 4), label_text, fill=(200, 30, 30), font=f_sub)
        draw.text((bx - 15, by + 4), sheet_text, fill=(200, 30, 30), font=f_small)

    # Bubble A at L156 Transition
    draw_callout_bubble(cx + 175, z_to_y(567.30), 26, "A", "S-828")
    draw.line([cx + 120, z_to_y(567.30), cx + 149, z_to_y(567.30)], fill=(200, 30, 30), width=1)

    # Bubble B at L163 Jacking Chamber
    draw_callout_bubble(cx - 145, z_to_y(598.00), 26, "B", "S-828")
    draw.line([cx - 75, z_to_y(598.00), cx - 119, z_to_y(598.00)], fill=(200, 30, 30), width=1)

    # Bubble C at +700m Catwalk & TMD
    draw_callout_bubble(cx + 125, z_to_y(700.00), 26, "C", "S-828")
    draw.line([cx + 40, z_to_y(700.00), cx + 99, z_to_y(700.00)], fill=(200, 30, 30), width=1)

    # Bubble D at +828m Beacon & Lightning Arrester
    draw_callout_bubble(cx + 95, z_to_y(828.00), 26, "D", "S-828")
    draw.line([cx + 15, z_to_y(828.00), cx + 69, z_to_y(828.00)], fill=(200, 30, 30), width=1)

    # =========================================================================
    # 7. ENLARGED INSET DETAILS (DETAILS A, B, C, D)
    # =========================================================================
    # Grid of 4 Detail Boxes:
    # Top-Left: DETAIL D (+828m Beacon), Top-Right: DETAIL C (+700m Catwalk)
    # Mid-Left: DETAIL B (L163 Jacking), Mid-Right: DETAIL A (L156 Transition)
    dt_w, dt_h = 760, 490

    # -------------------------------------------------------------------------
    # DETAIL A: CONCRETE-TO-STEEL TRANSITION JOINT AT L156 (+567.30M)
    # -------------------------------------------------------------------------
    da_x, da_y = 2270, 590
    draw.rectangle([da_x, da_y, da_x + dt_w, da_y + dt_h], fill=C_BG_BOX, outline=C_BORDER, width=2)
    # Header
    draw.rectangle([da_x, da_y, da_x + dt_w, da_y + 40], fill=(235, 240, 248), outline=C_BORDER, width=1)
    draw.text((da_x + 15, da_y + 8), "DETAIL A: CONCRETE-TO-STEEL CORE TRANSITION (LEVEL 156)", fill=C_BORDER, font=f_sub)
    draw.text((da_x + dt_w - 140, da_y + 11), "SCALE 1 : 25", fill=C_DIM, font=f_body_bold)

    # Detail A Graphic
    # Concrete buttress head (hatch), 60mm baseplate, shear studs, anchor bolts, box column
    c_head_y = da_y + 260
    draw.rectangle([da_x + 80, c_head_y, da_x + dt_w - 80, da_y + dt_h - 20], fill=(225, 228, 232), outline=C_BORDER, width=2)
    # Concrete Stipple
    for sy in range(c_head_y + 10, da_y + dt_h - 25, 20):
        for sx in range(da_x + 90, da_x + dt_w - 90, 25):
            draw.point((sx, sy), fill=(100, 105, 110))
            draw.polygon([(sx + 5, sy + 4), (sx + 9, sy + 4), (sx + 7, sy)], fill=(120, 125, 130))

    # High-Strength Epoxy Grout Bed (t = 50mm)
    draw.rectangle([da_x + 80, c_head_y - 14, da_x + dt_w - 80, c_head_y], fill=(185, 190, 195), outline=C_BORDER, width=1)
    # 60mm Solid Steel Baseplate (Grade S460)
    draw.rectangle([da_x + 70, c_head_y - 30, da_x + dt_w - 70, c_head_y - 14], fill=(70, 75, 85), outline=C_STEEL, width=2)

    # Structural Box Column Profile above plate
    draw.rectangle([da_x + 160, da_y + 60, da_x + dt_w - 160, c_head_y - 30], fill=(240, 242, 245), outline=C_STEEL, width=3)
    # Stiffener Gusset Plates
    draw.polygon([(da_x + 160, c_head_y - 30), (da_x + 110, c_head_y - 30), (da_x + 160, c_head_y - 120)], fill=(100, 105, 115), outline=C_STEEL)
    draw.polygon([(da_x + dt_w - 160, c_head_y - 30), (da_x + dt_w - 110, c_head_y - 30), (da_x + dt_w - 160, c_head_y - 120)], fill=(100, 105, 115), outline=C_STEEL)

    # Anchor Bolts (M48 Grade 10.9 with double heavy hex nuts)
    for bx in [da_x + 130, da_x + 200, da_x + dt_w - 200, da_x + dt_w - 130]:
        draw.line([bx, c_head_y - 45, bx, c_head_y + 160], fill=(40, 45, 50), width=4)
        draw.rectangle([bx - 6, c_head_y - 42, bx + 6, c_head_y - 32], fill=(80, 85, 90)) # Upper nut
        draw.line([bx - 12, c_head_y + 160, bx + 12, c_head_y + 160], fill=(40, 45, 50), width=4) # Embedment anchor plate

    # Shear Stud Connectors welded to underside
    for sx in range(da_x + 230, da_x + dt_w - 230, 35):
        draw.line([sx, c_head_y - 14, sx, c_head_y + 60], fill=(50, 55, 60), width=3)
        draw.ellipse([sx - 4, c_head_y + 58, sx + 4, c_head_y + 66], fill=(50, 55, 60))

    # Notes & Callouts for Detail A
    def draw_callout_line(x1, y1, x2, y2, text, subtext=""):
        draw.line([x1, y1, x2, y2], fill=C_BORDER, width=1)
        draw.polygon([(x1, y1), (x1 + 6, y1 - 3), (x1 + 6, y1 + 3)], fill=C_BORDER)
        draw.line([x2, y2, x2 + 100, y2], fill=C_BORDER, width=1)
        draw.text((x2 + 10, y2 - 16), text, fill=C_BORDER, font=f_small)
        if subtext:
            draw.text((x2 + 10, y2 + 2), subtext, fill=C_DIM, font=f_micro)

    draw_callout_line(da_x + 160, c_head_y - 80, da_x + 20, da_y + 90, "BUILT-UP STEEL BOX COLUMN", "PL 500x500x32 S460")
    draw_callout_line(da_x + 70, c_head_y - 22, da_x + 20, da_y + 160, "BASE EMBEDMENT PLATE t = 60mm", "EN 10025-2 S460ML")
    draw_callout_line(da_x + 80, c_head_y - 7, da_x + 20, da_y + 220, "NON-SHRINK HIGH STRENGTH GROUT", "t = 50mm, fck = 80 MPa")
    draw_callout_line(da_x + 130, c_head_y + 80, da_x + 20, da_y + 300, "8x M48 Gr 10.9 ANCHOR RODS", "EMBEDMENT DEPTH 1800mm")
    draw_callout_line(da_x + dt_w - 80, c_head_y + 40, da_x + dt_w - 240, da_y + 390, "HIGH-STRENGTH CONCRETE CORE", "C80 (fck = 80 MPa)")

    # -------------------------------------------------------------------------
    # DETAIL B: TELESCOPIC STRAND JACKING BASE AT L163 (+596.50M)
    # -------------------------------------------------------------------------
    db_x, db_y = 1480, 590
    draw.rectangle([db_x, db_y, db_x + dt_w, db_y + dt_h], fill=C_BG_BOX, outline=C_BORDER, width=2)
    draw.rectangle([db_x, db_y, db_x + dt_w, db_y + 40], fill=(235, 240, 248), outline=C_BORDER, width=1)
    draw.text((db_x + 15, db_y + 8), "DETAIL B: HYDRAULIC STRAND JACKING ASSEMBLY (LEVEL 163)", fill=C_BORDER, font=f_sub)
    draw.text((db_x + dt_w - 140, db_y + 11), "SCALE 1 : 25", fill=C_DIM, font=f_body_bold)

    # Floor Slab L163
    j_slab_y = db_y + 360
    draw.rectangle([db_x + 40, j_slab_y, db_x + dt_w - 40, j_slab_y + 30], fill=(225, 228, 232), outline=C_BORDER, width=2)

    # Heavy Steel Grillage Transfer Beams (W1000x249 profile)
    draw.rectangle([db_x + 100, j_slab_y - 45, db_x + dt_w - 100, j_slab_y], fill=(90, 95, 105), outline=C_STEEL, width=2)
    # Flange / web lines of I-beam
    draw.line([db_x + 100, j_slab_y - 35, db_x + dt_w - 100, j_slab_y - 35], fill=C_STEEL, width=1)
    draw.line([db_x + 100, j_slab_y - 10, db_x + dt_w - 100, j_slab_y - 10], fill=C_STEEL, width=1)

    # 2x 500-Ton Hydraulic Strand Jacks
    for jx in [db_x + 160, db_x + dt_w - 240]:
        # Jack Base Plate
        draw.rectangle([jx - 10, j_slab_y - 50, jx + 90, j_slab_y - 45], fill=(50, 50, 55), outline=C_STEEL, width=1)
        # Jack Main Body (Safety Orange)
        draw.rectangle([jx, j_slab_y - 170, jx + 80, j_slab_y - 50], fill=C_JACK, outline=C_STEEL, width=2)
        # Top Hydraulic Manifold & Anchor Head
        draw.rectangle([jx + 10, j_slab_y - 200, jx + 70, j_slab_y - 170], fill=(60, 65, 75), outline=C_STEEL, width=1)
        # Strand Cables exiting top
        for sx in [jx + 25, jx + 40, jx + 55]:
            draw.line([sx, j_slab_y - 200, sx, db_y + 60], fill=(40, 45, 50), width=2)
        # Jack Capacity Label
        draw.text((jx + 12, j_slab_y - 120), "500-TON", fill=(255, 255, 255), font=f_body_bold)
        draw.text((jx + 8, j_slab_y - 95), "HYDRAULIC", fill=(255, 255, 255), font=f_micro)

    # Spire Mast Sleeve passing through center (D = 2.1m)
    draw.rectangle([db_x + 300, db_y + 55, db_x + 460, j_slab_y - 45], fill=(215, 220, 228), outline=C_STEEL, width=3)
    # Temporary Shim Locking Wedge Plates
    draw.polygon([(db_x + 280, j_slab_y - 80), (db_x + 300, j_slab_y - 80), (db_x + 300, j_slab_y - 50), (db_x + 270, j_slab_y - 50)], fill=(70, 75, 80), outline=C_STEEL)
    draw.polygon([(db_x + 480, j_slab_y - 80), (db_x + 460, j_slab_y - 80), (db_x + 460, j_slab_y - 50), (db_x + 490, j_slab_y - 50)], fill=(70, 75, 80), outline=C_STEEL)

    # Callouts for Detail B
    draw_callout_line(db_x + 240, j_slab_y - 140, db_x + 30, db_y + 100, "500-TON HYDRAULIC STRAND JACK", "STROKE 250mm, 31x STRANDS")
    draw_callout_line(db_x + 300, db_y + 120, db_x + 30, db_y + 180, "TELESCOPIC SPIRE PIPE CORE", "DIA 2100mm x 40mm THK")
    draw_callout_line(db_x + 280, j_slab_y - 65, db_x + 30, db_y + 260, "TEMPORARY LOCKING SHIMS", "HIGH-TENSILE ALLOY WEDGES")
    draw_callout_line(db_x + 100, j_slab_y - 25, db_x + 30, db_y + 320, "GRILLAGE TRANSFER BEAMS", "DUAL W1000x249 S460 STEEL")

    # -------------------------------------------------------------------------
    # DETAIL C: INTERNAL SPIRE CATWALK & LADDER PLATFORM (+700.00M)
    # -------------------------------------------------------------------------
    dc_x, dc_y = 2270, 80
    draw.rectangle([dc_x, dc_y, dc_x + dt_w, dc_y + dt_h], fill=C_BG_BOX, outline=C_BORDER, width=2)
    draw.rectangle([dc_x, dc_y, dc_x + dt_w, dc_y + 40], fill=(235, 240, 248), outline=C_BORDER, width=1)
    draw.text((dc_x + 15, dc_y + 8), "DETAIL C: INTERNAL CATWALK & CAGE LADDER (+700.00M)", fill=C_BORDER, font=f_sub)
    draw.text((dc_x + dt_w - 140, dc_y + 11), "SCALE 1 : 20", fill=C_DIM, font=f_body_bold)

    # Outer Spire Cladding Walls (D = 4.5m)
    draw.line([dc_x + 80, dc_y + 60, dc_x + 80, dc_y + dt_h - 20], fill=C_STEEL, width=4)
    draw.line([dc_x + dt_w - 80, dc_y + 60, dc_x + dt_w - 80, dc_y + dt_h - 20], fill=C_STEEL, width=4)

    # Central Steel Pipe Column (D = 2.1m)
    draw.rectangle([dc_x + 280, dc_y + 60, dc_x + 480, dc_y + dt_h - 20], fill=(215, 220, 228), outline=C_STEEL, width=3)

    # Catwalk Platform Grating (Annular donut disc spanning between cladding and pipe)
    plat_y = dc_y + 320
    draw.rectangle([dc_x + 80, plat_y, dc_x + 280, plat_y + 12], fill=C_GRATING, outline=C_STEEL, width=1)
    draw.rectangle([dc_x + 480, plat_y, dc_x + dt_w - 80, plat_y + 12], fill=C_GRATING, outline=C_STEEL, width=1)
    # Grating Mesh Texture
    for gx in range(dc_x + 85, dc_x + 275, 12):
        draw.line([gx, plat_y, gx, plat_y + 12], fill=(80, 85, 90), width=1)
    for gx in range(dc_x + 485, dc_x + dt_w - 85, 12):
        draw.line([gx, plat_y, gx, plat_y + 12], fill=(80, 85, 90), width=1)

    # Safety Handrail (H = 1100mm)
    rail_top_y = plat_y - 75
    draw.line([dc_x + 110, rail_top_y, dc_x + 270, rail_top_y], fill=C_STEEL, width=3)
    draw.line([dc_x + 110, plat_y - 38, dc_x + 270, plat_y - 38], fill=C_STEEL, width=1) # Mid-rail
    draw.line([dc_x + 110, plat_y - 12, dc_x + 270, plat_y - 12], fill=(60, 65, 75), width=2) # Kickplate
    for post_x in [dc_x + 120, dc_x + 190, dc_x + 260]:
        draw.line([post_x, plat_y, post_x, rail_top_y], fill=C_STEEL, width=2)

    # Vertical Cage Access Ladder
    lad_x = dc_x + 330
    draw.line([lad_x, dc_y + 80, lad_x, dc_y + dt_h - 30], fill=(40, 45, 55), width=3)
    draw.line([lad_x + 30, dc_y + 80, lad_x + 30, dc_y + dt_h - 30], fill=(40, 45, 55), width=3)
    for ry in range(dc_y + 90, dc_y + dt_h - 30, 22):
        draw.line([lad_x, ry, lad_x + 30, ry], fill=(40, 45, 55), width=2)
    # Safety Hoop Cage around ladder
    for hy in range(dc_y + 110, dc_y + dt_h - 50, 45):
        draw.arc([lad_x - 15, hy - 15, lad_x + 45, hy + 15], 180, 0, fill=(70, 75, 85), width=2)

    # Callouts for Detail C
    draw_callout_line(dc_x + 200, plat_y + 6, dc_x + 20, dc_y + 200, "HOT-DIP GALVANIZED SERRATED GRATING", "32x5mm BEARING BARS @ 30mm")
    draw_callout_line(dc_x + 190, rail_top_y, dc_x + 20, dc_y + 130, "SAFETY HANDRAIL H = 1100mm", "DIA 48.3mm x 3.2mm CHS POSTS")
    draw_callout_line(dc_x + 330, dc_y + 150, dc_x + dt_w - 240, dc_y + 100, "SAFETY CAGE ACCESS LADDER", "STEEL RUNGS @ 250mm C/C")
    draw_callout_line(dc_x + dt_w - 80, dc_y + 220, dc_x + dt_w - 240, dc_y + 220, "STAINLESS STEEL CLADDING", "GRADE 316L EMBOSSED PANELS")

    # -------------------------------------------------------------------------
    # DETAIL D: PINNACLE BEACON & LIGHTNING ARRESTER ASSEMBLY (+828.00M)
    # -------------------------------------------------------------------------
    dd_x, dd_y = 1480, 80
    draw.rectangle([dd_x, dd_y, dd_x + dt_w, dd_y + dt_h], fill=C_BG_BOX, outline=C_BORDER, width=2)
    draw.rectangle([dd_x, dd_y, dd_x + dt_w, dd_y + 40], fill=(235, 240, 248), outline=C_BORDER, width=1)
    draw.text((dd_x + 15, dd_y + 8), "DETAIL D: PINNACLE BEACON & LIGHTNING ARRESTER (+828.00M)", fill=C_BORDER, font=f_sub)
    draw.text((dd_x + dt_w - 140, dd_y + 11), "SCALE 1 : 15", fill=C_DIM, font=f_body_bold)

    # Tapering Needle Mast Head (D = 150mm tapering to 100mm)
    mast_y_top = dd_y + 290
    draw.polygon([(dd_x + 350, dd_y + dt_h - 20), (dd_x + 410, dd_y + dt_h - 20), (dd_x + 395, mast_y_top), (dd_x + 365, mast_y_top)], fill=(190, 195, 205), outline=C_STEEL)
    draw.line([dd_x + 350, dd_y + dt_h - 20, dd_x + 365, mast_y_top], fill=C_STEEL, width=3)
    draw.line([dd_x + 410, dd_y + dt_h - 20, dd_x + 395, mast_y_top], fill=C_STEEL, width=3)

    # Flange Mount Cap Plate (D = 250mm x 25mm thk)
    draw.rectangle([dd_x + 335, mast_y_top - 12, dd_x + 425, mast_y_top], fill=(70, 75, 85), outline=C_STEEL, width=2)
    # Bolted connections
    for bx in [dd_x + 345, dd_x + 360, dd_x + 400, dd_x + 415]:
        draw.line([bx, mast_y_top - 18, bx, mast_y_top + 6], fill=(40, 45, 50), width=3)

    # Aircraft Warning Beacon (Dual High-Intensity ICAO/FAA Type L-856 White/Red Strobe)
    b_center_y = mast_y_top - 70
    draw.rectangle([dd_x + 345, mast_y_top - 35, dd_x + 415, mast_y_top - 12], fill=(60, 65, 75), outline=C_STEEL, width=2) # Mounting bracket
    draw.ellipse([dd_x + 330, b_center_y - 35, dd_x + 430, b_center_y + 35], fill=C_BEACON, outline=C_STEEL, width=3)
    # Fresnel Glass Lens Rings
    draw.ellipse([dd_x + 345, b_center_y - 22, dd_x + 415, b_center_y + 22], outline=(255, 255, 255), width=2)
    draw.line([dd_x + 330, b_center_y, dd_x + 430, b_center_y], fill=(255, 255, 255), width=1)
    # High-Power LED Cluster
    draw.ellipse([dd_x + 370, b_center_y - 10, dd_x + 390, b_center_y + 10], fill=(255, 255, 200), outline=C_STEEL)

    # Solid Electrolytic Copper Lightning Strike Rod (D = 32mm, H = 2500mm)
    rod_y_top = dd_y + 55
    draw.line([dd_x + 380, b_center_y - 35, dd_x + 380, rod_y_top], fill=C_COPPER, width=7)
    # Early Streamer Emission (ESE) Multi-Point Terminal
    draw.polygon([(dd_x + 372, rod_y_top + 15), (dd_x + 388, rod_y_top + 15), (dd_x + 380, rod_y_top)], fill=C_COPPER)
    draw.line([dd_x + 368, rod_y_top + 18, dd_x + 360, rod_y_top + 5], fill=C_COPPER, width=3) # Left prong
    draw.line([dd_x + 392, rod_y_top + 18, dd_x + 400, rod_y_top + 5], fill=C_COPPER, width=3) # Right prong

    # 70mm2 Annealed Copper Down-Conductor Cable routed inside
    draw.line([dd_x + 376, rod_y_top + 25, dd_x + 376, dd_y + dt_h - 20], fill=(210, 110, 50), width=3)

    # Callouts for Detail D
    draw_callout_line(dd_x + 380, rod_y_top + 10, dd_x + 30, dd_y + 80, "EARLY STREAMER EMISSION (ESE) TERMINAL", "SOLID ELECTROLYTIC COPPER DIA 32mm")
    draw_callout_line(dd_x + 330, b_center_y, dd_x + 30, dd_y + 160, "DUAL AIRCRAFT WARNING BEACON", "ICAO / FAA TYPE L-856 HIGH-INTENSITY LED")
    draw_callout_line(dd_x + 335, mast_y_top - 6, dd_x + 30, dd_y + 240, "BOLTED FLANGE CAP MOUNTING", "M24 Gr 10.9 HDG BOLTS (6x)")
    draw_callout_line(dd_x + 376, mast_y_top + 60, dd_x + 30, dd_y + 320, "70mm2 COPPER DOWN-CONDUCTOR", "ROUTED INSIDE STEEL MAST TO GROUND GRID")

    # =========================================================================
    # 8. GENERAL NOTES, STRUCTURAL SPECIFICATIONS & SCHEDULE
    # =========================================================================
    gn_x, gn_y = 1480, 1100
    gn_w, gn_h = 1550, 480
    draw.rectangle([gn_x, gn_y, gn_x + gn_w, gn_y + gn_h], fill=(252, 253, 255), outline=C_BORDER, width=2)
    # Header
    draw.rectangle([gn_x, gn_y, gn_x + gn_w, gn_y + 36], fill=(235, 240, 248), outline=C_BORDER, width=1)
    draw.text((gn_x + 15, gn_y + 7), "GENERAL STRUCTURAL STEEL NOTES & TECHNICAL SPECIFICATIONS", fill=C_BORDER, font=f_header)

    notes_col1 = [
        ("1. GOVERNING CODES & DESIGN STANDARDS:", True),
        ("   - Structural Steel Design: AISC 360-16 / Eurocode 3 (EN 1993-1-1 / EN 1993-1-8).", False),
        ("   - Wind Engineering: ASCE 7-10 wind tunnel tested boundary layer profile (RWDI).", False),
        ("   - Structural Welding: AWS D1.1/D1.1M Structural Welding Code - Steel (CJP 100% UT).", False),
        ("   - Lightning Protection: NFPA 780 & IEC 62305 Class 1 Supertall Protection System.", False),
        ("   - Aviation Obstruction Marking: ICAO Annex 14 / FAA AC 70/7460-1L (Dual White/Red).", False),
        ("", False),
        ("2. STRUCTURAL STEEL MATERIAL GRADES:", True),
        ("   - Hollow Structural Sections (HSS Pipe): EN 10210 S355NH / S460NH (Fy = 460 MPa).", False),
        ("   - Built-Up Plates & Transition Shoes: ASTM A572 Grade 50 / Grade 65 & EN 10025-2 S460.", False),
        ("   - High-Strength Structural Bolts: ISO 898-1 Class 10.9 / ASTM A490 pre-tensioned.", False),
        ("   - Anchor Rods: ASTM F1554 Grade 105 with heavy hex nuts and welded embedment plates.", False),
        ("   - Concrete Core Wall: Ultra-high strength C80 concrete (fck = 80 MPa / f'c = 11,600 psi).", False)
    ]

    notes_col2 = [
        ("3. TELESCOPIC STRAND JACKING PROCEDURE (L163 - +828.00M):", True),
        ("   - Total Spire Weight: 4,000 metric tonnes, assembled inside core in 22 segmented tiers.", False),
        ("   - Jacking System: 4x 500-tonne computer-synchronized hydraulic strand jacks (VLS).", False),
        ("   - Dynamic Wind Lock: Mechanical locking shim wedges engaged at every 250mm stroke.", False),
        ("   - Final Pinning: Full penetration welded collar diaphragms at +630.00m spire base.", False),
        ("", False),
        ("4. CORROSION PROTECTION & LIGHTNING GROUNDING:", True),
        ("   - Spire Cladding: Architectural Grade 316L embossed stainless steel (satin finish).", False),
        ("   - Steel Framing: Hot-dip galvanized to ASTM A123 (min 85 microns) + 2x Epoxy-Polyamide.", False),
        ("   - Lightning Down-Conductors: 2x 70mm2 annealed electrolytic copper conductors bonded", False),
        ("     at every 15m interval to the structural steel column and grounded to bedrock piles.", False),
        ("   - Damper Calibration: 400-tonne Tuned Mass Damper tuned to first 3 spire sway modes.", False)
    ]

    ny_start = gn_y + 48
    for i, (txt, is_b) in enumerate(notes_col1):
        fnt = f_body_bold if is_b else f_body
        clr = C_BORDER if is_b else C_NOTE
        draw.text((gn_x + 20, ny_start + i * 23), txt, fill=clr, font=fnt)

    for i, (txt, is_b) in enumerate(notes_col2):
        fnt = f_body_bold if is_b else f_body
        clr = C_BORDER if is_b else C_NOTE
        draw.text((gn_x + 780, ny_start + i * 23), txt, fill=clr, font=fnt)

    # =========================================================================
    # 9. KEY PLAN & TOWER SCHEMATIC ELEVATION LOCATOR
    # =========================================================================
    kp_x, kp_y = 1480, 1600
    kp_w, kp_h = 750, 680
    draw.rectangle([kp_x, kp_y, kp_x + kp_w, kp_y + kp_h], fill=(255, 255, 255), outline=C_BORDER, width=2)
    # Header
    draw.rectangle([kp_x, kp_y, kp_x + kp_w, kp_y + 36], fill=(235, 240, 248), outline=C_BORDER, width=1)
    draw.text((kp_x + 15, kp_y + 8), "KEY PLAN - BURJ KHALIFA TOTAL ELEVATION LOCATOR", fill=C_BORDER, font=f_header)

    # Schematic Full Tower Silhouette (0.0m to 828.0m)
    # Scaled to fit height 580 px
    kcx = kp_x + 160
    ky_base = kp_y + 640
    ky_top = kp_y + 70

    # Draw Full Tower Outline
    draw.line([kcx - 70, ky_base, kcx + 70, ky_base], fill=C_BORDER, width=3) # Ground line
    # Stepped tiers silhouette
    tower_schematic = [
        (60, 0), (60, 150), (48, 150), (48, 275), (38, 275), (38, 400),
        (28, 400), (28, 480), (18, 480), (18, 567), (12, 567), (9, 596),
        (6, 630), (4, 700), (3, 760), (2, 810), (1, 828)
    ]
    t_poly = []
    for w_m, z_m in tower_schematic:
        sy = ky_base - (z_m / 828.0) * (ky_base - ky_top)
        t_poly.append((kcx + w_m * 0.9, sy))
    for w_m, z_m in reversed(tower_schematic):
        sy = ky_base - (z_m / 828.0) * (ky_base - ky_top)
        t_poly.append((kcx - w_m * 0.9, sy))
    draw.polygon(t_poly, fill=(240, 244, 250), outline=C_BORDER)

    # Highlight Detail Scope Zone (+567.30m to +828.0m) in Red / Shaded Hatched
    sy_scope_bot = ky_base - (567.30 / 828.0) * (ky_base - ky_top)
    sy_scope_top = ky_top
    draw.rectangle([kcx - 85, sy_scope_top - 6, kcx + 85, sy_scope_bot], fill=None, outline=(220, 30, 30), width=2)
    # Red hatched box indicating detail scope
    for hy in range(int(sy_scope_top), int(sy_scope_bot), 10):
        draw.line([kcx - 85, hy, kcx + 85, hy], fill=(240, 180, 180), width=1)
    draw.polygon(t_poly, fill=None, outline=C_BORDER) # Re-stroke

    # Scope Label
    draw.line([kcx + 85, (sy_scope_top + sy_scope_bot) * 0.5, kp_x + 380, (sy_scope_top + sy_scope_bot) * 0.5], fill=(200, 30, 30), width=2)
    draw.polygon([(kcx + 85, (sy_scope_top + sy_scope_bot) * 0.5), (kcx + 93, (sy_scope_top + sy_scope_bot) * 0.5 - 4), (kcx + 93, (sy_scope_top + sy_scope_bot) * 0.5 + 4)], fill=(200, 30, 30))
    draw.text((kp_x + 390, (sy_scope_top + sy_scope_bot) * 0.5 - 28), "DETAIL DRAWING SCOPE ZONE", fill=(200, 30, 30), font=f_body_bold)
    draw.text((kp_x + 390, (sy_scope_top + sy_scope_bot) * 0.5 - 6), "LEVEL 156 (+567.30M) TO PINNACLE (+828.00M)", fill=C_BORDER, font=f_small)
    draw.text((kp_x + 390, (sy_scope_top + sy_scope_bot) * 0.5 + 14), "SEE MAIN SECTION 01 & DETAILS A, B, C, D", fill=C_DIM, font=f_micro)

    # Key Plan Info Table
    kp_info = [
        ("TOTAL BUILDING HEIGHT:", "+828.000 m (2,717 ft)"),
        ("OCCUPIED FLOORS:", "163 Habitable Floors"),
        ("SPIRE STRUCTURAL LENGTH:", "190.000 m (22 Tiers)"),
        ("SPIRE STEEL WEIGHT:", "4,000+ Metric Tonnes"),
        ("CONCRETE TRANSITION:", "Level 156 (+567.30 m)"),
        ("TOP HABITABLE DECK:", "Level 163 (+596.50 m)"),
        ("STRUCTURAL SYSTEM:", "Buttressed Core + Steel Spire")
    ]
    for idx, (label, val) in enumerate(kp_info):
        draw.text((kp_x + 380, kp_y + 360 + idx * 30), label, fill=C_BORDER, font=f_body_bold)
        draw.text((kp_x + 380, kp_y + 378 + idx * 30), val, fill=C_DIM, font=f_dim)

    # =========================================================================
    # 10. OFFICIAL TITLE BLOCK (KOP GAMBAR TEKNIK REKAYASA STRUKTUR)
    # =========================================================================
    tb_x, tb_y = 2270, 1600
    tb_w, tb_h = 1450, 680
    draw.rectangle([tb_x, tb_y, tb_x + tb_w, tb_y + tb_h], fill=(255, 255, 255), outline=C_BORDER, width=3)

    # Row 1: Client & Project Identity
    draw.rectangle([tb_x, tb_y, tb_x + tb_w, tb_y + 110], fill=(245, 248, 252), outline=C_BORDER, width=2)
    # Client Logo Box
    draw.rectangle([tb_x + 15, tb_y + 12, tb_x + 220, tb_y + 98], fill=(30, 40, 55), outline=C_BORDER, width=2)
    draw.text((tb_x + 35, tb_y + 25), "EMAAR", fill=(255, 255, 255), font=f_title)
    draw.text((tb_x + 35, tb_y + 65), "PROPERTIES PJSC", fill=(200, 210, 225), font=f_small)

    draw.text((tb_x + 240, tb_y + 18), "PROJECT:", fill=C_NOTE, font=f_small)
    draw.text((tb_x + 240, tb_y + 38), "BURJ KHALIFA - THE SPIRE & PINNACLE STRUCTURAL DETAIL", fill=C_BORDER, font=f_title)
    draw.text((tb_x + 240, tb_y + 78), "DOWNTOWN DUBAI, UNITED ARAB EMIRATES | GLOBAL LANDMARK DEVELOPMENT", fill=C_DIM, font=f_body)

    # Row 2: Lead Architect & Structural Engineering Sign-off
    draw.rectangle([tb_x, tb_y + 110, tb_x + tb_w, tb_y + 230], fill=(255, 255, 255), outline=C_BORDER, width=2)
    draw.text((tb_x + 20, tb_y + 118), "ARCHITECT & STRUCTURAL ENGINEER OF RECORD:", fill=C_NOTE, font=f_small)
    draw.text((tb_x + 20, tb_y + 138), "SKIDMORE, OWINGS & MERRILL LLP (SOM)", fill=C_BORDER, font=f_sub)
    draw.text((tb_x + 20, tb_y + 168), "William F. Baker, PE, SE, FIStructE (Lead Structural Partner)", fill=C_NOTE, font=f_body)
    draw.text((tb_x + 20, tb_y + 192), "Adrian D. Smith, FAIA, RIBA (Consulting Design Partner)", fill=C_NOTE, font=f_body)

    # BIM Integration & Modeling Engine Box
    draw.rectangle([tb_x + 720, tb_y + 118, tb_x + tb_w - 20, tb_y + 220], fill=(245, 247, 250), outline=C_BORDER, width=1)
    draw.text((tb_x + 735, tb_y + 128), "BIM MODELING & COMPUTATIONAL ENGINE:", fill=C_NOTE, font=f_small)
    draw.text((tb_x + 735, tb_y + 148), "AIGRAV BIM AGENTIC PLATFORM 2.5", fill=C_BORDER, font=f_header)
    draw.text((tb_x + 735, tb_y + 174), "Autodesk Revit 2026.5 API Native Integration (Single-Threaded Safe Engine)", fill=C_DIM, font=f_micro)
    draw.text((tb_x + 735, tb_y + 194), "High-Density LOD 350-400 Structural Steel & Glazed Curtain Model", fill=C_NOTE, font=f_micro)

    # Row 3: Sheet Title & Drawing Type
    draw.rectangle([tb_x, tb_y + 230, tb_x + tb_w, tb_y + 370], fill=(240, 244, 250), outline=C_BORDER, width=2)
    draw.text((tb_x + 20, tb_y + 242), "SHEET TITLE / JUDUL GAMBAR:", fill=C_NOTE, font=f_small)
    draw.text((tb_x + 20, tb_y + 266), "STRUCTURAL & ARCHITECTURAL DETAIL SECTION", fill=C_BORDER, font=f_title)
    draw.text((tb_x + 20, tb_y + 306), "TOWER PEAK, MECHANICAL PLANT & TELESCOPIC SPIRE (+567.30M TO +828.00M)", fill=C_DIM, font=f_header)
    draw.text((tb_x + 20, tb_y + 338), "INCLUDES CONCRETE-STEEL TRANSITION, STRAND JACKING, CATWALKS & PINNACLE ARRESTER", fill=C_NOTE, font=f_body)

    # Row 4: Revision Table & Engineering Status
    draw.rectangle([tb_x, tb_y + 370, tb_x + 720, tb_y + 530], fill=(255, 255, 255), outline=C_BORDER, width=1)
    draw.text((tb_x + 15, tb_y + 378), "REVISION RECORD / CATATAN REVISI:", fill=C_NOTE, font=f_small)
    draw.line([tb_x, tb_y + 400, tb_x + 720, tb_y + 400], fill=C_BORDER, width=1)
    draw.text((tb_x + 15, tb_y + 406), "REV", fill=C_NOTE, font=f_micro)
    draw.text((tb_x + 60, tb_y + 406), "DESCRIPTION", fill=C_NOTE, font=f_micro)
    draw.text((tb_x + 480, tb_y + 406), "DATE", fill=C_NOTE, font=f_micro)
    draw.text((tb_x + 600, tb_y + 406), "APPROVED", fill=C_NOTE, font=f_micro)
    draw.line([tb_x, tb_y + 424, tb_x + 720, tb_y + 424], fill=C_BORDER, width=1)

    revisions = [
        ("0", "ISSUED FOR CONSTRUCTION SHOP DRAWING", "13-NOV-2026", "WFB / AIG"),
        ("A", "PEAK SPIRE ENGINEERING DETAIL REVIEW", "12-NOV-2026", "SOM / QA"),
        ("B", "SCHEMATIC STRUCTURAL COORDINATION", "10-NOV-2026", "EMAAR / PM")
    ]
    for r_idx, (r_no, r_desc, r_date, r_appr) in enumerate(revisions):
        ry = tb_y + 432 + r_idx * 30
        draw.text((tb_x + 20, ry), r_no, fill=C_BORDER, font=f_small)
        draw.text((tb_x + 60, ry), r_desc, fill=C_BORDER, font=f_small)
        draw.text((tb_x + 480, ry), r_date, fill=C_DIM, font=f_dim)
        draw.text((tb_x + 610, ry), r_appr, fill=C_BORDER, font=f_body_bold)

    # Professional Engineering Approval Stamp
    draw.rectangle([tb_x + 720, tb_y + 370, tb_x + tb_w, tb_y + 530], fill=(255, 255, 255), outline=C_BORDER, width=1)
    # Red Stamp Box
    draw.rectangle([tb_x + 740, tb_y + 385, tb_x + tb_w - 20, tb_y + 515], outline=(190, 30, 30), width=3)
    draw.rectangle([tb_x + 744, tb_y + 389, tb_x + tb_w - 24, tb_y + 511], outline=(190, 30, 30), width=1)
    draw.text((tb_x + 765, tb_y + 398), "APPROVED FOR CONSTRUCTION", fill=(190, 30, 30), font=f_sub)
    draw.text((tb_x + 765, tb_y + 432), "DUBAI MUNICIPALITY - BUILDING PERMIT DEPT.", fill=(190, 30, 30), font=f_small)
    draw.text((tb_x + 765, tb_y + 456), "STRUCTURAL VERIFICATION: 100% CODE COMPLIANT", fill=(190, 30, 30), font=f_small)
    draw.text((tb_x + 765, tb_y + 480), "DATE: 13-NOV-2026 | SIGNED: CHIEF STRUCTURAL ENGINEER", fill=(190, 30, 30), font=f_dim)

    # Row 5: Metadata (Drawing Number, Sheet Number, Scale, Date, Status)
    draw.rectangle([tb_x, tb_y + 530, tb_x + tb_w, tb_y + tb_h], fill=(245, 248, 252), outline=C_BORDER, width=2)

    meta_cells = [
        ("DRAWING NUMBER / NO. GAMBAR:", "DWG-STR-SEC-828-01", 380),
        ("SHEET NUMBER:", "S-828", 180),
        ("SCALE / SKALA:", "1:200 & 1:25", 220),
        ("CAD/BIM DISCIPLINE:", "STRUCTURAL (STR)", 240),
        ("STATUS:", "FINAL SHOP DWG", 210),
        ("SHEET SIZE:", "A1 (841 x 594)", 220)
    ]
    cur_mx = tb_x
    for label, val, cw in meta_cells:
        draw.line([cur_mx, tb_y + 530, cur_mx, tb_y + tb_h], fill=C_BORDER, width=1)
        draw.text((cur_mx + 12, tb_y + 545), label, fill=C_NOTE, font=f_micro)
        draw.text((cur_mx + 12, tb_y + 575), val, fill=C_BORDER, font=f_header)
        cur_mx += cw

    # 11. Save High-Resolution Image
    os.makedirs(os.path.dirname(output_path), exist_ok=True)
    img.save(output_path, "PNG", dpi=(300, 300))
    print(f"Engineering Drawing successfully created at: {output_path}")

if __name__ == "__main__":
    out = r"C:\Users\sapuser\.gemini\antigravity\brain\f3cffd9e-8311-48be-9d3a-6b90cb682998\burj_khalifa_spire_section_engineering_drawing.png"
    create_spire_engineering_drawing(out)
