import pypdf
import sys

print('Loading PDF...')
sys.stdout.flush()
reader = pypdf.PdfReader(r'C:\AIGRAV\benchmarks\type36\Prototipe_Type36_72_alt2.pdf')
pages_text = []
for idx, p in enumerate(reader.pages):
    txt = p.extract_text() or ''
    pages_text.append(txt)
print(f'Loaded {len(pages_text)} pages.')
sys.stdout.flush()

def search_kw(title, kws):
    print(f'=== {title} ===')
    for i, txt in enumerate(pages_text):
        found = [kw for kw in kws if kw.lower() in txt.lower()]
        if found:
            print(f'Page {i+1}: {found}')
    sys.stdout.flush()

search_kw('Ring Balok', ['ring balok', 'ring balk', 'rb 15', 'balok ring', '3.25', '3.50'])
search_kw('Gording & Rangka Atap', ['gording', 'kuda-kuda', 'ikatan angin', 'nok', 'ampig', '5/10', '6/12'])
search_kw('Plafond', ['plafond', 'calsiboard', 'kalsiboard', 'gypsum'])
search_kw('Kamar Mandi & Grid', ['kamar mandi', '141', '142', '140', '159', '158'])
