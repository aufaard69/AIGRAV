$res = Invoke-RestMethod -Uri "http://127.0.0.1:8765/" -Method Post -Body '{"tool":"aigrav_audit_steel_schedule_weights"}' -ContentType "application/json" -TimeoutSec 60
$res.Result | Out-File -FilePath "C:\AIGRAV\audit_weights_output.txt" -Encoding utf8
Write-Host $res.Result
