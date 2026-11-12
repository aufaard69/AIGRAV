Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$src = @"
using System;
using System.Runtime.InteropServices;
public class WinUtil {
    [DllImport("user32.dll")] public static extern IntPtr OpenDesktop(string s, int f, bool i, uint a);
    [DllImport("user32.dll")] public static extern bool SetThreadDesktop(IntPtr h);
    [DllImport("user32.dll")] public static extern bool CloseDesktop(IntPtr h);
    [DllImport("user32.dll")] public static extern bool EnumDesktopWindows(IntPtr h, EnumWindowsProc p, IntPtr l);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint p);
    [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr h, System.Text.StringBuilder s, int c);
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
}
"@
Add-Type -TypeDefinition $src

$hDesk = [WinUtil]::OpenDesktop("Default", 0, $false, 0x01ff)
[WinUtil]::SetThreadDesktop($hDesk) | Out-Null

$revitHwnd = [IntPtr]::Zero
$cb = [WinUtil+EnumWindowsProc]{
    param($hwnd, $lParam)
    $sb = New-Object System.Text.StringBuilder 256
    [WinUtil]::GetWindowText($hwnd, $sb, 256) | Out-Null
    $txt = $sb.ToString()
    if ($txt -like "*Autodesk Revit*") {
        $script:revitHwnd = $hwnd
        return $false
    }
    return $true
}

[WinUtil]::EnumDesktopWindows($hDesk, $cb, [IntPtr]::Zero) | Out-Null
Write-Output "Found Revit HWND: $revitHwnd"

if ($revitHwnd -ne [IntPtr]::Zero) {
    $root = [System.Windows.Automation.AutomationElement]::FromHandle($revitHwnd)
    Write-Output "Root: $($root.Current.Name)"
    $cond = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty, "AIGRAV")
    $btn = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
    if ($btn -ne $null) {
        Write-Output "Found AIGRAV button! Type: $($btn.Current.ControlType.ProgrammaticName)"
        $pattern = $null
        if ($btn.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$pattern)) {
            $pattern.Invoke()
            Write-Output "Invoked AIGRAV button via InvokePattern!"
        } elseif ($btn.TryGetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern, [ref]$pattern)) {
            $pattern.Toggle()
            Write-Output "Toggled AIGRAV button via TogglePattern!"
        }
    } else {
        Write-Output "Button AIGRAV not found. Searching for tabs..."
        $tabCond = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::TabItem)
        $tabs = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $tabCond)
        foreach ($t in $tabs) {
            Write-Output "Tab: $($t.Current.Name)"
            if ($t.Current.Name -eq "AIGRAV") {
                $selPattern = $null
                if ($t.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$selPattern)) {
                    $selPattern.Select()
                    Write-Output "Selected AIGRAV tab!"
                }
            }
        }
    }
}
[WinUtil]::CloseDesktop($hDesk) | Out-Null
