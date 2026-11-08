$path = "C:\AIGRAV\Revit\AIGRAV.RevitBridge\AIGRAV.Skills.BimAudit\BimAuditSkill.cs"
$txt = [System.IO.File]::ReadAllText($path)
$target = 'if (toolName.Equals("aigrav_generate_location_bq_schedules", StringComparison.OrdinalIgnoreCase))'
$replacement = @"
if (toolName.Equals("aigrav_exec_subagent1_batch1", StringComparison.OrdinalIgnoreCase))
            {
                return ExecuteBatch1_CartonCupsAndProductionOffice(doc);
            }
            if (toolName.Equals("aigrav_exec_subagent2_batch2", StringComparison.OrdinalIgnoreCase))
            {
                return ExecuteBatch2_WaterlineAndPowerHouse(doc);
            }
            if (toolName.Equals("aigrav_exec_subagent3_batch3", StringComparison.OrdinalIgnoreCase))
            {
                return ExecuteBatch3_ChillerAndHousekeeping(doc);
            }
            if (toolName.Equals("aigrav_qa_audit_all_schedules", StringComparison.OrdinalIgnoreCase))
            {
                return QaAuditAllLocationSchedules(doc);
            }
            if (toolName.Equals("aigrav_cleanup_orphaned_copies", StringComparison.OrdinalIgnoreCase))
            {
                return CleanupOrphanedCopies(doc);
            }
            if (toolName.Equals("aigrav_apply_anti_double_counting", StringComparison.OrdinalIgnoreCase))
            {
                return ApplyAntiDoubleCountingToBangunanUtama(doc);
            }
            if (toolName.Equals("aigrav_generate_location_bq_schedules", StringComparison.OrdinalIgnoreCase))
"@

if ($txt.Contains($target)) {
    $newTxt = $txt.Replace($target, $replacement)
    [System.IO.File]::WriteAllText($path, $newTxt, [System.Text.Encoding]::UTF8)
    Write-Output "Successfully updated dispatcher in BimAuditSkill.cs"
} else {
    Write-Error "Target not found in BimAuditSkill.cs"
}
