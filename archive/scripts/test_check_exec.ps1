$s1 = & powershell -ExecutionPolicy Bypass -File C:\AIGRAV\call_revit.ps1 -Tool aigrav_inspect_schedule_def -Arg "C.3.5.10"
Write-Host "=== C.3.5.10 ==="
Write-Host $s1
$s2 = & powershell -ExecutionPolicy Bypass -File C:\AIGRAV\call_revit.ps1 -Tool aigrav_inspect_schedule_def -Arg "B.1.5"
Write-Host "=== B.1.5 ==="
Write-Host $s2
