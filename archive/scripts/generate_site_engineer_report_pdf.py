import os
from reportlab.lib import colors
from reportlab.lib.pagesizes import A4
from reportlab.platypus import (
    SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle, PageBreak, KeepTogether, HRFlowable
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
            self.drawString(2 * cm, 28.5 * cm, "SITE ENGINEERING DRAWING CROSS-CHECK REPORT — NUAS-FT-PN-PJ4")
            self.setStrokeColor(colors.HexColor("#CBD5E0"))
            self.setLineWidth(0.5)
            self.line(2 * cm, 28.3 * cm, 19 * cm, 28.3 * cm)
            
        # Footer
        footer_text = f"Halaman {self._pageNumber} dari {page_count}"
        self.drawRightString(19 * cm, 1.2 * cm, footer_text)
        self.drawString(2 * cm, 1.2 * cm, "CONFIDENTIAL & PROPRIETARY — REVIEW SEBELUM RILIS LAPANGAN")
        self.setStrokeColor(colors.HexColor("#CBD5E0"))
        self.setLineWidth(0.5)
        self.line(2 * cm, 1.5 * cm, 19 * cm, 1.5 * cm)
        self.restoreState()

def create_report(output_path):
    doc = SimpleDocTemplate(
        output_path,
        pagesize=A4,
        leftMargin=2 * cm,
        rightMargin=2 * cm,
        topMargin=2.2 * cm,
        bottomMargin=2.2 * cm
    )

    styles = getSampleStyleSheet()
    
    # Custom Styles
    primary_color = colors.HexColor("#1A365D")   # Deep Navy
    secondary_color = colors.HexColor("#2B6CB0") # Medium Blue
    accent_red = colors.HexColor("#C53030")      # Warning Red
    neutral_dark = colors.HexColor("#2D3748")    # Charcoal body
    neutral_light = colors.HexColor("#F7FAFC")
    border_color = colors.HexColor("#E2E8F0")

    title_style = ParagraphStyle(
        'DocTitle',
        parent=styles['Normal'],
        fontName='Helvetica-Bold',
        fontSize=20,
        leading=24,
        textColor=primary_color,
        spaceAfter=4
    )

    subtitle_style = ParagraphStyle(
        'DocSubTitle',
        parent=styles['Normal'],
        fontName='Helvetica',
        fontSize=10,
        leading=14,
        textColor=colors.HexColor("#4A5568"),
        spaceAfter=15
    )

    h1_style = ParagraphStyle(
        'Heading1_Custom',
        parent=styles['Normal'],
        fontName='Helvetica-Bold',
        fontSize=13,
        leading=17,
        textColor=primary_color,
        spaceBefore=14,
        spaceAfter=8,
        keepWithNext=True
    )

    h2_style = ParagraphStyle(
        'Heading2_Custom',
        parent=styles['Normal'],
        fontName='Helvetica-Bold',
        fontSize=10.5,
        leading=14,
        textColor=secondary_color,
        spaceBefore=8,
        spaceAfter=4,
        keepWithNext=True
    )

    body_style = ParagraphStyle(
        'Body_Custom',
        parent=styles['Normal'],
        fontName='Helvetica',
        fontSize=9,
        leading=13,
        textColor=neutral_dark,
        spaceAfter=4
    )

    body_bold = ParagraphStyle(
        'Body_Bold',
        parent=body_style,
        fontName='Helvetica-Bold'
    )

    callout_style = ParagraphStyle(
        'Callout_Text',
        parent=styles['Normal'],
        fontName='Helvetica',
        fontSize=9,
        leading=13,
        textColor=colors.HexColor("#742A2A")
    )

    table_header_style = ParagraphStyle(
        'TH_Style',
        parent=styles['Normal'],
        fontName='Helvetica-Bold',
        fontSize=8.5,
        leading=11,
        textColor=colors.white,
        alignment=1 # Center
    )

    table_cell_style = ParagraphStyle(
        'TD_Style',
        parent=styles['Normal'],
        fontName='Helvetica',
        fontSize=8,
        leading=11,
        textColor=neutral_dark
    )

    table_cell_center = ParagraphStyle(
        'TD_Center',
        parent=table_cell_style,
        alignment=1
    )

    table_cell_bold = ParagraphStyle(
        'TD_Bold',
        parent=table_cell_style,
        fontName='Helvetica-Bold'
    )

    elements = []

    # Title Block
    elements.append(Paragraph("LAPORAN REVIEW & CROSS-CHECK GAMBAR", title_style))
    elements.append(Paragraph("Kajian Teknis Site Engineer Sebelum Rilis Dokumen Tender / Lapangan", subtitle_style))
    elements.append(HRFlowable(width="100%", thickness=2, color=primary_color, spaceBefore=0, spaceAfter=12))

    # Project Information Meta Table
    info_data = [
        [Paragraph("<b>Nama Proyek:</b>", body_style), Paragraph("NUAS-FT-PN-PJ4 (Pos Jaga & Parkir Motor)", body_style),
         Paragraph("<b>Tanggal Review:</b>", body_style), Paragraph("09 Oktober 2026", body_style)],
        [Paragraph("<b>Lokasi Dokumen:</b>", body_style), Paragraph("04. Parkir Motor (For Tender)", body_style),
         Paragraph("<b>Reviewer:</b>", body_style), Paragraph("Site Engineer (Quality Audit)", body_style)],
        [Paragraph("<b>File Model:</b>", body_style), Paragraph("NUAS-FT-PN-PJ4.rvt", body_style),
         Paragraph("<b>Status Kelayakan:</b>", body_style), Paragraph("<font color='#C53030'><b>REVISI MINOR SEBELUM RILIS</b></font>", body_style)]
    ]
    t_info = Table(info_data, colWidths=[3.2*cm, 5.5*cm, 3.2*cm, 5.1*cm])
    t_info.setStyle(TableStyle([
        ('BACKGROUND', (0,0), (-1,-1), neutral_light),
        ('BOX', (0,0), (-1,-1), 1, border_color),
        ('INNERGRID', (0,0), (-1,-1), 0.5, border_color),
        ('TOPPADDING', (0,0), (-1,-1), 5),
        ('BOTTOMPADDING', (0,0), (-1,-1), 5),
        ('LEFTPADDING', (0,0), (-1,-1), 6),
        ('RIGHTPADDING', (0,0), (-1,-1), 6),
    ]))
    elements.append(t_info)
    elements.append(Spacer(1, 14))

    # SECTION 1: RINGKASAN EKSEKUTIF
    elements.append(Paragraph("1. Ringkasan Eksekutif & Tujuan Audit", h1_style))
    p_intro = (
        "Audit teknis ini dilakukan terhadap 16 lembar gambar (*drawing sheets*) dan model 3D BIM proyek "
        "<b>NUAS-FT-PN-PJ4</b>. Tujuan pemeriksaan ini adalah mendeteksi secara dini potensi konflik gambar kerja, "
        "kesalahan urutan gambar yang membingungkan tim pelaksana/kontraktor, serta risiko klaim/kesalahan volume "
        "pekerjaan (BQ) saat proses tender maupun pelaksanaan fisik di lapangan."
    )
    elements.append(Paragraph(p_intro, body_style))
    elements.append(Spacer(1, 8))

    # SECTION 2: TEMUAN KRITIS SITE ENGINEER
    elements.append(Paragraph("2. Temuan Kritis Model & Risiko Lapangan", h1_style))
    
    findings_data = [
        [
            Paragraph("No", table_header_style),
            Paragraph("Kategori Temuan", table_header_style),
            Paragraph("Uraian Isu Teknis Model", table_header_style),
            Paragraph("Dampak Lapangan & Risiko QS", table_header_style),
            Paragraph("Rekomendasi Tindakan", table_header_style)
        ],
        [
            Paragraph("1", table_cell_center),
            Paragraph("<b>Elemen Ganda<br/>(Duplicate)</b>", table_cell_style),
            Paragraph("Terdeteksi instans ganda pada koordinat yang persis sama (<i>identical instances in same place</i>).", table_cell_style),
            Paragraph("<font color='#C53030'><b>Double counting BQ:</b></font> Volume beton/dinding terhitung ganda pada bill of quantity.", table_cell_style),
            Paragraph("Hapus instans duplikat melalui audit ID di Revit.", table_cell_style)
        ],
        [
            Paragraph("2", table_cell_center),
            Paragraph("<b>Struktur Miring<br/>(Off-Axis)</b>", table_cell_style),
            Paragraph("Balok / Bracing baja sedikit bergeser dari sumbu ortogonal (<i>slightly off axis</i>).", table_cell_style),
            Paragraph("<font color='#C53030'><b>Kesalahan Fabrikasi:</b></font> Lubang baut pelat sambungan rawan meleset saat perakitan baja di lokasi.", table_cell_style),
            Paragraph("Luruskan koordinat end-point framing ke sumbu grid.", table_cell_style)
        ],
        [
            Paragraph("3", table_cell_center),
            Paragraph("<b>Dinding Tumpang Tindih</b>", table_cell_style),
            Paragraph("Dinding bata dan partisi saling menembus (<i>overlapping walls</i>) tanpa pembagian pemotongan.", table_cell_style),
            Paragraph("Luasan plester, acian, dan cat membengkak di perhitungan RAB.", table_cell_style),
            Paragraph("Gunakan <i>Cut Geometry</i> atau rapikan join dinding.", table_cell_style)
        ],
        [
            Paragraph("4", table_cell_center),
            Paragraph("<b>Struktur Terpisah<br/>(Join Error)</b>", table_cell_style),
            Paragraph("Elemen berstatus ter-join namun tidak saling berpotongan (<i>joined but do not intersect</i>).", table_cell_style),
            Paragraph("Visual gambar potongan menampilkan garis perpotongan yang tidak sinkron.", table_cell_style),
            Paragraph("Unjoin geometry elemen yang sudah tidak bertemu.", table_cell_style)
        ]
    ]

    t_findings = Table(findings_data, colWidths=[0.8*cm, 2.7*cm, 4.3*cm, 4.7*cm, 4.5*cm])
    t_findings.setStyle(TableStyle([
        ('BACKGROUND', (0,0), (-1,0), primary_color),
        ('BOX', (0,0), (-1,-1), 1, primary_color),
        ('INNERGRID', (0,0), (-1,-1), 0.5, border_color),
        ('VALIGN', (0,0), (-1,-1), 'MIDDLE'),
        ('TOPPADDING', (0,0), (-1,-1), 5),
        ('BOTTOMPADDING', (0,0), (-1,-1), 5),
        ('ROWBACKGROUNDS', (0,1), (-1,-1), [colors.white, neutral_light])
    ]))
    elements.append(t_findings)
    elements.append(Spacer(1, 14))

    # SECTION 3: AUDIT URUTAN LEMBAR GAMBAR (DRAWING LIST)
    elements.append(Paragraph("3. Evaluasi Urutan Lembar Gambar (Drawing Index Audit)", h1_style))
    p_sheets = (
        "Berdasarkan standar penyusunan DED konstruksi, paket gambar harus tersusun secara runut mulai dari "
        "informasi umum, arsitektur menyeluruh, detail arsitektur, hingga struktur dan atap. "
        "Di bawah ini adalah status 16 lembar gambar yang terdaftar di proyek saat ini:"
    )
    elements.append(Paragraph(p_sheets, body_style))
    elements.append(Spacer(1, 6))

    sheets_data = [
        [Paragraph("No", table_header_style),
         Paragraph("Sheet No", table_header_style),
         Paragraph("Judul Lembar Gambar", table_header_style),
         Paragraph("Disiplin", table_header_style),
         Paragraph("Catatan Review Lapangan", table_header_style),
         Paragraph("Status Urutan", table_header_style)],
        [Paragraph("1", table_cell_center), Paragraph("000", table_cell_center), Paragraph("COVER", table_cell_style), Paragraph("Umum", table_cell_center), Paragraph("Halaman sampul proyek.", table_cell_style), Paragraph("Perlu No Standar", table_cell_bold)],
        [Paragraph("2", table_cell_center), Paragraph("PJ - 00", table_cell_center), Paragraph("DAFTAR GAMBAR", table_cell_style), Paragraph("Umum", table_cell_center), Paragraph("Daftar isi seluruh lembar.", table_cell_style), Paragraph("Sesuai", table_cell_center)],
        [Paragraph("3", table_cell_center), Paragraph("PJ - 01", table_cell_center), Paragraph("DENAH POS JAGA & DENAH ATAP", table_cell_style), Paragraph("Arsitektur", table_cell_center), Paragraph("Denah tata ruang & luasan.", table_cell_style), Paragraph("Sesuai", table_cell_center)],
        [Paragraph("4", table_cell_center), Paragraph("PJ - 02", table_cell_center), Paragraph("TAMPAK DEPAN & TAMPAK KIRI", table_cell_style), Paragraph("Arsitektur", table_cell_center), Paragraph("Tampak elevasi bangunan.", table_cell_style), Paragraph("Sesuai", table_cell_center)],
        [Paragraph("5", table_cell_center), Paragraph("PJ - 03", table_cell_center), Paragraph("TAMPAK BELAKANG & TAMPAK KANAN", table_cell_style), Paragraph("Arsitektur", table_cell_center), Paragraph("Tampak lanjutan.", table_cell_style), Paragraph("Sesuai", table_cell_center)],
        [Paragraph("6", table_cell_center), Paragraph("PJ - 04", table_cell_center), Paragraph("POTONGAN - A / B", table_cell_style), Paragraph("Arsitektur", table_cell_center), Paragraph("Potongan memanjang & melintang.", table_cell_style), Paragraph("Sesuai", table_cell_center)],
        [Paragraph("7", table_cell_center), Paragraph("PJ - 05", table_cell_center), Paragraph("POTONGAN - C", table_cell_style), Paragraph("Arsitektur", table_cell_center), Paragraph("Potongan detail ruang.", table_cell_style), Paragraph("Sesuai", table_cell_center)],
        [Paragraph("8", table_cell_center), Paragraph("PJ - 06", table_cell_center), Paragraph("POTONGAN - D", table_cell_style), Paragraph("Arsitektur", table_cell_center), Paragraph("Potongan tambahan.", table_cell_style), Paragraph("Sesuai", table_cell_center)],
        [Paragraph("9", table_cell_center), Paragraph("PJ - 07", table_cell_center), Paragraph("DETAIL POTONGAN", table_cell_style), Paragraph("Arsitektur", table_cell_center), Paragraph("Detail dinding & sambungan.", table_cell_style), Paragraph("Sesuai", table_cell_center)],
        [Paragraph("10", table_cell_center), Paragraph("PJ - 08", table_cell_center), Paragraph("DENAH KERAMIK & DENAH PLAFOND", table_cell_style), Paragraph("Arsitektur", table_cell_center), Paragraph("Pola keramik lantai & ceiling.", table_cell_style), Paragraph("Sesuai", table_cell_center)],
        [Paragraph("11", table_cell_center), Paragraph("PJ - 09", table_cell_center), Paragraph("DENAH FIN. DINDING", table_cell_style), Paragraph("Arsitektur", table_cell_center), Paragraph("Spesifikasi cat / keramik dinding.", table_cell_style), Paragraph("Sesuai", table_cell_center)],
        [Paragraph("12", table_cell_center), Paragraph("PJ - 10", table_cell_center), Paragraph("DENAH KUSEN", table_cell_style), Paragraph("Arsitektur", table_cell_center), Paragraph("Tata letak pintu & jendela.", table_cell_style), Paragraph("Sesuai", table_cell_center)],
        [Paragraph("13", table_cell_center), Paragraph("PJ - 11", table_cell_center), Paragraph("DETAIL KUSEN", table_cell_style), Paragraph("Arsitektur", table_cell_center), Paragraph("Dimensi profil kusen, kaca, daun.", table_cell_style), Paragraph("Sesuai", table_cell_center)],
        [Paragraph("14", table_cell_center), Paragraph("<b>PJ - 12</b>", table_cell_center), Paragraph("<b>DENAH PONDASI</b>", table_cell_style), Paragraph("Struktur", table_cell_center), Paragraph("<font color='#C53030'><b>Posisi terisolir di akhir.</b></font>", table_cell_style), Paragraph("<font color='#C53030'><b>Anomali</b></font>", table_cell_center)],
        [Paragraph("15", table_cell_center), Paragraph("PJ - 13", table_cell_center), Paragraph("DENAH RANGKA ATAP & INSULATION", table_cell_style), Paragraph("Struktur/Atap", table_cell_center), Paragraph("Rangka gording, kuda-kuda, peredam.", table_cell_style), Paragraph("Sesuai", table_cell_center)],
        [Paragraph("16", table_cell_center), Paragraph("PJ - 14", table_cell_center), Paragraph("DETAIL WIND BRACING", table_cell_style), Paragraph("Struktur", table_cell_center), Paragraph("Detail ikatan angin baja.", table_cell_style), Paragraph("Sesuai", table_cell_center)],
    ]

    t_sheets = Table(sheets_data, colWidths=[0.8*cm, 1.8*cm, 5.8*cm, 2.2*cm, 4.5*cm, 1.9*cm])
    t_sheets.setStyle(TableStyle([
        ('BACKGROUND', (0,0), (-1,0), secondary_color),
        ('BOX', (0,0), (-1,-1), 1, secondary_color),
        ('INNERGRID', (0,0), (-1,-1), 0.5, border_color),
        ('VALIGN', (0,0), (-1,-1), 'MIDDLE'),
        ('TOPPADDING', (0,0), (-1,-1), 3.5),
        ('BOTTOMPADDING', (0,0), (-1,-1), 3.5),
        ('ROWBACKGROUNDS', (0,1), (-1,-1), [colors.white, neutral_light])
    ]))
    elements.append(t_sheets)
    elements.append(Spacer(1, 14))

    # SECTION 4: KONTROL ELEVASI & BENCHMARK
    elements.append(Paragraph("4. Verifikasi Elevasi Bangunan (Site Benchmark)", h1_style))
    elev_data = [
        [Paragraph("Nama Level Acuan", table_header_style),
         Paragraph("Elevasi Proyek (m)", table_header_style),
         Paragraph("Tinggi Relatif (m)", table_header_style),
         Paragraph("Catatan Teknis Site Engineer", table_header_style)],
        [Paragraph("<b>REFERENSI</b>", table_cell_style), Paragraph("+71.100 m", table_cell_center), Paragraph("+0.000 m", table_cell_center), Paragraph("Benchmark dasar pengukuran tanah / kontur.", table_cell_style)],
        [Paragraph("<b>JL (Jalan)</b>", table_cell_style), Paragraph("+71.600 m", table_cell_center), Paragraph("+0.500 m", table_cell_center), Paragraph("Elevasi permukaan perkerasan jalan / parkir motor.", table_cell_style)],
        [Paragraph("<b>POS JAGA (Lantai)</b>", table_cell_style), Paragraph("+71.800 m", table_cell_center), Paragraph("+0.700 m", table_cell_center), Paragraph("Beda tinggi <b>+20 cm</b> di atas jalan. Bebas banjir & limpasan air.", table_cell_style)],
        [Paragraph("<b>PLAFOND</b>", table_cell_style), Paragraph("+74.400 m", table_cell_center), Paragraph("+3.300 m", table_cell_center), Paragraph("Clear height ruangan = <b>2.60 m</b>. Sirkulasi udara ergonomis.", table_cell_style)],
        [Paragraph("<b>ATAP</b>", table_cell_style), Paragraph("+75.030 m", table_cell_center), Paragraph("+3.930 m", table_cell_center), Paragraph("Tinggi puncak bangunan pos jaga.", table_cell_style)],
    ]
    t_elev = Table(elev_data, colWidths=[4.2*cm, 3.2*cm, 3.2*cm, 6.4*cm])
    t_elev.setStyle(TableStyle([
        ('BACKGROUND', (0,0), (-1,0), primary_color),
        ('BOX', (0,0), (-1,-1), 1, primary_color),
        ('INNERGRID', (0,0), (-1,-1), 0.5, border_color),
        ('VALIGN', (0,0), (-1,-1), 'MIDDLE'),
        ('TOPPADDING', (0,0), (-1,-1), 4),
        ('BOTTOMPADDING', (0,0), (-1,-1), 4),
        ('ROWBACKGROUNDS', (0,1), (-1,-1), [colors.white, neutral_light])
    ]))
    elements.append(t_elev)
    elements.append(Spacer(1, 14))

    # SECTION 5: KESIMPULAN & CHECKLIST RILIS
    elements.append(Paragraph("5. Rekomendasi Checklist Sebelum Release Lapangan", h1_style))
    checklist_text = (
        "Sebelum dokumen ini dicap <b>ISSUED FOR TENDER / CONSTRUCTION</b>, tim drafter & engineer wajib "
        "menyelesaikan 3 poin perbaikan berikut:<br/>"
        "• <b>[Wajib] Pembersihan Elemen Duplikat:</b> Bersihkan elemen kembar pada model agar BQ tender presisi.<br/>"
        "• <b>[Wajib] Penataan Sheet Pondasi:</b> Pertimbangkan menata urutan gambar menjadi:<br/>"
        "&nbsp;&nbsp;&nbsp;&nbsp;1. <i>Cover & Daftar Gambar</i> (PJ-00A & PJ-00B)<br/>"
        "&nbsp;&nbsp;&nbsp;&nbsp;2. <i>Gambar Struktur (Pondasi, Balok, Atap, Bracing)</i> (PJ-01 s/d PJ-04)<br/>"
        "&nbsp;&nbsp;&nbsp;&nbsp;3. <i>Gambar Arsitektur (Denah, Tampak, Potongan, Finishes, Kusen)</i> (PJ-05 s/d PJ-14)<br/>"
        "• <b>[Penting] Cek Sambungan Baja:</b> Verifikasi titik koordinat ikatan angin (*wind bracing*) pada lembar PJ-14 "
        "agar bebas dari anomali *off-axis* sebelum masuk ke bengkel fabrikasi."
    )
    elements.append(Paragraph(checklist_text, body_style))
    elements.append(Spacer(1, 20))

    # Sign-off box
    sign_data = [
        [Paragraph("Disiapkan Oleh:", body_style), Paragraph("Diverifikasi Oleh:", body_style), Paragraph("Disetujui Untuk Rilis:", body_style)],
        [Spacer(1, 1.4*cm), Spacer(1, 1.4*cm), Spacer(1, 1.4*cm)],
        [Paragraph("<b>Site Engineer</b><br/>Quality Review", body_style),
         Paragraph("<b>Lead Architect / Structural Eng.</b><br/>Project Designer", body_style),
         Paragraph("<b>Project Manager</b><br/>Representative", body_style)]
    ]
    t_sign = Table(sign_data, colWidths=[5.6*cm, 5.7*cm, 5.7*cm])
    t_sign.setStyle(TableStyle([
        ('BOX', (0,0), (-1,-1), 1, border_color),
        ('INNERGRID', (0,0), (-1,-1), 0.5, border_color),
        ('BACKGROUND', (0,0), (-1,-1), neutral_light),
        ('ALIGN', (0,0), (-1,-1), 'CENTER'),
        ('TOPPADDING', (0,0), (-1,-1), 6),
        ('BOTTOMPADDING', (0,0), (-1,-1), 6),
    ]))
    elements.append(KeepTogether(t_sign))

    doc.build(elements, canvasmaker=NumberedCanvas)
    print(f"REPORT_GENERATED:{output_path}")

if __name__ == '__main__':
    out_pdf = r"C:\AIGRAV\Site_Engineer_CrossCheck_Report_NUAS.pdf"
    create_report(out_pdf)
