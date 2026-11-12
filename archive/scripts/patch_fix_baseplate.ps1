$path = "C:\AIGRAV\Revit\AIGRAV.RevitBridge\AIGRAV.Skills.BimAudit\BimAuditSkill.cs"
$txt = [System.IO.File]::ReadAllText($path)

$dispatcherTarget = 'if (toolName.Equals("aigrav_exec_subagent1_batch1", StringComparison.OrdinalIgnoreCase))'
$dispatcherReplacement = @"
if (toolName.Equals("aigrav_fix_production_office_baseplates", StringComparison.OrdinalIgnoreCase))
            {
                return FixProductionOfficeBaseplateSchedules(doc);
            }
            if (toolName.Equals("aigrav_exec_subagent1_batch1", StringComparison.OrdinalIgnoreCase))
"@

$methodToAdd = @'

    public static string FixProductionOfficeBaseplateSchedules(Document doc)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== AIGRAV: FIX PRODUCTION OFFICE BASEPLATE SCHEDULES (t=12 vs t=19) ===");

        using (Transaction tx = new(doc, "AIGRAV - Fix Production Office Baseplates"))
        {
            tx.Start();

            // 1. Dapatkan seluruh pelat di Production Office
            var plates = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_StructConnectionPlates)
                .WhereElementIsNotElementType()
                .Where(p =>
                {
                    var pLok = FindParam(p, "Lokasi") ?? FindParam(p, "LOKASI");
                    return pLok != null && pLok.AsString() == "Production Office";
                })
                .ToList();

            sb.AppendLine($"Ditemukan {plates.Count} pelat di Production Office.");
            int updatedT12 = 0;
            int updatedT19 = 0;

            foreach (var p in plates)
            {
                var pThk = FindParam(p, "Thickness") ?? FindParam(p, "Tebal") ?? FindParam(p, "Plate Thickness");
                double thkMm = 0;
                if (pThk != null)
                {
                    if (pThk.StorageType == StorageType.Double) thkMm = pThk.AsDouble() * 304.8;
                    else if (double.TryParse(pThk.AsValueString(), out double d)) thkMm = d;
                }

                var pComm = FindParam(p, "Comments");
                if (pComm != null && !pComm.IsReadOnly)
                {
                    if (Math.Abs(thkMm - 12.0) < 2.0)
                    {
                        pComm.Set("Baseplate Kolom t=12");
                        updatedT12++;
                    }
                    else if (Math.Abs(thkMm - 19.0) < 2.0)
                    {
                        pComm.Set("Baseplate Kolom t=19");
                        updatedT19++;
                    }
                }
            }

            sb.AppendLine($"Update Comments: {updatedT12} unit pelat t=12, {updatedT19} unit pelat t=19.");

            // 2. Update Filter Schedule C.3.4.9 (t=12)
            var sC349 = FindSched(doc, s => s.Name.Equals("C.3.4.9 Production Office - Baseplate Kolom t=12", StringComparison.OrdinalIgnoreCase));
            if (sC349 != null)
            {
                var def = sC349.Definition;
                ScheduleField? fComm = null;
                for (int i = 0; i < def.GetFieldCount(); i++)
                {
                    var f = def.GetField(i);
                    if (f.GetName().Equals("Comments", StringComparison.OrdinalIgnoreCase)) { fComm = f; break; }
                }

                if (fComm != null)
                {
                    for (int i = def.GetFilterCount() - 1; i >= 0; i--)
                    {
                        if (def.GetFilter(i).FieldId == fComm.FieldId) def.RemoveFilter(i);
                    }
                    def.AddFilter(new ScheduleFilter(fComm.FieldId, ScheduleFilterType.Contains, "t=12"));
                    sb.AppendLine("  ✓ Schedule C.3.4.9 filter diperbarui: Comments contains 't=12'");
                }
            }

            // 3. Update Filter Schedule C.3.4.10 (t=19)
            var sC3410 = FindSched(doc, s => s.Name.Equals("C.3.4.10 Production Office - Baseplate Kolom t=19", StringComparison.OrdinalIgnoreCase));
            if (sC3410 != null)
            {
                var def = sC3410.Definition;
                ScheduleField? fComm = null;
                for (int i = 0; i < def.GetFieldCount(); i++)
                {
                    var f = def.GetField(i);
                    if (f.GetName().Equals("Comments", StringComparison.OrdinalIgnoreCase)) { fComm = f; break; }
                }

                if (fComm != null)
                {
                    for (int i = def.GetFilterCount() - 1; i >= 0; i--)
                    {
                        if (def.GetFilter(i).FieldId == fComm.FieldId) def.RemoveFilter(i);
                    }
                    def.AddFilter(new ScheduleFilter(fComm.FieldId, ScheduleFilterType.Contains, "t=19"));
                    sb.AppendLine("  ✓ Schedule C.3.4.10 filter diperbarui: Comments contains 't=19'");
                }
            }

            doc.Regenerate();
            tx.Commit();
        }

        return sb.ToString();
    }
'@

if ($txt.Contains($dispatcherTarget)) {
    $txt = $txt.Replace($dispatcherTarget, $dispatcherReplacement)
}

$canHandleTarget = 'toolName.Equals("aigrav_exec_subagent1_batch1", StringComparison.OrdinalIgnoreCase)'
$canHandleReplacement = 'toolName.Equals("aigrav_fix_production_office_baseplates", StringComparison.OrdinalIgnoreCase) || toolName.Equals("aigrav_exec_subagent1_batch1", StringComparison.OrdinalIgnoreCase)'
if ($txt.Contains($canHandleTarget)) {
    $txt = $txt.Replace($canHandleTarget, $canHandleReplacement)
}

$lastBrace = $txt.LastIndexOf('}')
if ($lastBrace -gt 0) {
    $txt = $txt.Substring(0, $lastBrace) + $methodToAdd + "`n}"
    [System.IO.File]::WriteAllText($path, $txt, [System.Text.Encoding]::UTF8)
    Write-Output "Successfully patched FixProductionOfficeBaseplateSchedules into BimAuditSkill.cs"
} else {
    Write-Error "Could not find closing brace"
}
