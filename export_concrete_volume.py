import urllib.request
import json
import os
from collections import defaultdict
import openpyxl
from openpyxl.styles import Font, PatternFill, Alignment, Border, Side

CF_TO_M3 = 0.028316846592

def call_tool(tool_name, argument=None):
    payload = {'tool': tool_name}
    if argument is not None:
        payload['argument'] = str(argument)
    req = urllib.request.Request(
        'http://127.0.0.1:8765/',
        data=json.dumps(payload).encode('utf-8'),
        headers={'Content-Type': 'application/json'},
        method='POST'
    )
    with urllib.request.urlopen(req) as res:
        data = json.loads(res.read().decode('utf-8'))
        return data.get('Result', '')

def get_elements_list(cat_tool):
    res_text = call_tool(cat_tool)
    elements = []
    for line in res_text.splitlines()[1:]:
        if line.startswith('- Id:'):
            parts = line.split('|')
            eid = parts[0].replace('- Id:', '').strip()
            name = parts[1].replace('Name:', '').strip()
            tid = parts[2].replace('TypeId:', '').strip() if len(parts) > 2 else ''
            elements.append((eid, name, tid))
    return elements

def get_parameters(eid):
    res_text = call_tool('get_element_parameters', eid)
    params = {}
    for line in res_text.splitlines():
        if '=' in line:
            k, v = line.split('=', 1)
            params[k.strip()] = v.strip()
    return params

print('Collecting elements...')
columns_raw = get_elements_list('get_structural_columns')
framings_raw = get_elements_list('get_structural_framing')
floors_raw = get_elements_list('get_floors')

detailed_records = []

# Process Columns
for eid, name, tid in columns_raw:
    p = get_parameters(eid)
    vol_cf = float(p.get('Volume', 0)) if p.get('Volume') else 0.0
    vol_m3 = vol_cf * CF_TO_M3
    dim = '110x110 mm' if 'KP2' in name else ('300x250 mm' if '300' in name else name)
    detailed_records.append({
        'id': eid,
        'kategori': 'Structural Columns (Kolom Praktis & Utama)',
        'tipe': name,
        'dimensi': dim,
        'volume_m3': vol_m3,
        'material': 'Concrete, Cast-in-Place gray',
        'level': '1ST FLOOR'
    })

# Process Framing (Beams)
concrete_beam_types = ['BP 110x110', 'BP 110x150', 'B1 250X300']
for eid, name, tid in framings_raw:
    if name in concrete_beam_types:
        p = get_parameters(eid)
        vol_cf = float(p.get('Volume', 0)) if p.get('Volume') else 0.0
        vol_m3 = vol_cf * CF_TO_M3
        dim = '110x110 mm' if '110x110' in name else ('110x150 mm' if '110x150' in name else '250x300 mm')
        detailed_records.append({
            'id': eid,
            'kategori': 'Structural Framing (Balok Praktis & Utama)',
            'tipe': name,
            'dimensi': dim,
            'volume_m3': vol_m3,
            'material': 'Concrete, Cast-in-Place gray',
            'level': '1ST FLOOR'
        })

# Process Floors / Special Concrete
special_floors = {
    '4467739': ('Pedestall Mesin Beton', '6600 x 1400 x 100 mm', 'Concrete - CIP 35 MPa'),
    '4676783': ('Bak Pit Beton', '1400 x 1400 x 2400 mm', 'Concrete - CIP 35 MPa'),
    '4698421': ('Meja Lab Beton Tipe 1', '2980 x 1180 x 890 mm', 'Concrete - CIP 35 MPa'),
    '4698793': ('Meja Lab Beton Tipe 2 (Unit A)', 'Meja Beton Laboratorium', 'Concrete - CIP 35 MPa'),
    '4763834': ('Meja Lab Beton Tipe 2 (Unit B)', 'Meja Beton Laboratorium', 'Concrete - CIP 35 MPa'),
    '4765976': ('Meja Lab Beton Tipe 2 (Unit C)', 'Meja Beton Laboratorium', 'Concrete - CIP 35 MPa'),
    '4893377': ('Meja Lab Beton Tipe 2 (Unit D)', 'Meja Beton Laboratorium', 'Concrete - CIP 35 MPa'),
    '4698569': ('Top Table Meja Beton', 'Pelat Meja Beton t=150mm', 'Concrete - CIP 35 MPa'),
    '4303079': ('Pelat Lantai Beton (Area Panel)', 'Area 22.52 m2, t=50mm', 'Cement Concrete'),
    '4468376': ('Pelat Lantai Beton (Area Utama)', 'Area 36.31 m2, t=50mm', 'Cement Concrete')
}

for eid, (desc, dim, mat) in special_floors.items():
    p = get_parameters(eid)
    vol_cf = float(p.get('Volume', 0)) if p.get('Volume') else 0.0
    vol_m3 = vol_cf * CF_TO_M3
    detailed_records.append({
        'id': eid,
        'kategori': 'Struktur Khusus & Pelat Lantai Beton',
        'tipe': desc,
        'dimensi': dim,
        'volume_m3': vol_m3,
        'material': mat,
        'level': '1ST FLOOR'
    })

print(f'Total elements gathered: {len(detailed_records)}')

# Group by category and type for summary
summary_groups = defaultdict(lambda: {'count': 0, 'volume_m3': 0.0, 'items': defaultdict(lambda: {'count': 0, 'volume_m3': 0.0, 'material': ''})})

for rec in detailed_records:
    cat = rec['kategori']
    tip = rec['tipe']
    v = rec['volume_m3']
    m = rec['material']
    summary_groups[cat]['count'] += 1
    summary_groups[cat]['volume_m3'] += v
    summary_groups[cat]['items'][tip]['count'] += 1
    summary_groups[cat]['items'][tip]['volume_m3'] += v
    summary_groups[cat]['items'][tip]['material'] = m

wb = openpyxl.Workbook()

font_title = Font(name='Calibri', size=16, bold=True, color='1B365D')
font_subtitle = Font(name='Calibri', size=11, italic=True, color='555555')
font_header = Font(name='Calibri', size=11, bold=True, color='FFFFFF')
font_cat_header = Font(name='Calibri', size=11, bold=True, color='1B365D')
font_bold = Font(name='Calibri', size=11, bold=True)
font_regular = Font(name='Calibri', size=11)

fill_navy = PatternFill(start_color='1B365D', end_color='1B365D', fill_type='solid')
fill_cat = PatternFill(start_color='D9E1F2', end_color='D9E1F2', fill_type='solid')
fill_subtotal = PatternFill(start_color='E9EEF4', end_color='E9EEF4', fill_type='solid')
fill_grand_total = PatternFill(start_color='203764', end_color='203764', fill_type='solid')

border_thin = Border(
    left=Side(style='thin', color='BFBFBF'),
    right=Side(style='thin', color='BFBFBF'),
    top=Side(style='thin', color='BFBFBF'),
    bottom=Side(style='thin', color='BFBFBF')
)
border_double_bottom = Border(
    left=Side(style='thin', color='BFBFBF'),
    right=Side(style='thin', color='BFBFBF'),
    top=Side(style='thin', color='BFBFBF'),
    bottom=Side(style='double', color='1B365D')
)

align_center = Alignment(horizontal='center', vertical='center')
align_left = Alignment(horizontal='left', vertical='center')
align_right = Alignment(horizontal='right', vertical='center')

# --- SHEET 1: REKAPITULASI ---
ws_sum = wb.active
ws_sum.title = 'Rekapitulasi Volume'
ws_sum.views.sheetView[0].showGridLines = True

ws_sum['B2'] = 'REKAPITULASI ESTIMASI TOTAL VOLUME BETON'
ws_sum['B2'].font = font_title
ws_sum['B3'] = 'Proyek: OLY-SI18-PWD-AR-GU | Model Revit: OLY-SI18-PWD-AR-GU.rvt'
ws_sum['B3'].font = font_subtitle

headers_sum = ['No', 'Uraian Item Pekerjaan Beton', 'Spesifikasi / Dimensi', 'Material Mutu Beton', 'Jumlah (Unit)', 'Satuan', 'Volume (m3)']
cols_sum = ['B', 'C', 'D', 'E', 'F', 'G', 'H']

row_idx = 5
for col_letter, h_text in zip(cols_sum, headers_sum):
    cell = ws_sum[f'{col_letter}{row_idx}']
    cell.value = h_text
    cell.font = font_header
    cell.fill = fill_navy
    cell.alignment = align_center
    cell.border = border_thin

item_num = 1
subtotal_rows = []

for cat_name, cat_data in summary_groups.items():
    row_idx += 1
    ws_sum[f'B{row_idx}'] = ''
    ws_sum[f'C{row_idx}'] = f'PEKERJAAN {cat_name.upper()}'
    ws_sum[f'C{row_idx}'].font = font_cat_header
    for c in cols_sum:
        ws_sum[f'{c}{row_idx}'].fill = fill_cat
        ws_sum[f'{c}{row_idx}'].border = border_thin
    
    start_row = row_idx + 1
    for t_name, t_data in cat_data['items'].items():
        row_idx += 1
        ws_sum[f'B{row_idx}'] = item_num
        ws_sum[f'B{row_idx}'].alignment = align_center
        ws_sum[f'C{row_idx}'] = t_name
        ws_sum[f'C{row_idx}'].alignment = align_left
        
        dim_text = '-'
        for rec in detailed_records:
            if rec['tipe'] == t_name:
                dim_text = rec['dimensi']
                break
        ws_sum[f'D{row_idx}'] = dim_text
        ws_sum[f'D{row_idx}'].alignment = align_left
        ws_sum[f'E{row_idx}'] = t_data['material']
        ws_sum[f'E{row_idx}'].alignment = align_left
        ws_sum[f'F{row_idx}'] = t_data['count']
        ws_sum[f'F{row_idx}'].alignment = align_center
        ws_sum[f'F{row_idx}'].number_format = '#,##0'
        ws_sum[f'G{row_idx}'] = 'm3'
        ws_sum[f'G{row_idx}'].alignment = align_center
        ws_sum[f'H{row_idx}'] = round(t_data['volume_m3'], 4)
        ws_sum[f'H{row_idx}'].alignment = align_right
        ws_sum[f'H{row_idx}'].number_format = '#,##0.000'
        
        for c in cols_sum:
            ws_sum[f'{c}{row_idx}'].font = font_regular
            ws_sum[f'{c}{row_idx}'].border = border_thin
        item_num += 1
        
    row_idx += 1
    ws_sum[f'B{row_idx}'] = ''
    ws_sum[f'C{row_idx}'] = f'Subtotal {cat_name}'
    ws_sum[f'C{row_idx}'].font = font_bold
    ws_sum[f'C{row_idx}'].alignment = align_left
    ws_sum[f'D{row_idx}'] = ''
    ws_sum[f'E{row_idx}'] = ''
    ws_sum[f'F{row_idx}'] = f'=SUM(F{start_row}:F{row_idx-1})'
    ws_sum[f'F{row_idx}'].font = font_bold
    ws_sum[f'F{row_idx}'].alignment = align_center
    ws_sum[f'F{row_idx}'].number_format = '#,##0'
    ws_sum[f'G{row_idx}'] = 'm3'
    ws_sum[f'G{row_idx}'].alignment = align_center
    ws_sum[f'H{row_idx}'] = f'=SUM(H{start_row}:H{row_idx-1})'
    ws_sum[f'H{row_idx}'].font = font_bold
    ws_sum[f'H{row_idx}'].alignment = align_right
    ws_sum[f'H{row_idx}'].number_format = '#,##0.000'
    for c in cols_sum:
        ws_sum[f'{c}{row_idx}'].fill = fill_subtotal
        ws_sum[f'{c}{row_idx}'].border = border_thin
    subtotal_rows.append(row_idx)

row_idx += 1
ws_sum[f'B{row_idx}'] = ''
ws_sum[f'C{row_idx}'] = 'TOTAL KESELURUHAN VOLUME BETON'
ws_sum[f'C{row_idx}'].font = Font(name='Calibri', size=11, bold=True, color='FFFFFF')
ws_sum[f'C{row_idx}'].alignment = align_left
ws_sum[f'D{row_idx}'] = ''
ws_sum[f'E{row_idx}'] = ''
ws_sum[f'F{row_idx}'] = '=' + '+'.join([f'F{r}' for r in subtotal_rows])
ws_sum[f'F{row_idx}'].font = Font(name='Calibri', size=11, bold=True, color='FFFFFF')
ws_sum[f'F{row_idx}'].alignment = align_center
ws_sum[f'F{row_idx}'].number_format = '#,##0'
ws_sum[f'G{row_idx}'] = 'm3'
ws_sum[f'G{row_idx}'].font = Font(name='Calibri', size=11, bold=True, color='FFFFFF')
ws_sum[f'G{row_idx}'].alignment = align_center
ws_sum[f'H{row_idx}'] = '=' + '+'.join([f'H{r}' for r in subtotal_rows])
ws_sum[f'H{row_idx}'].font = Font(name='Calibri', size=11, bold=True, color='FFFFFF')
ws_sum[f'H{row_idx}'].alignment = align_right
ws_sum[f'H{row_idx}'].number_format = '#,##0.000'

for c in cols_sum:
    ws_sum[f'{c}{row_idx}'].fill = fill_grand_total
    ws_sum[f'{c}{row_idx}'].border = border_double_bottom

column_widths_sum = {'B': 8, 'C': 44, 'D': 30, 'E': 34, 'F': 15, 'G': 10, 'H': 18}
for col, width in column_widths_sum.items():
    ws_sum.column_dimensions[col].width = width

# --- SHEET 2: DETAIL INDIVIDUAL ELEMEN ---
ws_det = wb.create_sheet(title='Detail Elemen Model')
ws_det.views.sheetView[0].showGridLines = True

ws_det['B2'] = 'DAFTAR DETAIL ELEMEN BETON DALAM MODEL REVIT'
ws_det['B2'].font = font_title
ws_det['B3'] = 'Daftar per-element ID untuk audit volume dan cross-check BIM'
ws_det['B3'].font = font_subtitle

headers_det = ['No', 'Element ID', 'Kategori', 'Tipe Elemen', 'Dimensi', 'Material', 'Level', 'Volume (m3)']
cols_det = ['B', 'C', 'D', 'E', 'F', 'G', 'H', 'I']

d_row = 5
for c, h in zip(cols_det, headers_det):
    cell = ws_det[f'{c}{d_row}']
    cell.value = h
    cell.font = font_header
    cell.fill = fill_navy
    cell.alignment = align_center
    cell.border = border_thin

for i, rec in enumerate(detailed_records, 1):
    d_row += 1
    ws_det[f'B{d_row}'] = i
    ws_det[f'B{d_row}'].alignment = align_center
    ws_det[f'C{d_row}'] = int(rec['id'])
    ws_det[f'C{d_row}'].alignment = align_center
    ws_det[f'D{d_row}'] = rec['kategori']
    ws_det[f'D{d_row}'].alignment = align_left
    ws_det[f'E{d_row}'] = rec['tipe']
    ws_det[f'E{d_row}'].alignment = align_left
    ws_det[f'F{d_row}'] = rec['dimensi']
    ws_det[f'F{d_row}'].alignment = align_left
    ws_det[f'G{d_row}'] = rec['material']
    ws_det[f'G{d_row}'].alignment = align_left
    ws_det[f'H{d_row}'] = rec['level']
    ws_det[f'H{d_row}'].alignment = align_center
    ws_det[f'I{d_row}'] = round(rec['volume_m3'], 4)
    ws_det[f'I{d_row}'].alignment = align_right
    ws_det[f'I{d_row}'].number_format = '#,##0.000'
    for c in cols_det:
        ws_det[f'{c}{d_row}'].font = font_regular
        ws_det[f'{c}{d_row}'].border = border_thin

d_row += 1
ws_det[f'B{d_row}'] = ''
ws_det[f'C{d_row}'] = ''
ws_det[f'D{d_row}'] = 'TOTAL VOLUME ELEMEN'
ws_det[f'D{d_row}'].font = font_bold
ws_det[f'E{d_row}'] = ''
ws_det[f'F{d_row}'] = ''
ws_det[f'G{d_row}'] = ''
ws_det[f'H{d_row}'] = ''
ws_det[f'I{d_row}'] = f'=SUM(I6:I{d_row-1})'
ws_det[f'I{d_row}'].font = font_bold
ws_det[f'I{d_row}'].alignment = align_right
ws_det[f'I{d_row}'].number_format = '#,##0.000'
for c in cols_det:
    ws_det[f'{c}{d_row}'].fill = fill_subtotal
    ws_det[f'{c}{d_row}'].border = border_double_bottom

column_widths_det = {'B': 8, 'C': 15, 'D': 32, 'E': 34, 'F': 28, 'G': 32, 'H': 15, 'I': 18}
for col, width in column_widths_det.items():
    ws_det.column_dimensions[col].width = width

output_dir = r'C:\AIGRAV'
output_file = os.path.join(output_dir, 'Estimasi_Volume_Beton_OLY-SI18.xlsx')
wb.save(output_file)
print(f'EXCEL_SAVED_AT:{output_file}')
