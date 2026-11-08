$aud1 = & powershell -ExecutionPolicy Bypass -File C:\AIGRAV\call_revit.ps1 -Tool aigrav_qto_audit_framing_columns
Write-Host "=== AUDIT 1: FRAMING & COLUMNS ==="
Write-Host $aud1

$aud2 = & powershell -ExecutionPolicy Bypass -File C:\AIGRAV\call_revit.ps1 -Tool aigrav_qto_audit_connections
Write-Host "=== AUDIT 2: CONNECTIONS & HARDWARE ==="
Write-Host $aud2

$aud3 = & powershell -ExecutionPolicy Bypass -File C:\AIGRAV\call_revit.ps1 -Tool aigrav_qto_audit_reconciliation
Write-Host "=== AUDIT 3: RECONCILIATION & WEIGHT ==="
Write-Host $aud3
