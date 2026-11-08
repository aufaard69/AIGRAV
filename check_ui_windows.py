import subprocess

ps_script = """
Add-Type -AssemblyName UIAutomationClient
$root = [System.Windows.Automation.AutomationElement]::RootElement
$pids = @(7156, 21888, 27204)
$children = $root.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
foreach ($c in $children) {
    if ($pids -contains $c.Current.ProcessId) {
        Write-Output "$($c.Current.ProcessId): $($c.Current.Name)"
    }
}
"""

res = subprocess.run(["powershell", "-NoProfile", "-Command", ps_script], capture_output=True, text=True)
print(res.stdout)
