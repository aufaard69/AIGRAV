path = r'c:\AIGRAV\Revit\AIGRAV.RevitBridge\AIGRAV.Skills.BimAudit\BimAuditSkill.cs'
with open(path, 'r', encoding='utf-8') as f:
    text = f.read()

bad = '''                string val = "";
                try { if (f.IsStringFilter) val = f.GetStringValue(); else if (f.IsElementIdFilter) val = f.GetElementIdValue().ToString(); } catch { }'''

good = '''                string val = "";
                try { val = f.GetStringValue(); } catch { }'''

if bad in text:
    text = text.replace(bad, good, 1)
    print("Replaced with simple try-catch")
else:
    print("bad pattern not found")

with open(path, 'w', encoding='utf-8') as f:
    f.write(text)
