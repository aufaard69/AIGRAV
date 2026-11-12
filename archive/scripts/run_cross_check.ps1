$body = @{ tool = "aigrav_cross_check_schedules" } | ConvertTo-Json
$res = Invoke-RestMethod -Uri "http://127.0.0.1:8765/" -Method Post -Body $body -ContentType "application/json" -TimeoutSec 60
$res.Result | Out-File -FilePath "C:\AIGRAV\cross_check_output.txt" -Encoding utf8
Write-Host "Success, wrote cross check output."
