$s1 = & powershell -ExecutionPolicy Bypass -File C:\AIGRAV\call_revit.ps1 -Tool aigrav_inspect_schedule_def -Arg "B.1.3"
Write-Host "=== B.1.3 ==="
Write-Host $s1
$s2 = & powershell -ExecutionPolicy Bypass -File C:\AIGRAV\call_revit.ps1 -Tool aigrav_inspect_schedule_def -Arg "B.1.1"
Write-Host "=== B.1.1 ==="
Write-Host $s2
