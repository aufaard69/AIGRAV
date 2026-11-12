<#
.SYNOPSIS
    AIGRAV Distribution Packager
    Bundles the complete AIGRAV suite with all databases, connection libraries,
    precompiled Revit Bridge DLLs, MCP Server, and installer scripts into a
    distributable folder and ZIP archive ready for transfer to another PC.
#>

[CmdletBinding()]
param (
    [string]$SourcePath = "C:\AIGRAV",
    [string]$StagingPath = "C:\AIGRAV_Installer_Package",
    [string]$OutputZip = "C:\AIGRAV\AIGRAV_Installer_Ready.zip"
)

Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host "                 AIGRAV DISTRIBUTION PACKAGER                   " -ForegroundColor White
Write-Host "=================================================================" -ForegroundColor Cyan

# 1. Clean staging directory
if (Test-Path $StagingPath) {
    Write-Host "Cleaning existing staging directory: $StagingPath..."
    Remove-Item -Path $StagingPath -Recurse -Force
}
New-Item -ItemType Directory -Path $StagingPath -Force | Out-Null

# 2. Copy root installer files
Write-Host "Copying installer root files..." -ForegroundColor Cyan
$RootFiles = @("install.bat", "Install-AIGRAV.ps1", "Verify-AIGRAV.ps1", "AGENTS.md", "README.md")
foreach ($f in $RootFiles) {
    $src = Join-Path $SourcePath $f
    if (Test-Path $src) {
        Copy-Item -Path $src -Destination $StagingPath -Force
    }
}

# 3. Copy ConnectionLibrary (.rfa families)
Write-Host "Copying ConnectionLibrary (.rfa Revit families)..." -ForegroundColor Cyan
Copy-Item -Path (Join-Path $SourcePath "ConnectionLibrary") -Destination $StagingPath -Recurse -Force

# 4. Copy KnowledgeBase (all databases)
Write-Host "Copying KnowledgeBase and Steel Connection databases..." -ForegroundColor Cyan
$kbDest = Join-Path $StagingPath "KnowledgeBase"
New-Item -ItemType Directory -Path $kbDest -Force | Out-Null

# Copy SteelConnectionDatabase
Copy-Item -Path (Join-Path $SourcePath "KnowledgeBase\SteelConnectionDatabase") -Destination $kbDest -Recurse -Force

# Copy standard markdown guides
Get-ChildItem -Path (Join-Path $SourcePath "KnowledgeBase") -File -Filter "*.md" | ForEach-Object {
    Copy-Item -Path $_.FullName -Destination $kbDest -Force
}

# 5. Copy MCP Server (without venv and cache)
Write-Host "Copying MCP TestServer..." -ForegroundColor Cyan
$mcpDest = Join-Path $StagingPath "MCP\TestServer"
New-Item -ItemType Directory -Path $mcpDest -Force | Out-Null
Copy-Item -Path (Join-Path $SourcePath "MCP\TestServer\server.py") -Destination $mcpDest -Force
Copy-Item -Path (Join-Path $SourcePath "MCP\TestServer\requirements.txt") -Destination $mcpDest -Force
if (Test-Path (Join-Path $SourcePath "MCP\TestServer\orchestrator")) {
    Copy-Item -Path (Join-Path $SourcePath "MCP\TestServer\orchestrator") -Destination $mcpDest -Recurse -Force
    # Clean pycache inside orchestrator
    Get-ChildItem -Path (Join-Path $mcpDest "orchestrator") -Recurse -Filter "__pycache__" | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
}

# 6. Copy Revit Bridge, Binaries, and Steel Database CSV
Write-Host "Copying Revit Bridge and precompiled binaries..." -ForegroundColor Cyan
$revitDest = Join-Path $StagingPath "Revit"
New-Item -ItemType Directory -Path $revitDest -Force | Out-Null
Copy-Item -Path (Join-Path $SourcePath "Revit\AIGRAV.SteelDatabase.csv") -Destination $revitDest -Force

# Copy RevitBridge project and precompiled output
$bridgeSrc = Join-Path $SourcePath "Revit\AIGRAV.RevitBridge"
if (Test-Path $bridgeSrc) {
    Copy-Item -Path $bridgeSrc -Destination $revitDest -Recurse -Force
    # Clean temporary obj folders
    Get-ChildItem -Path (Join-Path $revitDest "AIGRAV.RevitBridge") -Recurse -Directory -Filter "obj" | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
}

# 7. Copy .agents skills
Write-Host "Copying Antigravity skills (.agents)..." -ForegroundColor Cyan
Copy-Item -Path (Join-Path $SourcePath ".agents") -Destination $StagingPath -Recurse -Force

# 8. Create README_INSTALL.txt for user on the new PC
$readmePath = Join-Path $StagingPath "README_INSTALL.txt"
$readmeText = @"
========================================================================
                 AIGRAV INSTALLER PACKAGE FOR OTHER PC
========================================================================

SELAMAT! Paket instalasi AIGRAV ini sudah berisi:
 1. Database Baja & Profil (AIGRAV.SteelDatabase.csv, 230 profil WF & H-Beam)
 2. Database Sambungan Resmi SNI/STD (AIGRAV_SteelConnectionDatabase_v1.json)
 3. Library Family Revit (.rfa Base Plate WF 400, End Plate WF 400)
 4. Binary Revit Bridge Precompiled (.NET 10 / Revit 2024, 2025, 2026, 2027)
 5. Server MCP Python (50+ Revit Automation Tools)
 6. Plugin Antigravity (aigrav-bim, skill QTO Schedule Audit, install-aigrav)
 7. Standard Operating Protocol (AGENTS.md)

------------------------------------------------------------------------
CARA 1: INSTALASI MELALUI ANTIGRAVITY (PALING MUDAH)
------------------------------------------------------------------------
1. Copy folder installer ini ke PC baru (direkomendasikan letakkan di C:\AIGRAV).
2. Buka aplikasi Antigravity di PC tersebut.
3. Di chat prompt Antigravity, cukup ketik:
   "install aigrav"  atau  "instalkan aigrav sekarang"
4. Antigravity akan otomatis mendeteksi script installer, menginstal add-in Revit,
   menyiapkan runtime Python MCP, mengonfigurasi mcp_config.json, dan melaporkan
   status instalasi sampai selesai!

------------------------------------------------------------------------
CARA 2: DOUBLE-CLICK INSTALLER WINDOWS (TANPA PROMPT)
------------------------------------------------------------------------
1. Klik kanan pada file "install.bat"
2. Pilih "Run as administrator" (atau double click biasa)
3. Tunggu hingga proses instalasi menampilkan:
   "AIGRAV INSTALLATION COMPLETED SUCCESSFULLY!"
4. Buka Autodesk Revit -> Plugin AIGRAV langsung aktif di Ribbon.
5. Buka Antigravity -> Siap digunakan!

------------------------------------------------------------------------
VERIFIKASI SETELAH INSTALL:
------------------------------------------------------------------------
Jalankan di PowerShell:
  powershell -ExecutionPolicy Bypass -File C:\AIGRAV\Verify-AIGRAV.ps1

========================================================================
"@
Set-Content -Path $readmePath -Value $readmeText -Encoding UTF8 -Force

# 9. Create ZIP archive
Write-Host "Compressing package into ZIP: $OutputZip..." -ForegroundColor Cyan
if (Test-Path $OutputZip) {
    Remove-Item -Path $OutputZip -Force
}
Compress-Archive -Path "$StagingPath\*" -DestinationPath $OutputZip -CompressionLevel Optimal

$zipItem = Get-Item $OutputZip
$zipSizeMb = [math]::Round($zipItem.Length / 1MB, 2)

Write-Host "`n=================================================================" -ForegroundColor Green
Write-Host "             PACKAGE CREATION COMPLETED SUCCESSFULLY!            " -ForegroundColor Green
Write-Host "=================================================================" -ForegroundColor Green
Write-Host "  Staging Folder : $StagingPath" -ForegroundColor White
Write-Host "  ZIP Package    : $OutputZip ($zipSizeMb MB)" -ForegroundColor White
Write-Host "`nAnda tinggal meng-copy file ZIP di atas ke PC lain dan mengekstraknya!" -ForegroundColor Cyan
Write-Host "=================================================================`n" -ForegroundColor Green
