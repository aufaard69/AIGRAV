Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$src = @"
using System;
using System.Runtime.InteropServices;
public class WinUtil4 {
    [DllImport("user32.dll")] public static extern IntPtr OpenDesktop(string s, int f, bool i, uint a);
    [DllImport("user32.dll")] public static extern bool SetThreadDesktop(IntPtr h);
    [DllImport("user32.dll")] public static extern bool CloseDesktop(IntPtr h);
}
"@
Add-Type -TypeDefinition $src

$hDesk = [WinUtil4]::OpenDesktop("Default", 0, $false, 0x01ff)
[WinUtil4]::SetThreadDesktop($hDesk) | Out-Null

$root = [System.Windows.Automation.AutomationElement]::FromHandle([IntPtr]67006)
$cond = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)
$buttons = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)
Write-Output "Found $($buttons.Count) buttons:"
foreach ($b in $buttons) {
    if ($b.Current.Name -like "*AIGRAV*" -or $b.Current.AutomationId -like "*AIGRAV*") {
        Write-Output "MATCH: Name='$($b.Current.Name)', Id='$($b.Current.AutomationId)', Rect=$($b.Current.BoundingRectangle)"
        $pat = $null
        if ($b.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$pat)) {
            $pat.Invoke()
            Write-Output "SUCCESSFULLY INVOKED $($b.Current.Name)!"
        }
    }
}
[WinUtil4]::CloseDesktop($hDesk) | Out-Null
