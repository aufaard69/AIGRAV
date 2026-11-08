$ws = New-Object -ComObject WScript.Shell
$success = $ws.AppActivate("Antigravity")
Write-Host "AppActivate Antigravity success: $success"
