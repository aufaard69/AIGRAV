import pypdf

reader = pypdf.PdfReader(r'C:\AIGRAV\benchmarks\type36\Prototipe_Type36_72_alt2.pdf')

def extract_text_with_positions(page_num):
    page = reader.pages[page_num - 1]
    elements = []
    def visitor_body(text, cm, tm, font_dict, font_size):
        if text.strip():
            elements.append((tm[4], tm[5], text.strip()))
    page.extract_text(visitor_text=visitor_body)
    # sort by y descending, then x ascending
    elements.sort(key=lambda item: (-item[1], item[0]))
    return elements

for p in [7, 8, 9, 10]:
    print(f'================ PAGE {p} TEXT POSITIONS ================')
    elems = extract_text_with_positions(p)
    for x, y, text in elems:
        # print if it looks like elevation or dimension
        if any(c in text for c in ['+', '-', '325', '350', '50', '75', '213', '216', '300', '600', '1200', 'TAMPAK', 'POTONGAN']):
            print(f'({x:6.1f}, {y:6.1f}) : {text}')
