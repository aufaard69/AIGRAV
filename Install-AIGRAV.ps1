<#
.SYNOPSIS
    AIGRAV Automated One-Click Installer & Configurator
    Installs AIGRAV Suite, Revit Bridge Add-in, KnowledgeBase Databases,
    MCP Server, and Antigravity Plugin on any target Windows PC.

.PARAMETER DestinationPath
    The target installation root directory (default: C:\AIGRAV).

.PARAMETER TargetRevitYears
    Revit versions to register the add-in for (default: 2024, 2025, 2026, 2027).

.PARAMETER SkipPythonVenv
    Skip setting up or upgrading the Python virtual environment.
#>

[CmdletBinding()]
param (
    [string]$DestinationPath = "C:\AIGRAV",
    [string[]]$TargetRevitYears = @("2024", "2025", "2026", "2027"),
    [switch]$SkipPythonVenv,
    [switch]$Silent
)

$ErrorActionPreference = "Continue"

function Write-Step {
    param([string]$Message)
    Write-Host "`n[AIGRAV-INSTALLER] >>> $Message" -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-Host "  [OK] $Message" -ForegroundColor Green
}

function Write-WarnMsg {
    param([string]$Message)
    Write-Host "  [WARN] $Message" -ForegroundColor Yellow
}

function Write-Fail {
    param([string]$Message)
    Write-Host "  [FAIL] $Message" -ForegroundColor Red
}

Clear-Host
Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host "           AIGRAV ONE-CLICK INSTALLER & CONFIGURATOR             " -ForegroundColor White
Write-Host "    AI-Powered Revit BIM, Structural Engineering & MCP Suite     " -ForegroundColor DarkCyan
Write-Host "=================================================================" -ForegroundColor Cyan

# -------------------------------------------------------------
# STEP 1: Determine Source & Destination Directories
# -------------------------------------------------------------
Write-Step "Step 1: Analyzing Source and Target Directories..."
$SourceDir = $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($SourceDir)) {
    $SourceDir = (Get-Location).Path
}

Write-Host "  Source Directory : $SourceDir"
Write-Host "  Target Directory : $DestinationPath"

$IsSourceSameAsDest = ($SourceDir.TrimEnd('\') -ieq $DestinationPath.TrimEnd('\'))

if (-not (Test-Path $DestinationPath)) {
    Write-Host "  Creating target directory: $DestinationPath..."
    New-Item -ItemType Directory -Path $DestinationPath -Force | Out-Null
}

# -------------------------------------------------------------
# STEP 2: Deploy Files (if running from an installer package)
# -------------------------------------------------------------
if (-not $IsSourceSameAsDest) {
    Write-Step "Step 2: Copying AIGRAV Files and Databases to $DestinationPath..."

    $FoldersToCopy = @("ConnectionLibrary", "KnowledgeBase", "MCP", "Revit", ".agents")
    foreach ($folder in $FoldersToCopy) {
        $src = Join-Path $SourceDir $folder
        $dst = Join-Path $DestinationPath $folder
        if (Test-Path $src) {
            Write-Host "  Copying $folder..."
            Copy-Item -Path $src -Destination $DestinationPath -Recurse -Force -ErrorAction SilentlyContinue
            Write-Success "$folder copied successfully."
        } else {
            Write-WarnMsg "Source folder '$folder' not found in $SourceDir."
        }
    }

    $FilesToCopy = @("AGENTS.md", "README.md", "Install-AIGRAV.ps1", "install.bat", "Verify-AIGRAV.ps1")
    foreach ($file in $FilesToCopy) {
        $src = Join-Path $SourceDir $file
        if (Test-Path $src) {
            Copy-Item -Path $src -Destination $DestinationPath -Force -ErrorAction SilentlyContinue
        }
    }
} else {
    Write-Step "Step 2: Running directly from $DestinationPath (Copy skipped)."
    Write-Success "Destination directory verified."
}

# -------------------------------------------------------------
# STEP 3: Verify Critical Databases & Precompiled Binaries
# -------------------------------------------------------------
Write-Step "Step 3: Verifying Databases and Precompiled Assemblies..."

$CriticalChecks = @(
    @{ Name = "Connection Database (JSON v1)"; Path = Join-Path $DestinationPath "KnowledgeBase\SteelConnectionDatabase\AIGRAV_SteelConnectionDatabase_v1.json" },
    @{ Name = "Connection Database (JSON)"; Path = Join-Path $DestinationPath "KnowledgeBase\SteelConnectionDatabase\AIGRAV.ConnectionDatabase.json" },
    @{ Name = "Steel Profile Catalog (CSV)"; Path = Join-Path $DestinationPath "Revit\AIGRAV.SteelDatabase.csv" },
    @{ Name = "BasePlate Family (RFA)"; Path = Join-Path $DestinationPath "ConnectionLibrary\AIGRAV_BasePlate_WF400.rfa" },
    @{ Name = "EndPlate Family (RFA)"; Path = Join-Path $DestinationPath "ConnectionLibrary\AIGRAV_EndPlate_WF400.rfa" },
    @{ Name = "Revit Bridge Core DLL"; Path = Join-Path $DestinationPath "Revit\AIGRAV.RevitBridge\AIGRAV.RevitBridge\bin\x64\Debug\net10.0-windows\AIGRAV.RevitBridge.dll" },
    @{ Name = "Revit Contracts DLL"; Path = Join-Path $DestinationPath "Revit\AIGRAV.RevitBridge\AIGRAV.RevitBridge\bin\x64\Debug\net10.0-windows\AIGRAV.Contracts.dll" },
    @{ Name = "BIM Audit Skill DLL"; Path = Join-Path $DestinationPath "Revit\AIGRAV.RevitBridge\AIGRAV.RevitBridge\bin\x64\Debug\net10.0-windows\Skills\AIGRAV.Skills.BimAudit.dll" },
    @{ Name = "QTO Skill DLL"; Path = Join-Path $DestinationPath "Revit\AIGRAV.RevitBridge\AIGRAV.RevitBridge\bin\x64\Debug\net10.0-windows\Skills\AIGRAV.Skills.QTO.dll" },
    @{ Name = "Steel Type Generator DLL"; Path = Join-Path $DestinationPath "Revit\AIGRAV.RevitBridge\AIGRAV.RevitBridge\bin\x64\Debug\net10.0-windows\Skills\AIGRAV.Skills.SteelTypeGenerator.dll" },
    @{ Name = "MCP Python Server Script"; Path = Join-Path $DestinationPath "MCP\TestServer\server.py" }
)

$AllDbOk = $true
foreach ($item in $CriticalChecks) {
    if (Test-Path $item.Path) {
        Write-Success "$($item.Name) verified."
    } else {
        Write-Fail "$($item.Name) MISSING at: $($item.Path)"
        $AllDbOk = $false
    }
}

if (-not $AllDbOk) {
    Write-WarnMsg "Some files are missing. Please verify the source distribution package."
}

# -------------------------------------------------------------
# STEP 4: Register Autodesk Revit Add-in Manifest (.addin)
# -------------------------------------------------------------
Write-Step "Step 4: Registering Revit Add-in Manifest..."

$BridgeDllPath = Join-Path $DestinationPath "Revit\AIGRAV.RevitBridge\AIGRAV.RevitBridge\bin\x64\Debug\net10.0-windows\AIGRAV.RevitBridge.dll"

$AddinContent = @"
<?xml version="1.0" encoding="utf-8" standalone="no"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>AIGRAV</Name>
    <Assembly>$BridgeDllPath</Assembly>
    <AddInId>14e28fa0-d3f8-45a4-8fe7-10dee7e30509</AddInId>
    <FullClassName>AIGRAV.RevitBridge.AIGRAVApp</FullClassName>
    <VendorId>AIGRAV</VendorId>
    <VendorDescription>AIGRAV AI Revit Integration</VendorDescription>
  </AddIn>
</RevitAddIns>
"@

# Detect installed Revit years
$DetectedYears = [System.Collections.Generic.HashSet[string]]::new()

$AutodeskProgramFiles = "C:\Program Files\Autodesk"
if (Test-Path $AutodeskProgramFiles) {
    Get-ChildItem -Path $AutodeskProgramFiles -Directory -Filter "Revit 20*" | ForEach-Object {
        if ($_.Name -match "Revit\s+(20\d\d)") {
            [void]$DetectedYears.Add($Matches[1])
        }
    }
}

# Also add target years
foreach ($yr in $TargetRevitYears) {
    [void]$DetectedYears.Add($yr)
}

$UserAddinsRoot = Join-Path $env:APPDATA "Autodesk\Revit\Addins"
$ProgramDataAddinsRoot = "C:\ProgramData\Autodesk\Revit\Addins"

foreach ($year in $DetectedYears) {
    # User-level addin
    $userYearDir = Join-Path $UserAddinsRoot $year
    if (-not (Test-Path $userYearDir)) {
        New-Item -ItemType Directory -Path $userYearDir -Force | Out-Null
    }
    $userAddinPath = Join-Path $userYearDir "AIGRAV.addin"
    Set-Content -Path $userAddinPath -Value $AddinContent -Encoding UTF8 -Force
    Write-Success "Registered Revit Add-in for Revit $($year): $userAddinPath"

    # Machine-level addin (if directory exists or can be created)
    try {
        $progDataYearDir = Join-Path $ProgramDataAddinsRoot $year
        if (-not (Test-Path $progDataYearDir)) {
            New-Item -ItemType Directory -Path $progDataYearDir -Force -ErrorAction SilentlyContinue | Out-Null
        }
        if (Test-Path $progDataYearDir) {
            $progDataAddinPath = Join-Path $progDataYearDir "AIGRAV.addin"
            Set-Content -Path $progDataAddinPath -Value $AddinContent -Encoding UTF8 -Force -ErrorAction SilentlyContinue
        }
    } catch {
        # Optional machine level write
    }
}

# -------------------------------------------------------------
# STEP 5: Python Virtual Environment & MCP Dependencies
# -------------------------------------------------------------
Write-Step "Step 5: Configuring Python Virtual Environment & MCP Packages..."

$VenvDir = Join-Path $DestinationPath "MCP\TestServer\venv"
$VenvPython = Join-Path $VenvDir "Scripts\python.exe"

if ($SkipPythonVenv) {
    Write-WarnMsg "SkipPythonVenv flag specified. Skipping Python venv setup."
} else {
    $PythonExe = $null

    # 1. Check if venv already exists and works
    if (Test-Path $VenvPython) {
        $PythonExe = $VenvPython
        Write-Host "  Existing venv found at: $VenvPython"
    } else {
        # 2. Look for system python
        $candidateCommands = @("python", "py")
        foreach ($cmd in $candidateCommands) {
            try {
                $ver = & $cmd --version 2>&1
                if ($LASTEXITCODE -eq 0 -or $ver -match "Python 3") {
                    $resolved = (Get-Command $cmd -ErrorAction SilentlyContinue).Source
                    if ($resolved) {
                        $PythonExe = $resolved
                        Write-Host "  System Python found: $PythonExe ($ver)"
                        break
                    }
                }
            } catch {}
        }

        # 3. Look in standard install locations
        if (-not $PythonExe) {
            $candidatePaths = @(
                "$env:LOCALAPPDATA\Programs\Python\Python313\python.exe",
                "$env:LOCALAPPDATA\Programs\Python\Python312\python.exe",
                "$env:LOCALAPPDATA\Programs\Python\Python311\python.exe",
                "$env:LOCALAPPDATA\Programs\Python\Python310\python.exe",
                "C:\Python313\python.exe",
                "C:\Python312\python.exe",
                "C:\Python311\python.exe",
                "C:\Python310\python.exe"
            )
            foreach ($p in $candidatePaths) {
                if (Test-Path $p) {
                    $PythonExe = $p
                    Write-Host "  Discovered Python at: $PythonExe"
                    break
                }
            }
        }

        # 4. If still not found, attempt automatic silent installation via winget
        if (-not $PythonExe) {
            Write-Host "  Python not detected on system. Attempting automatic background install..." -ForegroundColor Cyan
            if (Get-Command winget -ErrorAction SilentlyContinue) {
                Write-Host "  Installing Python 3.12 automatically via Windows Package Manager (winget)..." -ForegroundColor Yellow
                try {
                    Start-Process -FilePath "winget" -ArgumentList "install -e --id Python.Python.3.12 --silent --accept-package-agreements --accept-source-agreements" -Wait -NoNewWindow
                    $autoCandidatePaths = @(
                        "$env:LOCALAPPDATA\Programs\Python\Python312\python.exe",
                        "C:\Program Files\Python312\python.exe",
                        "C:\Python312\python.exe"
                    )
                    foreach ($p in $autoCandidatePaths) {
                        if (Test-Path $p) {
                            $PythonExe = $p
                            Write-Success "Python 3.12 auto-installed successfully: $PythonExe"
                            break
                        }
                    }
                } catch {
                    Write-WarnMsg "Automatic winget installation failed: $_"
                }
            }
        }

        # Create venv if Python found
        if ($PythonExe) {
            Write-Host "  Creating Python virtual environment in $VenvDir..."
            & $PythonExe -m venv $VenvDir
            if (Test-Path $VenvPython) {
                Write-Success "Virtual environment created successfully."
                $PythonExe = $VenvPython
            } else {
                Write-WarnMsg "Virtual environment creation may have failed. Will attempt to use: $PythonExe"
            }
        } else {
            Write-Fail "Python 3.10+ was not found on this computer!"
            Write-Host "  Please install Python 3.10+ from https://www.python.org/downloads/ and check 'Add python.exe to PATH'." -ForegroundColor Yellow
        }
    }

    # Install MCP and dependencies into venv
    if (Test-Path $VenvPython) {
        Write-Host "  Installing required Python MCP packages into venv..."
        $reqPath = Join-Path $DestinationPath "MCP\TestServer\requirements.txt"
        if (-not (Test-Path $reqPath)) {
            Set-Content -Path $reqPath -Value "mcp>=2.3.0`npydantic>=2.0.0`nopenpyxl>=3.1.0`npywin32>=306" -Force
        }

        & $VenvPython -m pip install --quiet --upgrade pip 2>&1 | Out-Null
        & $VenvPython -m pip install --quiet -r $reqPath

        # Test MCP import
        $testResult = & $VenvPython -c "import mcp; print('MCP_OK')" 2>&1
        if ($testResult -match "MCP_OK") {
            Write-Success "Python MCP runtime verified and working."
        } else {
            Write-WarnMsg "Python MCP test returned: $testResult"
        }
    }
}

# -------------------------------------------------------------
# STEP 6: Configure Antigravity MCP Server (mcp_config.json)
# -------------------------------------------------------------
Write-Step "Step 6: Configuring Antigravity MCP Settings..."

$GeminiConfigDir = Join-Path $env:USERPROFILE ".gemini\config"
if (-not (Test-Path $GeminiConfigDir)) {
    New-Item -ItemType Directory -Path $GeminiConfigDir -Force | Out-Null
}

$McpConfigFile = Join-Path $GeminiConfigDir "mcp_config.json"
$McpConfig = @{ mcpServers = @{} }

if (Test-Path $McpConfigFile) {
    try {
        $raw = Get-Content -Path $McpConfigFile -Raw -ErrorAction Stop
        if (-not [string]::IsNullOrWhiteSpace($raw)) {
            $parsed = $raw | ConvertFrom-Json
            if ($parsed.mcpServers) {
                # Convert PSObject to hashtable
                foreach ($prop in $parsed.mcpServers.PSObject.Properties) {
                    $McpConfig.mcpServers[$prop.Name] = $prop.Value
                }
            }
        }
    } catch {
        Write-WarnMsg "Existing mcp_config.json could not be fully parsed. Backing up..."
        Copy-Item -Path $McpConfigFile -Destination "$McpConfigFile.bak" -Force
    }
}

# Add or update aigrav-test configuration
$serverPy = Join-Path $DestinationPath "MCP\TestServer\server.py"
$serverCwd = Join-Path $DestinationPath "MCP\TestServer"
$venvPy = Join-Path $DestinationPath "MCP\TestServer\venv\Scripts\python.exe"

$McpConfig.mcpServers["aigrav-test"] = @{
    command = $venvPy
    args = @($serverPy)
    cwd = $serverCwd
}

$jsonOutput = $McpConfig | ConvertTo-Json -Depth 10
Set-Content -Path $McpConfigFile -Value $jsonOutput -Encoding UTF8 -Force
Write-Success "Updated Antigravity MCP configuration: $McpConfigFile"

# -------------------------------------------------------------
# STEP 7: Install / Update Antigravity Plugin (aigrav-bim)
# -------------------------------------------------------------
Write-Step "Step 7: Deploying Antigravity Plugin (aigrav-bim)..."

$PluginDir = Join-Path $GeminiConfigDir "plugins\aigrav-bim"
if (-not (Test-Path $PluginDir)) {
    New-Item -ItemType Directory -Path $PluginDir -Force | Out-Null
}

# 1. plugin.json
$PluginJson = @"
{
  "name": "aigrav-bim",
  "version": "1.1.0",
  "description": "AIGRAV BIM Engineering, Revit MCP Bridge & Structural QTO Automation Plugin",
  "author": "AIGRAV Engineering Team",
  "skills": [
    "skills/revit-qto-schedule-audit",
    "skills/install-aigrav"
  ]
}
"@
Set-Content -Path (Join-Path $PluginDir "plugin.json") -Value $PluginJson -Encoding UTF8 -Force

# 2. plugin mcp_config.json
$PluginMcp = @"
{
  "mcpServers": {
    "aigrav-test": {
      "command": "$($venvPy.Replace('\', '\\'))",
      "args": [
        "$($serverPy.Replace('\', '\\'))"
      ],
      "cwd": "$($serverCwd.Replace('\', '\\'))"
    }
  }
}
"@
Set-Content -Path (Join-Path $PluginDir "mcp_config.json") -Value $PluginMcp -Encoding UTF8 -Force

# 3. plugin rules/AGENTS.md
$PluginRulesDir = Join-Path $PluginDir "rules"
if (-not (Test-Path $PluginRulesDir)) { New-Item -ItemType Directory -Path $PluginRulesDir -Force | Out-Null }
$AgentsMdSource = Join-Path $DestinationPath "AGENTS.md"
if (Test-Path $AgentsMdSource) {
    Copy-Item -Path $AgentsMdSource -Destination (Join-Path $PluginRulesDir "AGENTS.md") -Force
}

# 4. plugin skills/revit-qto-schedule-audit
$PluginSkillQtoDir = Join-Path $PluginDir "skills\revit-qto-schedule-audit"
$SrcSkillQtoDir = Join-Path $DestinationPath ".agents\skills\revit-qto-schedule-audit"
if (Test-Path $SrcSkillQtoDir) {
    Copy-Item -Path $SrcSkillQtoDir -Destination (Join-Path $PluginDir "skills") -Recurse -Force
}

# 5. plugin skills/install-aigrav
$PluginSkillInstallDir = Join-Path $PluginDir "skills\install-aigrav"
$SrcSkillInstallDir = Join-Path $DestinationPath ".agents\skills\install-aigrav"
if (Test-Path $SrcSkillInstallDir) {
    Copy-Item -Path $SrcSkillInstallDir -Destination (Join-Path $PluginDir "skills") -Recurse -Force
}

Write-Success "Antigravity plugin 'aigrav-bim' installed at: $PluginDir"

# -------------------------------------------------------------
# STEP 8: Installation Summary & Next Steps
# -------------------------------------------------------------
Write-Host "`n=================================================================" -ForegroundColor Green
Write-Host "             AIGRAV INSTALLATION COMPLETED SUCCESSFULLY!         " -ForegroundColor Green
Write-Host "=================================================================" -ForegroundColor Green

Write-Host "`nInstallation Summary:" -ForegroundColor White
Write-Host "  1. Suite Location      : $DestinationPath" -ForegroundColor Gray
Write-Host "  2. Revit Add-in Manifest: Deployed for Revit $($DetectedYears -join ', ')" -ForegroundColor Gray
Write-Host "  3. Revit Bridge DLL    : $BridgeDllPath" -ForegroundColor Gray
Write-Host "  4. Python MCP Runtime  : $VenvPython" -ForegroundColor Gray
Write-Host "  5. Antigravity MCP Conf: $McpConfigFile" -ForegroundColor Gray
Write-Host "  6. Antigravity Plugin  : $PluginDir" -ForegroundColor Gray
Write-Host "  7. Steel & Conn DB     : Verified ($($CriticalChecks.Count) components)" -ForegroundColor Gray

Write-Host "`nHow to use AIGRAV right now:" -ForegroundColor Cyan
Write-Host "  1. Start Autodesk Revit (AIGRAV Ribbon and Bridge will load automatically)." -ForegroundColor White
Write-Host "  2. In Antigravity on this PC, start chatting! You can prompt:" -ForegroundColor White
Write-Host "     - 'Ping Revit' or 'Cek status model Revit'" -ForegroundColor Yellow
Write-Host "     - 'Buat sambungan base plate WF 400'" -ForegroundColor Yellow
Write-Host "     - 'Jalankan QTO schedule audit'" -ForegroundColor Yellow
Write-Host "=================================================================`n" -ForegroundColor Green

if (-not $Silent) {
    Write-Host "Press Enter to exit installer..." -ForegroundColor Gray
    [void][System.Console]::ReadLine()
}
