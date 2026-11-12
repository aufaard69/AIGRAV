Add-Type @"
using System;
using System.Text;
using System.Runtime.InteropServices;

public class WinAPI {
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    public static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
}
"@

$list = New-Object System.Collections.ArrayList

[WinAPI]::EnumWindows({
    param($hWnd, $lParam)
    if ([WinAPI]::IsWindowVisible($hWnd)) {
        $len = [WinAPI]::GetWindowTextLength($hWnd)
        if ($len -gt 0) {
            $sb = New-Object System.Text.StringBuilder ($len + 1)
            [WinAPI]::GetWindowText($hWnd, $sb, $sb.Capacity) | Out-Null
            $pidVal = 0
            [WinAPI]::GetWindowThreadProcessId($hWnd, [ref]$pidVal) | Out-Null
            $p = Get-Process -Id $pidVal -ErrorAction SilentlyContinue
            if ($p -and ($p.ProcessName -like "*Antigravity*" -or $p.ProcessName -like "*code*")) {
                Write-Output "$($p.ProcessName) ($($p.Id)): $($sb.ToString())"
            }
        }
    }
    return $true
}, [IntPtr]::Zero)
