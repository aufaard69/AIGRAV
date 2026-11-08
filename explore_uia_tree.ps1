Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

[IntPtr]$hwnd = [IntPtr]67210

try {
    $element = [System.Windows.Automation.AutomationElement]::FromHandle($hwnd)
    if ($element) {
        Write-Host "Success getting AutomationElement from HWND 67210!"
        Write-Host "Name: $($element.Current.Name)"
        Write-Host "ControlType: $($element.Current.ControlType.ProgrammaticName)"
        Write-Host "ClassName: $($element.Current.ClassName)"
        Write-Host "FrameworkId: $($element.Current.FrameworkId)"
        Write-Host "IsContentElement: $($element.Current.IsContentElement)"
        
        # Enumerate children
        $children = $element.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
        Write-Host "Children count: $($children.Count)"
        foreach ($c in $children) {
            Write-Host "  Child: Name='$($c.Current.Name)', Type='$($c.Current.ControlType.ProgrammaticName)', Class='$($c.Current.ClassName)'"
        }
    } else {
        Write-Host "AutomationElement::FromHandle returned null."
    }
} catch {
    Write-Host "Error: $_"
}
