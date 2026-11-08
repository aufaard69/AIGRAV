$res = Invoke-RestMethod -Uri "http://127.0.0.1:8765/" -Method Post -Body '{"tool":"aigrav_find_empty_schedules"}' -ContentType "application/json" -TimeoutSec 60
$res.Result | Out-File -FilePath "C:\AIGRAV\empty_schedules_output.txt" -Encoding utf8
Write-Host "Success, wrote empty_schedules_output.txt"
