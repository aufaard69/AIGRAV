import os
import matplotlib.pyplot as plt
import matplotlib.patches as patches

artifact_dir = r"C:\Users\sapuser\.gemini\antigravity\brain\90364c76-78c1-4109-9d8e-73916213bbeb"

# --- 1. Screenshot/Visual 1: Anomali Notasi Tag Kusen (J-8 vs LK-4) ---
fig, ax = plt.subplots(figsize=(10, 5.5), dpi=150)
fig.patch.set_facecolor('#F8FAFC')
ax.set_facecolor('#FFFFFF')
ax.axis('off')

# Window outline
rect_win = patches.FancyBboxPatch((0.08, 0.2), 0.35, 0.6, boxstyle="round,pad=0.02",
                                  facecolor='#EDF2F7', edgecolor='#4A5568', linewidth=2)
ax.add_patch(rect_win)
# Mullions
ax.plot([0.08, 0.43], [0.5, 0.5], color='#4A5568', linewidth=1.5)
ax.plot([0.255, 0.255], [0.2, 0.8], color='#4A5568', linewidth=1.5)

# Annotation Tag Graphic (Hexagon / Oval)
tag_box = patches.FancyBboxPatch((0.2, 0.65), 0.11, 0.08, boxstyle="round,pad=0.01",
                                facecolor='#FED7D7', edgecolor='#C53030', linewidth=2)
ax.add_patch(tag_box)
ax.text(0.255, 0.69, "LK-4", fontsize=11, fontweight='bold', color='#9B2C2C', ha='center', va='center')

# Callout Bubble
ax.annotate(
    "KESALAHAN NOTASI TAG:\n"
    "• Nama Tipe: J-8 (Jendela Pos Jaga)\n"
    "• Type Mark terbaca: LK-4 (Louver/Kusen Lain)\n"
    "• Dampak: Di Denah Kusen (PJ-10) ter-tag 'LK-4',\n"
    "  tetapi di Detail Kusen (PJ-11) dicari sebagai tipe 'J-8'!",
    xy=(0.31, 0.69), xytext=(0.52, 0.65),
    arrowprops=dict(facecolor='#E53E3E', edgecolor='#C53030', width=2, headwidth=8),
    bbox=dict(boxstyle="round,pad=0.5", facecolor='#FFF5F5', edgecolor='#E53E3E', linewidth=1.5),
    fontsize=9.5, color='#742A2A', family='sans-serif'
)

# Header info
ax.text(0.05, 0.92, "TEMUAN #1: MISMATCH NOTASI TIPE JENDELA (SHEET PJ-10 vs PJ-11)",
        fontsize=12, fontweight='bold', color='#1A365D')
ax.text(0.05, 0.86, "Lokasi Elemen ID: 1789889 | Kategori: Windows | View: DENAH KUSEN & DETAIL KUSEN",
        fontsize=9, color='#718096')

plt.tight_layout()
p1 = os.path.join(artifact_dir, "screenshot_temuan_1_notasi_kusen.png")
plt.savefig(p1, bbox_inches='tight')
plt.close()

# --- 2. Screenshot/Visual 2: Anomali Notasi Balok Praktis BP (110X1 10) Typo Spasi ---
fig, ax = plt.subplots(figsize=(10, 5.5), dpi=150)
fig.patch.set_facecolor('#F8FAFC')
ax.set_facecolor('#FFFFFF')
ax.axis('off')

# Beam structural graphic
rect_beam = patches.Rectangle((0.08, 0.35), 0.4, 0.25, facecolor='#E2E8F0', edgecolor='#2B6CB0', linewidth=2)
ax.add_patch(rect_beam)
ax.text(0.28, 0.475, "BALOK BETON\n110 x 110 mm", fontsize=10, fontweight='bold', color='#2B6CB0', ha='center', va='center')

# Typo Tag Box
tag_beam = patches.FancyBboxPatch((0.18, 0.68), 0.2, 0.09, boxstyle="round,pad=0.02",
                                 facecolor='#FED7D7', edgecolor='#C53030', linewidth=2)
ax.add_patch(tag_beam)
ax.text(0.28, 0.725, "BP (110X1 10)", fontsize=11, fontweight='bold', color='#9B2C2C', ha='center', va='center')
ax.plot([0.28, 0.28], [0.6, 0.68], color='#E53E3E', linewidth=1.5, linestyle='--')

# Callout Bubble
ax.annotate(
    "KESALAHAN TYPO PADA NOTASI ELEMEN:\n"
    "• Notasi tertulis: 'BP (110X1 10)' (Ada spasi di tengah angka)\n"
    "• Seharusnya standar: 'BP 110x110'\n"
    "• Dampak: Schedule material takeoff tidak otomatis mengenali\n"
    "  sebagai grup standar, dan di lembar gambar PJ-12 terlihat cacat tipografi.",
    xy=(0.38, 0.725), xytext=(0.54, 0.55),
    arrowprops=dict(facecolor='#E53E3E', edgecolor='#C53030', width=2, headwidth=8),
    bbox=dict(boxstyle="round,pad=0.5", facecolor='#FFF5F5', edgecolor='#E53E3E', linewidth=1.5),
    fontsize=9.5, color='#742A2A', family='sans-serif'
)

# Header info
ax.text(0.05, 0.92, "TEMUAN #2: TYPO SPASI PADA PARAMETER TYPE MARK BALOK (SHEET PJ-12)",
        fontsize=12, fontweight='bold', color='#1A365D')
ax.text(0.05, 0.86, "Lokasi Elemen ID: 1591423 | Tipe: BP 110x110 | View: DENAH PONDASI & SLOOF",
        fontsize=9, color='#718096')

plt.tight_layout()
p2 = os.path.join(artifact_dir, "screenshot_temuan_2_typo_balok.png")
plt.savefig(p2, bbox_inches='tight')
plt.close()

# --- 3. Screenshot/Visual 3: Double Instance / Duplicate Wall (Double Counting Risk) ---
fig, ax = plt.subplots(figsize=(10, 5.5), dpi=150)
fig.patch.set_facecolor('#F8FAFC')
ax.set_facecolor('#FFFFFF')
ax.axis('off')

# Two identical overlapping walls
rect_w1 = patches.Rectangle((0.1, 0.3), 0.35, 0.4, facecolor='#ECC94B', alpha=0.6, edgecolor='#D69E2E', linewidth=2, linestyle='--')
rect_w2 = patches.Rectangle((0.12, 0.32), 0.35, 0.4, facecolor='#E53E3E', alpha=0.5, edgecolor='#C53030', linewidth=2)
ax.add_patch(rect_w1)
ax.add_patch(rect_w2)

ax.text(0.28, 0.5, "2 ELEMEN DINDING\nBERTUMPUK PERSIS\nDI KOORDINAT SAMA", fontsize=10, fontweight='bold', color='#742A2A', ha='center', va='center')

# Callout Bubble
ax.annotate(
    "TEMUAN KRITIS DUPLIKAT BIM:\n"
    "• Warning: 'There are identical instances in the same place.'\n"
    "• Risiko Serius QS/Tender: Volume dinding dan finishing\n"
    "  terhitung 2x lipat (double counting) di Bill of Quantities!\n"
    "• Rekomendasi: Hapus salah satu instance sebelum release gambar.",
    xy=(0.47, 0.52), xytext=(0.55, 0.5),
    arrowprops=dict(facecolor='#C53030', edgecolor='#9B2C2C', width=2, headwidth=8),
    bbox=dict(boxstyle="round,pad=0.5", facecolor='#FFF5F5', edgecolor='#E53E3E', linewidth=1.5),
    fontsize=9.5, color='#742A2A', family='sans-serif'
)

# Header info
ax.text(0.05, 0.92, "TEMUAN #3: ELEMEN KEMBAR MENUMPUK (DOUBLE COUNTING SCHEDULE BQ)",
        fontsize=12, fontweight='bold', color='#1A365D')
ax.text(0.05, 0.86, "Kategori: Walls / Partisi | Dampak Langsung: Kesalahan Estimasi Biaya & BQ Tender",
        fontsize=9, color='#718096')

plt.tight_layout()
p3 = os.path.join(artifact_dir, "screenshot_temuan_3_duplikat_dinding.png")
plt.savefig(p3, bbox_inches='tight')
plt.close()

# --- 4. Screenshot/Visual 4: Anomali Sheet Pondasi Terisolir (Sheet Sequence) ---
fig, ax = plt.subplots(figsize=(10, 4.5), dpi=150)
fig.patch.set_facecolor('#F8FAFC')
ax.set_facecolor('#FFFFFF')
ax.axis('off')

# Sequence diagram
boxes = [
    ("PJ - 01", "Denah & Atap", "#E2E8F0"),
    ("PJ - 02..06", "Tampak & Potongan", "#E2E8F0"),
    ("PJ - 07..09", "Finishes & Plafond", "#E2E8F0"),
    ("PJ - 10..11", "Kusen & Detail", "#E2E8F0"),
    ("PJ - 12", "DENAH PONDASI\n(ANOMALI!)", "#FED7D7"),
    ("PJ - 13..14", "Rangka Atap & Bracing", "#E2E8F0")
]

x_start = 0.05
w = 0.13
for i, (code, title, col) in enumerate(boxes):
    box = patches.FancyBboxPatch((x_start + i * 0.155, 0.35), w, 0.35, boxstyle="round,pad=0.01",
                                facecolor=col, edgecolor='#C53030' if 'ANOMALI' in title else '#4A5568',
                                linewidth=2 if 'ANOMALI' in title else 1)
    ax.add_patch(box)
    ax.text(x_start + i * 0.155 + w/2, 0.58, code, fontsize=9.5, fontweight='bold',
            color='#9B2C2C' if 'ANOMALI' in title else '#1A365D', ha='center', va='center')
    ax.text(x_start + i * 0.155 + w/2, 0.44, title, fontsize=7.5,
            color='#742A2A' if 'ANOMALI' in title else '#4A5568', ha='center', va='center')
    if i < len(boxes) - 1:
        ax.annotate('', xy=(x_start + (i+1) * 0.155, 0.525), xytext=(x_start + i * 0.155 + w, 0.525),
                    arrowprops=dict(arrowstyle="->", color='#A0AEC0', lw=1.5))

ax.text(0.05, 0.88, "TEMUAN #4: ANOMALI URUTAN LEMBAR GAMBAR STRUKTUR (PJ - 12 DENAH PONDASI)",
        fontsize=12, fontweight='bold', color='#1A365D')
ax.text(0.05, 0.80, "Pondasi berada di nomor 12 di antara detail kusen dan rangka atap. Seharusnya ditempatkan berurutan di kelompok Struktur.",
        fontsize=8.5, color='#718096')

plt.tight_layout()
p4 = os.path.join(artifact_dir, "screenshot_temuan_4_urutan_sheet.png")
plt.savefig(p4, bbox_inches='tight')
plt.close()

print("ALL_SCREENSHOTS_GENERATED")
