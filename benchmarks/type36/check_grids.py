import pypdf

reader = pypdf.PdfReader(r'C:\AIGRAV\benchmarks\type36\Prototipe_Type36_72_alt2.pdf')

for p_no in [23, 25, 26, 28]: # pages 24, 26, 27, 29
    p = reader.pages[p_no]
    elems = []
    def visitor_body(text, cm, tm, font_dict, font_size):
        if text.strip():
            elems.append((tm[4], tm[5], text.strip()))
    p.extract_text(visitor_text=visitor_body)
    elems.sort(key=lambda item: (-item[1], item[0]))
    print(f'=== Page {p_no+1} Grids & Dimensions ===')
    for x, y, t in elems:
        if any(c in t for c in ['A', 'B', 'C', 'D', 'E', '1', '2', '3', '4']) and len(t) <= 4:
            print(f'  ({x:6.1f}, {y:6.1f}) : {t}')
