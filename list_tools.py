import re

with open(r"C:\AIGRAV\Revit\AIGRAV.RevitBridge\AIGRAV.RevitBridge\AIGRAVToolExecutor.cs", "r", encoding="utf-8") as f:
    content = f.read()

cases = re.findall(r'case "([^"]+)":', content)
print(f"Total tools: {len(cases)}")
for c in sorted(set(cases)):
    print(c)
