$res = Invoke-RestMethod -Uri "http://127.0.0.1:8765/" -Method Post -Body '{"tool":"aigrav_fix_pondasi_comments"}' -ContentType "application/json" -TimeoutSec 60
$res.Result | Out-File -FilePath "C:\AIGRAV\fix_pondasi_output.txt" -Encoding utf8
Write-Host $res.Result
