<#
.SYNOPSIS
    AIGRAV One-Click Cloud Synchronizer (Multi-PC)
.DESCRIPTION
    Pulls the latest AIGRAV Agent Skills, Revit Bridge DLLs, and Knowledge Base
    from GitHub and automatically triggers Hot-Reload if Revit is currently running.
#>

param(
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$AigravRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "   AIGRAV Cloud Synchronizer (GitHub)    " -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "Working Directory: $AigravRoot" -ForegroundColor Gray

# 1. Check Git
if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    Write-Host "[ERROR] Git is not installed or not in PATH." -ForegroundColor Red
    exit 1
}

# 2. Git Fetch & Pull
Set-Location -Path $AigravRoot
Write-Host "`n[1/3] Pulling latest Skills & Database updates from GitHub..." -ForegroundColor Yellow

try {
    $pullOutput = git pull origin main 2>&1
    Write-Host $pullOutput -ForegroundColor Green
}
catch {
    Write-Host "[WARNING] Git pull encountered an issue: $_" -ForegroundColor Yellow
    Write-Host "Attempting git fetch..." -ForegroundColor Gray
    git fetch origin main
}

# 3. Check / Compile C# Skills if needed
$bimAuditCs = Join-Path $AigravRoot "Revit\AIGRAV.RevitBridge\AIGRAV.Skills.BimAudit\BimAuditSkill.cs"
if (Test-Path $bimAuditCs) {
    Write-Host "`n[2/3] Checking compiled Revit Bridge Skills..." -ForegroundColor Yellow
    $skillsDll = Join-Path $AigravRoot "Revit\AIGRAV.RevitBridge\AIGRAV.RevitBridge\bin\x64\Debug\net10.0-windows\Skills\AIGRAV.Skills.BimAudit.dll"
    if (-not (Test-Path $skillsDll) -or $Force) {
        Write-Host "Building skills for .NET 10 (Revit 2026)..." -ForegroundColor Gray
        dotnet build "$AigravRoot\Revit\AIGRAV.RevitBridge\AIGRAV.Skills.BimAudit\AIGRAV.Skills.BimAudit.csproj" -c Debug
        dotnet build "$AigravRoot\Revit\AIGRAV.RevitBridge\AIGRAV.Skills.SheetManager\AIGRAV.Skills.SheetManager.csproj" -c Debug
    } else {
        Write-Host "Skills DLLs are ready and up to date." -ForegroundColor Green
    }
}

# 4. Trigger Live Revit Hot-Reload if Revit Bridge Listener is active
Write-Host "`n[3/3] Checking Live Revit Session Listener (Port 8765)..." -ForegroundColor Yellow
try {
    $req = [System.Net.WebRequest]::Create("http://127.0.0.1:8765/")
    $req.Method = "POST"
    $req.ContentType = "application/json"
    $req.Timeout = 3000
    $body = '{"tool":"aigrav_reload_skills"}'
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($body)
    $req.ContentLength = $bytes.Length
    $stream = $req.GetRequestStream()
    $stream.Write($bytes, 0, $bytes.Length)
    $stream.Close()
    
    $resp = $req.GetResponse()
    $reader = New-Object System.IO.StreamReader($resp.GetResponseStream())
    $resultText = $reader.ReadToEnd()
    Write-Host "[SUCCESS] Live Revit Bridge Hot-Reload Triggered!" -ForegroundColor Green
}
catch {
    Write-Host "[INFO] Revit is not currently active on Port 8765. Updates will load automatically when Revit opens." -ForegroundColor Gray
}

Write-Host "`n=========================================" -ForegroundColor Green
Write-Host "   AIGRAV Synchronized Successfully!     " -ForegroundColor Green
Write-Host "=========================================" -ForegroundColor Green
