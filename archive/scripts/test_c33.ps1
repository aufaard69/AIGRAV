$s1 = & powershell -ExecutionPolicy Bypass -File C:\AIGRAV\call_revit.ps1 -Tool aigrav_inspect_schedule_def -Arg "C.3.3.1"
Write-Host "=== C.3.3.1 ==="
Write-Host $s1
$s2 = & powershell -ExecutionPolicy Bypass -File C:\AIGRAV\call_revit.ps1 -Tool aigrav_inspect_schedule_def -Arg "C.3.3.2"
Write-Host "=== C.3.3.2 ==="
Write-Host $s2
$s3 = & powershell -ExecutionPolicy Bypass -File C:\AIGRAV\call_revit.ps1 -Tool aigrav_inspect_schedule_def -Arg "C.3.3.3"
Write-Host "=== C.3.3.3 ==="
Write-Host $s3
