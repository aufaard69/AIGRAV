$res = & powershell -ExecutionPolicy Bypass -File C:\AIGRAV\call_revit.ps1 -Tool aigrav_list_all_schedules
$res | Where-Object { $_ -match 'Chiller Area 1' -or $_ -match 'Connection Steel' }
