import pypdf

reader = pypdf.PdfReader(r'C:\AIGRAV\benchmarks\type36\Prototipe_Type36_72_alt2.pdf')

for p_no in [6, 7, 8, 9, 10]: # 0-indexed: 7, 8, 9, 10, 11
    print(f'================ PAGE {p_no+1} ================')
    print(reader.pages[p_no].extract_text())
