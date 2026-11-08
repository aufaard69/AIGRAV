import sys

path = r'c:\AIGRAV\Revit\AIGRAV.RevitBridge\AIGRAV.Skills.BimAudit\BimAuditSkill.cs'
with open(path, 'r', encoding='utf-8') as f:
    text = f.read()

target = 'sb.AppendLine($"    * S1-A 150 X 200 : {elev0Pairs.Count(x => x.sloofType.Contains("S1-A"))}'
idx = text.find(target)
if idx != -1:
    print('Found target at', idx)
    # print context
    print(text[idx-100:idx+300])

