import pypdf

reader = pypdf.PdfReader(r'C:\AIGRAV\benchmarks\type36\Prototipe_Type36_72_alt2.pdf')

print('================ PAGE 6 (DENAH ARSITEKTUR) ================')
page6 = reader.pages[5]

elems = []
def visitor_body(text, cm, tm, font_dict, font_size):
    if text.strip():
        elems.append((tm[4], tm[5], text.strip()))
page6.extract_text(visitor_text=visitor_body)
elems.sort(key=lambda item: (-item[1], item[0]))
for x, y, t in elems:
    print(f'({x:6.1f}, {y:6.1f}) : {t}')
