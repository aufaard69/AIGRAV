import os
from reportlab.lib import colors
from reportlab.lib.pagesizes import A4
from reportlab.platypus import (
    SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle, PageBreak, KeepTogether, HRFlowable, Image
)
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib.units import cm
from reportlab.pdfgen import canvas

class NumberedCanvas(canvas.Canvas):
    def __init__(self, *args, **kwargs):
        super(NumberedCanvas, self).__init__(*args, **kwargs)
        self._saved_page_states = []

    def showPage(self):
        self._saved_page_states.append(dict(self.__dict__))
        self._startPage()

    def save(self):
        num_pages = len(self._saved_page_states)
        for state in self._saved_page_states:
            self.__dict__.update(state)
            self.draw_page_decorations(num_pages)
            super(NumberedCanvas, self).showPage()
        super(NumberedCanvas, self).save()

    def draw_page_decorations(self, page_count):
        self.saveState()
        self.setFont("Helvetica", 8)
        self.setFillColor(colors.HexColor("#718096"))
        
        # Header (Pages > 1)
        if self._pageNumber > 1:
            self.drawString(1.8 * cm, 28.5 * cm, "SITE ENGINEERING QUALITY AUDIT — LAMPIRAN KESALAHAN NOTASI & DETAILING")
            self.setStrokeColor(colors.HexColor("#CBD5E0"))
            self.setLineWidth(0.5)
            self.line(1.8 * cm, 28.3 * cm, 19.2 * cm, 28.3 * cm)
            
        # Footer
        footer_text = f"Halaman {self._pageNumber} dari {page_count}"
        self.drawRightString(19.2 * cm, 1.2 * cm, footer_text)
        self.drawString(1.8 * cm, 1.2 * cm, "NUAS-FT-PN-PJ4 — LAMPIRAN BUKTI VISUAL TEMUAN SEBELUM RILIS")
        self.setStrokeColor(colors.HexColor("#CBD5E0"))
        self.setLineWidth(0.5)
        self.line(1.8 * cm, 1.5 * cm, 19.2 * cm, 1.5 * cm)
        self.restoreState()

def create_full_report(output_path, img_dir):
    doc = SimpleDocTemplate(
        output_path,
        pagesize=A4,
        leftMargin=1.8 * cm,
        rightMargin=1.8 * cm,
        topMargin=2.2 * cm,
        bottomMargin=2.2 * cm
    )

    styles = getSampleStyleSheet()
    
    primary_color = colors.HexColor("#1A365D")   # Deep Navy
    secondary_color = colors.HexColor("#2B6CB0") # Medium Blue
    accent_red = colors.HexColor("#C53030")      # Warning Red
    neutral_dark = colors.HexColor("#2D3748")    # Charcoal
    neutral_light = colors.HexColor("#F7FAFC")
    border_color = colors.HexColor("#CBD5E0")

    title_style = ParagraphStyle(
        'DocTitle',
        parent=styles['Normal'],
        fontName='Helvetica-Bold',
        fontSize=18,
        leading=22,
        textColor=primary_color,
        spaceAfter=4
    )

    subtitle_style = ParagraphStyle(
        'DocSubTitle',
        parent=styles['Normal'],
        fontName='Helvetica',
        fontSize=9.5,
        leading=13,
        textColor=colors.HexColor("#4A5568"),
        spaceAfter=12
    )

    h1_style = ParagraphStyle(
        'Heading1_Custom',
        parent=styles['Normal'],
        fontName='Helvetica-Bold',
        fontSize=12,
        leading=16,
        textColor=primary_color,
        spaceBefore=12,
        spaceAfter=6,
        keepWithNext=True
    )

    body_style = ParagraphStyle(
        'Body_Custom',
        parent=styles['Normal'],
        fontName='Helvetica',
        fontSize=8.5,
        leading=12,
        textColor=neutral_dark,
        spaceAfter=3
    )

    table_header_style = ParagraphStyle(
        'TH_Style',
        parent=styles['Normal'],
        fontName='Helvetica-Bold',
        fontSize=8,
        leading=10,
        textColor=colors.white,
        alignment=1
    )

    table_cell_style = ParagraphStyle(
        'TD_Style',
        parent=styles['Normal'],
        fontName='Helvetica',
        fontSize=7.5,
        leading=10,
        textColor=neutral_dark
    )

    table_cell_center = ParagraphStyle(
        'TD_Center',
        parent=table_cell_style,
        alignment=1
    )

    elements = []

    # Title Block
    elements.append(Paragraph("LAPORAN QUALITY AUDIT & CROSS-CHECK DETAILING GAMBAR", title_style))
    elements.append(Paragraph("Verifikasi Kesalahan Notasi, Tagging & Anomali Detailing Sebelum Rilis Lapangan", subtitle_style))
    elements.append(HRFlowable(width="100%", thickness=2, color=primary_color, spaceBefore=0, spaceAfter=10))

    # Meta Table
    info_data = [
        [Paragraph("<b>Nama Proyek:</b>", body_style), Paragraph("NUAS-FT-PN-PJ4 (Pos Jaga & Parkir Motor)", body_style),
         Paragraph("<b>Tanggal Audit:</b>", body_style), Paragraph("09 Oktober 2026", body_style)],
        [Paragraph("<b>Paket Dokumen:</b>", body_style), Paragraph("05. Gambar For Tender / Konstruksi", body_style),
         Paragraph("<b>Reviewer:</b>", body_style), Paragraph("Site Engineer (BIM Quality Audit)", body_style)],
        [Paragraph("<b>File Model:</b>", body_style), Paragraph("NUAS-FT-PN-PJ4.rvt", body_style),
         Paragraph("<b>Status:</b>", body_style), Paragraph("<font color='#C53030'><b>PERLU PERBAIKAN NOTASI</b></font>", body_style)]
    ]
    t_info = Table(info_data, colWidths=[3.2*cm, 5.8*cm, 3.2*cm, 5.2*cm])
    t_info.setStyle(TableStyle([
        ('BACKGROUND', (0,0), (-1,-1), neutral_light),
        ('BOX', (0,0), (-1,-1), 1, border_color),
        ('INNERGRID', (0,0), (-1,-1), 0.5, border_color),
        ('TOPPADDING', (0,0), (-1,-1), 4),
        ('BOTTOMPADDING', (0,0), (-1,-1), 4),
        ('LEFTPADDING', (0,0), (-1,-1), 6),
        ('RIGHTPADDING', (0,0), (-1,-1), 6),
    ]))
    elements.append(t_info)
    elements.append(Spacer(1, 10))

    # SECTION 1: MATRIKS KESALAHAN NOTASI
    elements.append(Paragraph("1. Matriks Hasil Pemeriksaan Notasi & Detailing", h1_style))
    matrix_data = [
        [
            Paragraph("No", table_header_style),
            Paragraph("Sheet / Lokasi", table_header_style),
            Paragraph("Elemen ID", table_header_style),
            Paragraph("Kondisi Notasi Saat Ini", table_header_style),
            Paragraph("Seharusnya (Standar)", table_header_style),
            Paragraph("Dampak & Risiko Lapangan", table_header_style)
        ],
        [
            Paragraph("1", table_cell_center),
            Paragraph("<b>PJ - 10</b> (Denah Kusen)<br/>vs <b>PJ - 11</b> (Detail)", table_cell_style),
            Paragraph("1789889<br/>(Windows)", table_cell_center),
            Paragraph("• Nama Tipe: <b>J-8</b><br/>• Type Mark: <b>LK-4</b>", table_cell_style),
            Paragraph("Type Mark disamakan: <b>J-8</b>", table_cell_style),
            Paragraph("<font color='#C53030'><b>Mismatch Tag:</b></font> Di denah muncul LK-4, tetapi di tabel detail dicari sebagai J-8. Mandor/subkon kusen bingung.", table_cell_style)
        ],
        [
            Paragraph("2", table_cell_center),
            Paragraph("<b>PJ - 12</b><br/>(Denah Pondasi)", table_cell_style),
            Paragraph("1591423<br/>(Balok Sloof)", table_cell_center),
            Paragraph("Type Mark: <b>'BP (110X1 10)'</b><br/><i>(Typo spasi)</i>", table_cell_style),
            Paragraph("Type Mark: <b>'BP 110x110'</b>", table_cell_style),
            Paragraph("<font color='#C53030'><b>Typo Dimensi:</b></font> Format teks cacat di lembar kerja cetak & gagal grouping otomatis di Schedule Revit.", table_cell_style)
        ],
        [
            Paragraph("3", table_cell_center),
            Paragraph("Model 3D<br/>(Dinding / Partisi)", table_cell_style),
            Paragraph("Warnings<br/>Instance Ganda", table_cell_center),
            Paragraph("Dua dinding bertumpuk di koordinat identik.", table_cell_style),
            Paragraph("Hapus 1 dinding duplikat.", table_cell_style),
            Paragraph("<font color='#C53030'><b>Double Count BQ:</b></font> Volume dinding, plesteran, dan cat terhitung 2x lipat pada BQ tender.", table_cell_style)
        ],
        [
            Paragraph("4", table_cell_center),
            Paragraph("Project Browser<br/>(Daftar Sheet)", table_cell_style),
            Paragraph("1248068<br/>(PJ - 12)", table_cell_center),
            Paragraph("Pondasi berada di nomor 12 setelah kusen.", table_cell_style),
            Paragraph("Kelompokkan di awal struktur.", table_cell_style),
            Paragraph("<font color='#C53030'><b>Urutan Membingungkan:</b></font> Urutan cetak tidak runut menyulitkan cross-check bertahap di lapangan.", table_cell_style)
        ]
    ]

    t_mat = Table(matrix_data, colWidths=[0.7*cm, 3.2*cm, 2.2*cm, 3.8*cm, 3.2*cm, 4.3*cm])
    t_mat.setStyle(TableStyle([
        ('BACKGROUND', (0,0), (-1,0), primary_color),
        ('BOX', (0,0), (-1,-1), 1, primary_color),
        ('INNERGRID', (0,0), (-1,-1), 0.5, border_color),
        ('VALIGN', (0,0), (-1,-1), 'MIDDLE'),
        ('TOPPADDING', (0,0), (-1,-1), 4),
        ('BOTTOMPADDING', (0,0), (-1,-1), 4),
        ('ROWBACKGROUNDS', (0,1), (-1,-1), [colors.white, neutral_light])
    ]))
    elements.append(t_mat)
    elements.append(Spacer(1, 14))

    # SECTION 2: LAMPIRAN VISUAL SCREENSHOT
    elements.append(Paragraph("2. Lampiran Bukti Visual Temuan Kesalahan", h1_style))
    p_sc = Paragraph(
        "Di bawah ini dilampirkan bukti visual identifikasi kesalahan pada lembar kerja dan model untuk segera direvisi "
        "oleh tim drafter/modeler:",
        body_style
    )
    elements.append(p_sc)
    elements.append(Spacer(1, 8))

    # Image 1 & 2
    img1_path = os.path.join(img_dir, "screenshot_temuan_1_notasi_kusen.png")
    img2_path = os.path.join(img_dir, "screenshot_temuan_2_typo_balok.png")
    img3_path = os.path.join(img_dir, "screenshot_temuan_3_duplikat_dinding.png")
    img4_path = os.path.join(img_dir, "screenshot_temuan_4_urutan_sheet.png")

    if os.path.exists(img1_path):
        elements.append(Image(img1_path, width=17.4*cm, height=9.5*cm))
        elements.append(Spacer(1, 10))

    if os.path.exists(img2_path):
        elements.append(Image(img2_path, width=17.4*cm, height=9.5*cm))
        elements.append(Spacer(1, 12))

    elements.append(PageBreak())

    # Page 2 Images
    elements.append(Paragraph("2. Lampiran Bukti Visual Temuan Kesalahan (Lanjutan)", h1_style))
    elements.append(Spacer(1, 6))

    if os.path.exists(img3_path):
        elements.append(Image(img3_path, width=17.4*cm, height=9.5*cm))
        elements.append(Spacer(1, 10))

    if os.path.exists(img4_path):
        elements.append(Image(img4_path, width=17.4*cm, height=7.8*cm))
        elements.append(Spacer(1, 14))

    # Sign-off box
    sign_data = [
        [Paragraph("Disiapkan Oleh:", body_style), Paragraph("Diverifikasi Lead Drafter:", body_style), Paragraph("Persetujuan Rilis PM:", body_style)],
        [Spacer(1, 1.2*cm), Spacer(1, 1.2*cm), Spacer(1, 1.2*cm)],
        [Paragraph("<b>Site Engineer</b><br/>Quality Reviewer", body_style),
         Paragraph("<b>BIM Modeler / Drafter</b><br/>Action Taken", body_style),
         Paragraph("<b>Project Manager</b><br/>Final Approval", body_style)]
    ]
    t_sign = Table(sign_data, colWidths=[5.8*cm, 5.8*cm, 5.8*cm])
    t_sign.setStyle(TableStyle([
        ('BOX', (0,0), (-1,-1), 1, border_color),
        ('INNERGRID', (0,0), (-1,-1), 0.5, border_color),
        ('BACKGROUND', (0,0), (-1,-1), neutral_light),
        ('ALIGN', (0,0), (-1,-1), 'CENTER'),
        ('TOPPADDING', (0,0), (-1,-1), 5),
        ('BOTTOMPADDING', (0,0), (-1,-1), 5),
    ]))
    elements.append(KeepTogether(t_sign))

    doc.build(elements, canvasmaker=NumberedCanvas)
    print(f"FULL_REPORT_GENERATED:{output_path}")

if __name__ == '__main__':
    artifact_dir = r"C:\Users\sapuser\.gemini\antigravity\brain\90364c76-78c1-4109-9d8e-73916213bbeb"
    out_pdf = r"C:\AIGRAV\Site_Engineer_Detailing_Audit_Report.pdf"
    create_full_report(out_pdf, artifact_dir)
