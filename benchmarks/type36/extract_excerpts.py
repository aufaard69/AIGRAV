import pypdf
import sys

reader = pypdf.PdfReader(r'C:\AIGRAV\benchmarks\type36\Prototipe_Type36_72_alt2.pdf')

def print_page_lines(page_idx, keywords):
    txt = reader.pages[page_idx].extract_text() or ''
    lines = [l.strip() for l in txt.split('\n') if l.strip()]
    print(f'=== Page {page_idx+1} Excerpts ===')
    for l in lines:
        if any(kw.lower() in l.lower() for kw in keywords):
            print('  ', l)

# Ring Balok details on page 28, 29, 30
print_page_lines(27, ['ring', 'rb', 'kolom', 'balok'])
print_page_lines(28, ['ring', 'rb', 'balok', '142', '158', '300'])
print_page_lines(29, ['ring', 'rb', 'balok', 'detail', '15/'])

# Roof details on page 9, 10, 16, 31, 32, 33, 34, 38
print_page_lines(8, ['gording', 'nok', 'kuda', 'ikatan', '5/10', '6/12', 'gypsum', 'calsiboard'])
print_page_lines(9, ['gording', 'nok', 'kuda', 'ikatan', '5/10', '6/12', 'gypsum', 'calsiboard'])
print_page_lines(15, ['gording', 'nok', 'kuda', 'ikatan', '6/12', '5/10'])
print_page_lines(30, ['gording', 'nok', 'kuda', 'ikatan', '6/12', '5/10'])
print_page_lines(31, ['gording', 'nok', 'kuda', 'ikatan', '6/12', '5/10'])
print_page_lines(32, ['gording', 'nok', 'kuda', 'ikatan', '5/10', '6/12'])
print_page_lines(33, ['gording', 'nok', 'kuda', 'ikatan', '5/10', '6/12'])
print_page_lines(37, ['kayu', 'gording', 'kuda', 'mutu', 'balok'])

# Plafond on page 14, 22
print_page_lines(14, ['calsiboard', 'gypsum', 'plafond', '+'])
print_page_lines(22, ['plafond', 'kalsiboard', 'gypsum'])

# Bathroom on page 18, 19, 20
print_page_lines(18, ['140', '120', 'kamar', 'mandi', 'keramik'])
print_page_lines(19, ['140', '120', 'keramik', 'dinding', 'lantai'])
print_page_lines(20, ['140', '120', 'keramik', 'dinding', 'lantai'])
