param(
    [Parameter(Mandatory=$true)]
    [string]$Tool,
    [string]$Argument = ""
)

$payload = @{
    tool = $Tool
    argument = $Argument
} | ConvertTo-Json -Compress

$ws = New-Object -ComObject WScript.Shell
$timer = [System.Timers.Timer]::new(250)
$timer.AutoReset = $false
$action = {
    try {
        & "C:\AIGRAV\MCP\TestServer\venv\Scripts\python.exe" "C:\AIGRAV\wake_revit.py" | Out-Null
        (New-Object -ComObject WScript.Shell).AppActivate("Revit") | Out-Null
    } catch {}
}
$job = Register-ObjectEvent -InputObject $timer -EventName Elapsed -Action $action
$timer.Start()

Start-Process -FilePath "C:\AIGRAV\MCP\TestServer\venv\Scripts\python.exe" -ArgumentList "C:\AIGRAV\wake_revit.py" -WindowStyle Hidden

try {
    $res = Invoke-RestMethod -Uri "http://127.0.0.1:8765/api/execute" -Method Post -ContentType "application/json" -Body $payload -TimeoutSec 180
    if ($res.Success) {
        Write-Output $res.Result
    } else {
        Write-Error $res.Result
    }
} finally {
    $timer.Stop()
    $timer.Dispose()
    Unregister-Event -SourceIdentifier $job.Name -ErrorAction SilentlyContinue
}

