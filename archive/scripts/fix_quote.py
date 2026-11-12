path = r'c:\AIGRAV\Revit\AIGRAV.RevitBridge\AIGRAV.Skills.BimAudit\BimAuditSkill.cs'
with open(path, 'r', encoding='utf-8') as f:
    text = f.read()

bad = 'x.LokasiFilterDesc.Contains("\'\'") || x.LokasiFilterDesc.Contains("""")'
good = 'x.LokasiFilterDesc.Contains("\'\'") || x.LokasiFilterDesc.Contains("\' \'") || x.LokasiFilterDesc.EndsWith("\'\'")'

if bad in text:
    text = text.replace(bad, good, 1)
    print("Replaced successfully")
else:
    print("bad pattern not found")

with open(path, 'w', encoding='utf-8') as f:
    f.write(text)
