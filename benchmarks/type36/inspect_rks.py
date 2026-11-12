import pypdf

reader = pypdf.PdfReader(r'C:\AIGRAV\benchmarks\type36\Prototipe_Type36_72_alt2.pdf')

print('================ PAGE 23 (RKS ARSITEKTUR) ================')
print(reader.pages[22].extract_text())
print('================ PAGE 38 (RKS STRUKTUR) ================')
print(reader.pages[37].extract_text())
