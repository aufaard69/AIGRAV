$path = "C:\AIGRAV\Revit\AIGRAV.RevitBridge\AIGRAV.Skills.BimAudit\BimAuditSkill.cs"
$txt = [System.IO.File]::ReadAllText($path)

# 1. Update Baseplate section in CreateStructuredSchedule
$oldBaseplate = @'
                else if (itemKind == "Baseplate")
                {
                    ScheduleField? fComm = null;
                    for (int i = 0; i < def.GetFieldCount(); i++)
                    {
                        var f = def.GetField(i);
                        if (f.GetName().Equals("Comments", StringComparison.OrdinalIgnoreCase))
                        {
                            fComm = f;
                            break;
                        }
                    }
                    if (fComm == null)
                    {
                        var sf = def.GetSchedulableFields().FirstOrDefault(x => x.GetName(doc).Equals("Comments", StringComparison.OrdinalIgnoreCase));
                        if (sf != null) fComm = def.AddField(sf);
                    }
                    if (fComm != null)
                    {
                        def.AddFilter(new ScheduleFilter(fComm.FieldId, ScheduleFilterType.Contains, profileFilter));
                    }
                }
'@

$newBaseplate = @'
                else if (itemKind == "Baseplate")
                {
                    // Hide blank 'Type of Connection' column
                    for (int i = 0; i < def.GetFieldCount(); i++)
                    {
                        var f = def.GetField(i);
                        if (f.GetName().IndexOf("Type of Connection", StringComparison.OrdinalIgnoreCase) >= 0 || f.GetName().IndexOf("Tipe Sambungan", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            f.IsHidden = true;
                            break;
                        }
                    }

                    ScheduleField? fComm = null;
                    for (int i = 0; i < def.GetFieldCount(); i++)
                    {
                        var f = def.GetField(i);
                        if (f.GetName().Equals("Comments", StringComparison.OrdinalIgnoreCase))
                        {
                            fComm = f;
                            break;
                        }
                    }
                    if (fComm == null)
                    {
                        var sf = def.GetSchedulableFields().FirstOrDefault(x => x.GetName(doc).Equals("Comments", StringComparison.OrdinalIgnoreCase));
                        if (sf != null) fComm = def.AddField(sf);
                    }
                    if (fComm != null)
                    {
                        fComm.ColumnHeading = "Keterangan";
                        fComm.IsHidden = false;
                        def.AddFilter(new ScheduleFilter(fComm.FieldId, ScheduleFilterType.Contains, profileFilter));
                    }
                }
'@

if ($txt.Contains($oldBaseplate)) {
    $txt = $txt.Replace($oldBaseplate, $newBaseplate)
}

# 2. Fix QaAuditAllLocationSchedules colCount
$oldQaScope = @'
            int elemCount = 0;
            int rowCount = 0;
            try
            {
                var td = s.GetTableData();
                var sd = td?.GetSectionData(SectionType.Body);
                rowCount = sd?.NumberOfRows ?? 0;
                elemCount = new FilteredElementCollector(doc, s.Id).WhereElementIsNotElementType().GetElementCount();
            }
            catch { }

            var filterDetails = new List<string>();
            for (int i = 0; i < filterCount; i++)
            {
                var f = def.GetFilter(i);
                var field = def.GetField(f.FieldId);
                string fName = field?.GetName() ?? f.FieldId.ToString();
                string fVal = f.GetStringValue();
                filterDetails.Add($"{fName} {f.FilterType} '{fVal}'");
            }
            string filterDesc = filterCount >= 1 ? $"PASS ({string.Join(", ", filterDetails)})" : "FAIL (No Filter)";
            if (filterCount < 1) totalFilterIssues++;
            if (elemCount == 0) totalZero++; else totalPass++;

            string catName = s.Category?.Name ?? def.CategoryId.Value.ToString();
            int colCount = sd?.NumberOfColumns ?? 0;
'@

$newQaScope = @'
            int elemCount = 0;
            int rowCount = 0;
            int colCount = 0;
            try
            {
                var td = s.GetTableData();
                var sd = td?.GetSectionData(SectionType.Body);
                rowCount = sd?.NumberOfRows ?? 0;
                colCount = sd?.NumberOfColumns ?? 0;
                elemCount = new FilteredElementCollector(doc, s.Id).WhereElementIsNotElementType().GetElementCount();
            }
            catch { }

            var filterDetails = new List<string>();
            for (int i = 0; i < filterCount; i++)
            {
                var f = def.GetFilter(i);
                var field = def.GetField(f.FieldId);
                string fName = field?.GetName() ?? f.FieldId.ToString();
                string fVal = f.GetStringValue();
                filterDetails.Add($"{fName} {f.FilterType} '{fVal}'");
            }
            string filterDesc = filterCount >= 1 ? $"PASS ({string.Join(", ", filterDetails)})" : "FAIL (No Filter)";
            if (filterCount < 1) totalFilterIssues++;
            if (elemCount == 0) totalZero++; else totalPass++;

            string catName = s.Category?.Name ?? def.CategoryId.Value.ToString();
'@

if ($txt.Contains($oldQaScope)) {
    $txt = $txt.Replace($oldQaScope, $newQaScope)
}

[System.IO.File]::WriteAllText($path, $txt, [System.Text.Encoding]::UTF8)
Write-Output "Successfully updated Baseplate and QA Audit scopes in BimAuditSkill.cs"
