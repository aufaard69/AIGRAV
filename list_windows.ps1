Add-Type @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public class WindowFinder {
    [DllImport("user32.dll")]
    public static extern bool EnumThreadWindows(int dwThreadId, EnumThreadDelegate lpfn, IntPtr lParam);
    public delegate bool EnumThreadDelegate(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);
}
'@
$revit = Get-Process Revit
foreach ($thread in $revit.Threads) {
    [WindowFinder]::EnumThreadWindows($thread.Id, [WindowFinder+EnumThreadDelegate]{
        param($hwnd, $lparam)
        $sb = New-Object System.Text.StringBuilder 256
        [WindowFinder]::GetWindowText($hwnd, $sb, 256) | Out-Null
        $title = $sb.ToString()
        if ([WindowFinder]::IsWindowVisible($hwnd) -and $title.Length -gt 0) {
            Write-Host "Window: $title"
        }
        return $true
    }, [IntPtr]::Zero) | Out-Null
}
