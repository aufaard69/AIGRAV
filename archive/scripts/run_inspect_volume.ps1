$res = Invoke-RestMethod -Uri "http://127.0.0.1:8765/" -Method Post -Body '{"tool":"aigrav_inspect_steel_volume_calibration"}' -ContentType "application/json" -TimeoutSec 60
$res.Result | Out-File -FilePath "C:\AIGRAV\inspect_volume_output.txt" -Encoding utf8
Write-Host "Success, wrote inspect_volume_output.txt"
