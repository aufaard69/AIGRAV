import pypdf

reader = pypdf.PdfReader(r'C:\AIGRAV\benchmarks\type36\Prototipe_Type36_72_alt2.pdf')

for p in [7, 8, 9, 10]:
    txt = reader.pages[p-1].extract_text()
    lines = [l.strip() for l in txt.split('\n') if l.strip()]
    print(f'=== Page {p} ALL LINES ===')
    for l in lines:
        print('  ', l)
