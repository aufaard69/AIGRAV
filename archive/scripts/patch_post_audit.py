path = r'c:\AIGRAV\Revit\AIGRAV.RevitBridge\AIGRAV.Skills.BimAudit\BimAuditSkill.cs'
with open(path, 'r', encoding='utf-8') as f:
    text = f.read()

# 1. Add TableData inspection to InspectCalculatedFieldApi
target1 = '''        sb.AppendLine("\\n[ScheduleDefinition Reflection - Calculated / Formula Methods]:");'''

rep1 = '''        try
        {
            var td = sched.GetTableData();
            var tsd = td.GetSectionData(SectionType.Body);
            sb.AppendLine($"\\n[Schedule Table Body: {tsd.NumberOfRows} rows x {tsd.NumberOfColumns} columns]:");
            for (int r = 0; r < Math.Min(tsd.NumberOfRows, 15); r++)
            {
                var rowVals = new List<string>();
                for (int c = 0; c < tsd.NumberOfColumns; c++)
                {
                    rowVals.Add(tsd.GetCellText(r, c));
                }
                sb.AppendLine($"Row {r:D2}: [ {string.Join(" | ", rowVals)} ]");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"\\nCould not get TableData: {ex.Message}");
        }

        sb.AppendLine("\\n[ScheduleDefinition Reflection - Calculated / Formula Methods]:");'''

if target1 in text:
    text = text.replace(target1, rep1, 1)

with open(path, 'w', encoding='utf-8') as f:
    f.write(text)

print('Successfully patched post-audit inspection')
