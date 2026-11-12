import sys

path = r'c:\AIGRAV\Revit\AIGRAV.RevitBridge\AIGRAV.Skills.BimAudit\BimAuditSkill.cs'
with open(path, 'r', encoding='utf-8') as f:
    text = f.read()

target = '''        using (Transaction t = new Transaction(doc, "Switch Single Pair Join"))
        {
            t.Start();
            JoinGeometryUtils.SwitchJoinOrder(doc, elemA, elemB);
            doc.Regenerate();
            t.Commit();
        }'''

rep = '''        using (Transaction t = new Transaction(doc, "Switch Single Pair Join"))
        {
            t.Start();
            try {
                JoinGeometryUtils.UnjoinGeometry(doc, elemA, elemB);
                doc.Regenerate();
                JoinGeometryUtils.JoinGeometry(doc, elemA, elemB);
                doc.Regenerate();
            } catch (Exception ex) {
                res += $"Unjoin/Join ex: {ex.Message}\\n";
            }
            t.Commit();
        }'''

if target not in text:
    print('Target not found')
    sys.exit(1)

text = text.replace(target, rep, 1)

with open(path, 'w', encoding='utf-8') as f:
    f.write(text)

print('Successfully patched Unjoin/Join into SwitchSinglePair')
