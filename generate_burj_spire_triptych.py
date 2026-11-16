"""
Generate Master Presentation Board:
Side-by-side comparison of 3D Structural Cutaway & 2D CAD Engineering Shop Drawing Sheet
"""
import os
from PIL import Image, ImageDraw, ImageFont

def create_master_board():
    dir_path = r"C:\Users\sapuser\.gemini\antigravity\brain\f3cffd9e-8311-48be-9d3a-6b90cb682998"
    p3d_path = os.path.join(dir_path, "burj_khalifa_spire_3d_cutaway.jpg")
    p2d_path = os.path.join(dir_path, "burj_khalifa_spire_section_engineering_drawing.png")
    out_path = os.path.join(dir_path, "burj_khalifa_spire_master_triptych.png")

    img_3d = Image.open(p3d_path).convert("RGB")
    img_2d = Image.open(p2d_path).convert("RGB")

    # Target Canvas: 3840 x 1600 (Ultrawide Presentation Banner)
    W, H = 3840, 1600
    board = Image.new("RGB", (W, H), (18, 22, 28)) # Dark modern theme background
    draw = ImageDraw.Draw(board)

    # Fonts
    font_bold = "C:\\Windows\\Fonts\\arialbd.ttf"
    font_regular = "C:\\Windows\\Fonts\\arial.ttf"
    f_title = ImageFont.truetype(font_bold, 36)
    f_sub = ImageFont.truetype(font_regular, 20)
    f_header = ImageFont.truetype(font_bold, 24)
    f_meta = ImageFont.truetype(font_regular, 16)

    # Header Bar
    draw.rectangle([0, 0, W, 100], fill=(28, 34, 44))
    draw.line([0, 100, W, 100], fill=(0, 160, 230), width=3)
    draw.text((50, 20), "BURJ KHALIFA — TOWER PEAK & TELESCOPIC SPIRE ENGINEERING DETAIL", fill=(255, 255, 255), font=f_title)
    draw.text((50, 66), "LEVEL 156 (+567.30M) TO PINNACLE TIP (+828.00M) | 3D STRUCTURAL CUTAWAY vs 2D CAD SHOP DRAWING (DWG-STR-SEC-828-01)", fill=(180, 205, 230), font=f_sub)

    # Left Panel: 3D Structural Cutaway
    # Available area: X in [50, 1450], Y in [130, 1530] (Width 1400, Height 1400)
    panel_left_w = 1420
    panel_left_h = 1380
    box_left = [50, 130, 50 + panel_left_w, 130 + panel_left_h]
    draw.rectangle(box_left, fill=(24, 30, 40), outline=(50, 60, 75), width=2)
    # Header tag
    draw.rectangle([50, 130, 50 + panel_left_w, 180], fill=(35, 45, 60))
    draw.text((70, 142), "PANEL 01: 3D STRUCTURAL CUTAWAY & TECTONIC ANATOMY (LUMION ARCHVIZ)", fill=(0, 195, 255), font=f_header)

    # Resize img_3d maintaining aspect ratio
    img_3d_ratio = img_3d.width / img_3d.height
    target_3d_h = panel_left_h - 60
    target_3d_w = int(target_3d_h * img_3d_ratio)
    if target_3d_w > panel_left_w - 20:
        target_3d_w = panel_left_w - 20
        target_3d_h = int(target_3d_w / img_3d_ratio)
    img_3d_resized = img_3d.resize((target_3d_w, target_3d_h), Image.Resampling.LANCZOS)
    pos_3d_x = 50 + (panel_left_w - target_3d_w) // 2
    pos_3d_y = 190 + (panel_left_h - 60 - target_3d_h) // 2
    board.paste(img_3d_resized, (pos_3d_x, pos_3d_y))

    # Right Panel: 2D CAD Detail Shop Drawing Sheet
    # Available area: X in [1520, 3790] (Width 2270, Height 1380)
    panel_right_w = 2270
    panel_right_h = 1380
    box_right = [1520, 130, 1520 + panel_right_w, 130 + panel_right_h]
    draw.rectangle(box_right, fill=(24, 30, 40), outline=(50, 60, 75), width=2)
    # Header tag
    draw.rectangle([1520, 130, 1520 + panel_right_w, 180], fill=(35, 45, 60))
    draw.text((1540, 142), "PANEL 02: 2D CONSTRUCTION SHOP DRAWING SHEET S-828 (AUTOCAD/REVIT FORMAT A1 - 1:200 & 1:25)", fill=(0, 220, 160), font=f_header)

    # Resize img_2d maintaining aspect ratio
    img_2d_ratio = img_2d.width / img_2d.height
    target_2d_w = panel_right_w - 20
    target_2d_h = int(target_2d_w / img_2d_ratio)
    if target_2d_h > panel_right_h - 60:
        target_2d_h = panel_right_h - 60
        target_2d_w = int(target_2d_h * img_2d_ratio)
    img_2d_resized = img_2d.resize((target_2d_w, target_2d_h), Image.Resampling.LANCZOS)
    pos_2d_x = 1520 + (panel_right_w - target_2d_w) // 2
    pos_2d_y = 190 + (panel_right_h - 60 - target_2d_h) // 2
    board.paste(img_2d_resized, (pos_2d_x, pos_2d_y))

    # Footer Metadata Bar
    draw.rectangle([0, H - 50, W, H], fill=(14, 18, 24))
    draw.text((50, H - 35), "PROJECT: BURJ KHALIFA TOWER | CLIENT: EMAAR PROPERTIES PJSC | ARCHITECT & STR ENGINEER: SOM (WILLIAM F. BAKER) | BIM ENGINE: AIGRAV 2.5", fill=(140, 155, 175), font=f_meta)
    draw.text((W - 600, H - 35), "OFFICIAL STATUS: 🟢 APPROVED FOR CONSTRUCTION (DWG-STR-SEC-828-01)", fill=(0, 220, 160), font=f_meta)

    board.save(out_path, "PNG", dpi=(300, 300))
    print(f"Master Presentation Board created at: {out_path}")

if __name__ == "__main__":
    create_master_board()
