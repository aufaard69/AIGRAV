$b1 = & powershell -ExecutionPolicy Bypass -File C:\AIGRAV\call_revit.ps1 -Tool aigrav_exec_fix_batch1
Write-Host "=== BATCH 1 RESULT ==="
Write-Host $b1

$b2 = & powershell -ExecutionPolicy Bypass -File C:\AIGRAV\call_revit.ps1 -Tool aigrav_exec_fix_batch2
Write-Host "=== BATCH 2 RESULT ==="
Write-Host $b2
