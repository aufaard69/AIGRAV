<#
.SYNOPSIS
    Compiles and deploys AIGRAV.RevitBridge + AIGRAV.Skills.RebarExtension + runs deterministic unit tests.
    Ensure Autodesk Revit 2026 is closed before running so host DLLs are unlocked.
#>
$ErrorActionPreference = "Stop"

$revit = Get-Process Revit -ErrorAction SilentlyContinue
if ($revit) {
    Write-Warning "Autodesk Revit is currently running (PIDs: $($revit.Id -join ', ')). Please close Revit first so AIGRAV.RevitBridge.dll can be updated."
    exit 1
}

$dotnet = Join-Path $env:LOCALAPPDATA "Microsoft\dotnet\dotnet.exe"
if (-not (Test-Path $dotnet)) { $dotnet = "dotnet" }

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Remove-Item (Join-Path $root "AIGRAV.RevitBridge\bin\x64\Debug\net10.0-windows\Skills\AIGRAV.Skills.RebarExtension.*") -Force -ErrorAction SilentlyContinue
Write-Host "Building AIGRAV.RevitBridge (x64 Debug)..." -ForegroundColor Cyan
& $dotnet build (Join-Path $root "AIGRAV.RevitBridge\AIGRAV.RevitBridge.csproj") -c Debug -p:Platform=x64 -v:minimal -p:NoWarn=MSB3277
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Running AIGRAV.Skills.RebarExtension.Tests..." -ForegroundColor Cyan
& $dotnet run --project (Join-Path $root "AIGRAV.Skills.RebarExtension.Tests\AIGRAV.Skills.RebarExtension.Tests.csproj") -c Debug -p:Platform=x64 -v:minimal -p:NoWarn=MSB3277
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`nDeploy selesai! Buka Revit 2026 -> Tab AIGRAV -> Panel 'Rebar Suite'." -ForegroundColor Green
