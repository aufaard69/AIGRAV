import re

with open(r'c:\AIGRAV\Revit\AIGRAV.RevitBridge\AIGRAV.Skills.BimAudit\BimAuditSkill.cs', 'r', encoding='utf-8') as f:
    text = f.read()

tools = set(re.findall(r'toolName\.Equals\("([^"]+)"', text))
print("Registered tools:")
for t in sorted(tools):
    print(" -", t)
