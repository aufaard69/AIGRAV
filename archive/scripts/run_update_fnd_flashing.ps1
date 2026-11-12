$res = Invoke-RestMethod -Uri "http://127.0.0.1:8765/" -Method Post -Body '{"tool":"aigrav_update_foundation_and_flashing_schedules"}' -ContentType "application/json" -TimeoutSec 60
$res.Result | Out-File -FilePath "C:\AIGRAV\update_fnd_flashing_output.txt" -Encoding utf8
Write-Host $res.Result
