$rec = & powershell -ExecutionPolicy Bypass -File C:\AIGRAV\call_revit.ps1 -Tool aigrav_qto_audit_reconciliation
Write-Host $rec
