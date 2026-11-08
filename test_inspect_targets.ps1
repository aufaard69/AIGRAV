$s1 = & powershell -ExecutionPolicy Bypass -File C:\AIGRAV\call_revit.ps1 -Tool aigrav_inspect_schedule_def -Arg "C.3.3.14"
Write-Host "=== SCREENSHOT 1 TARGET (C.3.3.14 - Baut) ==="
Write-Host $s1

$s2 = & powershell -ExecutionPolicy Bypass -File C:\AIGRAV\call_revit.ps1 -Tool aigrav_inspect_schedule_def -Arg "C.3.5.10"
Write-Host "=== SCREENSHOT 2 TARGET (C.3.5.10 - Baseplate) ==="
Write-Host $s2

$s3 = & powershell -ExecutionPolicy Bypass -File C:\AIGRAV\call_revit.ps1 -Tool aigrav_inspect_schedule_def -Arg "C.3.5.5"
Write-Host "=== SCREENSHOT 3 TARGET (C.3.5.5 - Pelat Sambungan) ==="
Write-Host $s3
