import pypdf

reader = pypdf.PdfReader(r'C:\AIGRAV\benchmarks\type36\Prototipe_Type36_72_alt2.pdf')

print('--- Searching for 350 or 3.50 ---')
for i, page in enumerate(reader.pages):
    txt = page.extract_text() or ''
    if '350' in txt or '3.50' in txt:
        print(f'Page {i+1} has 350 / 3.50')
        lines = [l.strip() for l in txt.split('\n') if l.strip()]
        for l in lines:
            if '350' in l or '3.50' in l:
                print('  ', l)
