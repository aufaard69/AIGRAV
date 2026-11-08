import sys

path = r'c:\AIGRAV\Revit\AIGRAV.RevitBridge\AIGRAV.Skills.BimAudit\BimAuditSkill.cs'
with open(path, 'r', encoding='utf-8') as f:
    text = f.read()

target = 'sb.AppendLine($"    * S1-A 150 X 200 : {elev0Pairs.Count(x => x.sloofType.Contains("S1-A"))} pasang (Sloof potong Pelat: {elev0Pairs.Count(x => x.sloofType.Contains("S1-A") && x.sloofCutsFloor)}, Pelat potong Sloof: {elev0Pairs.Count(x => x.sloofType.Contains("S1-A") && x.floorCutsSloof)})");'

rep = target + '''
        foreach (var p in elev0Pairs.Where(x => x.floorCutsSloof))
        {
            sb.AppendLine($"      [Sisa Pelat Potong Sloof]: Sloof ID {p.sloof.Id.Value} ({p.sloofType}) vs Floor ID {p.floor.Id.Value} ({p.floorType})");
        }'''

if target not in text:
    print('Target not found')
    sys.exit(1)

text = text.replace(target, rep, 1)

with open(path, 'w', encoding='utf-8') as f:
    f.write(text)

print('Successfully patched details for remaining floor cuts sloof pair')
