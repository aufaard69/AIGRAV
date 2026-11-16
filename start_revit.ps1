$action = New-ScheduledTaskAction -Execute 'C:\Program Files\Autodesk\Revit 2026\Revit.exe' -Argument '"C:\Users\sapuser\Documents\Project1.rvt"'
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
Register-ScheduledTask -TaskName 'OpenRevitInteractive' -Action $action -Settings $settings -Force
Start-ScheduledTask -TaskName 'OpenRevitInteractive'
Write-Output "Task registered and started successfully!"
