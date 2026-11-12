import sys

path = r'c:\AIGRAV\Revit\AIGRAV.RevitBridge\AIGRAV.Skills.BimAudit\BimAuditSkill.cs'
with open(path, 'r', encoding='utf-8') as f:
    text = f.read()

target1 = '|| toolName.Equals("aigrav_switch_join_sloof_s1_s1a", StringComparison.OrdinalIgnoreCase);'
rep1 = '''|| toolName.Equals("aigrav_switch_join_sloof_s1_s1a", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("aigrav_switch_single_pair", StringComparison.OrdinalIgnoreCase);'''

target2 = '''if (toolName.Equals("aigrav_switch_join_sloof_s1_s1a", StringComparison.OrdinalIgnoreCase))
            {
                return SwitchJoinOrderSloofS1AndS1A(doc);
            }'''
rep2 = '''if (toolName.Equals("aigrav_switch_join_sloof_s1_s1a", StringComparison.OrdinalIgnoreCase))
            {
                return SwitchJoinOrderSloofS1AndS1A(doc);
            }
            if (toolName.Equals("aigrav_switch_single_pair", StringComparison.OrdinalIgnoreCase))
            {
                return SwitchSinglePair(doc, argument ?? "6481290,6886055");
            }'''

new_method = '''
    public static string SwitchSinglePair(Document doc, string arg)
    {
        var parts = arg.Split(',');
        var idA = new ElementId(long.Parse(parts[0].Trim()));
        var idB = new ElementId(long.Parse(parts[1].Trim()));
        var elemA = doc.GetElement(idA);
        var elemB = doc.GetElement(idB);

        bool joined = JoinGeometryUtils.AreElementsJoined(doc, elemA, elemB);
        bool aCutsB = JoinGeometryUtils.IsCuttingElementInJoin(doc, elemA, elemB);
        bool bCutsA = JoinGeometryUtils.IsCuttingElementInJoin(doc, elemB, elemA);

        string res = $"BEFORE: Joined={joined}, A cuts B={aCutsB}, B cuts A={bCutsA}\\n";

        using (Transaction t = new Transaction(doc, "Switch Single Pair Join"))
        {
            t.Start();
            JoinGeometryUtils.SwitchJoinOrder(doc, elemA, elemB);
            doc.Regenerate();
            t.Commit();
        }

        bool aCutsBAft = JoinGeometryUtils.IsCuttingElementInJoin(doc, elemA, elemB);
        bool bCutsAAft = JoinGeometryUtils.IsCuttingElementInJoin(doc, elemB, elemA);
        res += $"AFTER: Joined={joined}, A cuts B={aCutsBAft}, B cuts A={bCutsAAft}";
        return res;
    }
'''

if target1 not in text or target2 not in text:
    print('Target not found')
    sys.exit(1)

text = text.replace(target1, rep1, 1)
text = text.replace(target2, rep2, 1)

pos_insert = text.find('public static string SwitchJoinOrderSloofS1AndS1A(Document doc)')
text = text[:pos_insert] + new_method + '\n    ' + text[pos_insert:]

with open(path, 'w', encoding='utf-8') as f:
    f.write(text)

print('Successfully patched SwitchSinglePair')
