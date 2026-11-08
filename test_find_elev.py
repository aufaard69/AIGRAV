import sys

path = r'c:\AIGRAV\Revit\AIGRAV.RevitBridge\AIGRAV.Skills.BimAudit\BimAuditSkill.cs'
with open(path, 'r', encoding='utf-8') as f:
    text = f.read()

# Replace the condition in AuditSloofS1AndS1AFloors and SwitchJoinOrderSloofS1AndS1A
# We want to check:
# 1) Floor is on Level "1ST FLOOR" (or offset is 0, or level elevation <= 1.55m)
# 2) Both S1 200 X 300 and S1-A 150 X 200

old_condition = 'bool isElev0 = Math.Abs(flInfo.ElevM) < 0.05;'
# Let's find where elev0 is used
print('Found occurrences of flData.ElevM:')
import re
for m in re.finditer(r'flData\.ElevM', text):
    print(m.start())

