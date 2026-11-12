import pypdf

reader = pypdf.PdfReader(r'C:\AIGRAV\benchmarks\type36\Prototipe_Type36_72_alt2.pdf')

for p_no in [15, 30, 31, 32, 33]: # 0-indexed: 16, 31, 32, 33, 34
    print(f'================ PAGE {p_no+1} ================')
    print(reader.pages[p_no].extract_text())
