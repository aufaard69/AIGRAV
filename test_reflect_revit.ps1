Add-Type -Path 'C:\Program Files\Autodesk\Revit 2026\RevitAPI.dll'
$sd = [Autodesk.Revit.DB.ScheduleDefinition]
Write-Host "=== ScheduleDefinition Methods ==="
$sd.GetMethods() | Where-Object { $_.Name -match 'Calculated|Formula|Field' } | ForEach-Object {
    $p = ($_.GetParameters() | ForEach-Object { "$($_.ParameterType.Name) $($_.Name)" }) -join ", "
    Write-Host "$($_.ReturnType.Name) $($_.Name)($p)"
}

$sf = [Autodesk.Revit.DB.ScheduleField]
Write-Host "`n=== ScheduleField Methods & Properties ==="
$sf.GetMembers() | Where-Object { $_.Name -match 'Calculated|Formula|Value' } | ForEach-Object {
    Write-Host "$($_.MemberType): $($_.Name)"
}
