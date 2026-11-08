<#
.SYNOPSIS
    AIGRAV System Health & Diagnostic Verifier
    Checks Revit Bridge, Databases, MCP Server, and Antigravity Configuration.
#>

[CmdletBinding()]
param (
    [string]$RootPath = "C:\AIGRAV"
)

Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host "                AIGRAV HEALTH & DIAGNOSTIC CHECK                 " -ForegroundColor White
Write-Host "=================================================================" -ForegroundColor Cyan

$SuccessCount = 0
$TotalCount = 0

function Check-Item {
    param([string]$Title, [scriptblock]$Test)
    $script:TotalCount++
    try {
        $result = & $Test
        if ($result.Success) {
            $script:SuccessCount++
            Write-Host " [PASS] $Title" -ForegroundColor Green
            if ($result.Message) { Write-Host "        $($result.Message)" -ForegroundColor DarkGray }
        } else {
            Write-Host " [FAIL] $Title" -ForegroundColor Red
            if ($result.Message) { Write-Host "        $($result.Message)" -ForegroundColor Yellow }
        }
    } catch {
        Write-Host " [ERR]  $Title - $_" -ForegroundColor Red
    }
}

# 1. Connection Database v1
Check-Item "Steel Connection Database (JSON v1)" {
    $path = Join-Path $RootPath "KnowledgeBase\SteelConnectionDatabase\AIGRAV_SteelConnectionDatabase_v1.json"
    if (Test-Path $path) {
        $len = (Get-Item $path).Length
        return @{ Success = $true; Message = "$path ($([math]::Round($len/1KB, 1)) KB)" }
    }
    return @{ Success = $false; Message = "Not found at $path" }
}

# 2. Connection Database
Check-Item "Steel Connection Database (JSON)" {
    $path = Join-Path $RootPath "KnowledgeBase\SteelConnectionDatabase\AIGRAV.ConnectionDatabase.json"
    if (Test-Path $path) {
        return @{ Success = $true; Message = "Found at $path" }
    }
    return @{ Success = $false; Message = "Not found at $path" }
}

# 3. Steel Profile Catalog
Check-Item "Steel Profile Master Catalog (CSV)" {
    $path = Join-Path $RootPath "Revit\AIGRAV.SteelDatabase.csv"
    if (Test-Path $path) {
        $lines = (Get-Content $path | Measure-Object -Line).Lines
        return @{ Success = $true; Message = "$path ($lines profile entries)" }
    }
    return @{ Success = $false; Message = "Not found at $path" }
}

# 4. Connection Library Families
Check-Item "Revit Connection Family: BasePlate" {
    $path = Join-Path $RootPath "ConnectionLibrary\AIGRAV_BasePlate_WF400.rfa"
    if (Test-Path $path) {
        return @{ Success = $true; Message = "Found at $path" }
    }
    return @{ Success = $false; Message = "Not found at $path" }
}

Check-Item "Revit Connection Family: EndPlate" {
    $path = Join-Path $RootPath "ConnectionLibrary\AIGRAV_EndPlate_WF400.rfa"
    if (Test-Path $path) {
        return @{ Success = $true; Message = "Found at $path" }
    }
    return @{ Success = $false; Message = "Not found at $path" }
}

# 5. Revit Bridge Binaries
Check-Item "Revit Bridge Core DLL" {
    $path = Join-Path $RootPath "Revit\AIGRAV.RevitBridge\AIGRAV.RevitBridge\bin\x64\Debug\net10.0-windows\AIGRAV.RevitBridge.dll"
    if (Test-Path $path) {
        return @{ Success = $true; Message = "Found at $path" }
    }
    return @{ Success = $false; Message = "Not found at $path" }
}

Check-Item "Revit Skills Assemblies" {
    $skillsDir = Join-Path $RootPath "Revit\AIGRAV.RevitBridge\AIGRAV.RevitBridge\bin\x64\Debug\net10.0-windows\Skills"
    if (Test-Path $skillsDir) {
        $dlls = (Get-ChildItem -Path $skillsDir -Filter "AIGRAV.Skills.*.dll").Count
        return @{ Success = ($dlls -gt 0); Message = "$dlls skills found in $skillsDir" }
    }
    return @{ Success = $false; Message = "Skills dir not found: $skillsDir" }
}

# 6. Revit Addin Manifest
Check-Item "Revit .addin Manifest (AppData)" {
    $found = @()
    Get-ChildItem -Path (Join-Path $env:APPDATA "Autodesk\Revit\Addins") -Recurse -Filter "AIGRAV.addin" -ErrorAction SilentlyContinue | ForEach-Object {
        $found += $_.FullName
    }
    if ($found.Count -gt 0) {
        return @{ Success = $true; Message = "Found in: $($found -join '; ')" }
    }
    return @{ Success = $false; Message = "No AIGRAV.addin found in %APPDATA%\Autodesk\Revit\Addins" }
}

# 7. Python Venv and MCP module
Check-Item "Python MCP Virtual Environment" {
    $venvPy = Join-Path $RootPath "MCP\TestServer\venv\Scripts\python.exe"
    if (Test-Path $venvPy) {
        $res = & $venvPy -c "import mcp; print('MCP_OK')" 2>&1
        if ($res -match "MCP_OK") {
            return @{ Success = $true; Message = "Python and MCP module ready ($venvPy)" }
        }
        return @{ Success = $false; Message = "mcp module failed to import: $res" }
    }
    return @{ Success = $false; Message = "Venv python not found at $venvPy" }
}

# 8. Antigravity MCP Config
Check-Item "Antigravity MCP Configuration (mcp_config.json)" {
    $cfgPath = Join-Path $env:USERPROFILE ".gemini\config\mcp_config.json"
    if (Test-Path $cfgPath) {
        $raw = Get-Content -Path $cfgPath -Raw
        if ($raw -match "aigrav-test") {
            return @{ Success = $true; Message = "'aigrav-test' server found in $cfgPath" }
        }
        return @{ Success = $false; Message = "'aigrav-test' entry missing in $cfgPath" }
    }
    return @{ Success = $false; Message = "mcp_config.json not found at $cfgPath" }
}

# 9. Antigravity Plugin
Check-Item "Antigravity Plugin (aigrav-bim)" {
    $pluginDir = Join-Path $env:USERPROFILE ".gemini\config\plugins\aigrav-bim"
    $pluginJson = Join-Path $pluginDir "plugin.json"
    if ((Test-Path $pluginDir) -and (Test-Path $pluginJson)) {
        return @{ Success = $true; Message = "Plugin present at $pluginDir" }
    }
    return @{ Success = $false; Message = "Plugin not found at $pluginDir" }
}

# 10. Revit Bridge HTTP Live Connection (Optional / If Revit is running)
Check-Item "Revit Bridge HTTP Listener (http://127.0.0.1:8765/)" {
    try {
        $req = [System.Net.WebRequest]::Create("http://127.0.0.1:8765/")
        $req.Timeout = 2000
        $req.Method = "POST"
        $body = [System.Text.Encoding]::UTF8.GetBytes('{"tool":"aigrav_ping"}')
        $req.ContentType = "application/json"
        $req.ContentLength = $body.Length
        $st = $req.GetRequestStream()
        $st.Write($body, 0, $body.Length)
        $st.Close()
        $resp = $req.GetResponse()
        $reader = New-Object System.IO.StreamReader($resp.GetResponseStream())
        $content = $reader.ReadToEnd()
        $reader.Close()
        $resp.Close()
        return @{ Success = $true; Message = "Revit Bridge is LIVE and responsive: $content" }
    } catch {
        return @{ Success = $false; Message = "Revit is not currently open or Bridge not running yet (Normal if Revit is closed)." }
    }
}

Write-Host "-----------------------------------------------------------------" -ForegroundColor Cyan
Write-Host "Diagnostic Result: $SuccessCount / $TotalCount items verified." -ForegroundColor $(if ($SuccessCount -ge ($TotalCount - 1)) { "Green" } else { "Yellow" })
Write-Host "=================================================================" -ForegroundColor Cyan
