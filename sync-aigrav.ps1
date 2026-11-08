<#
.SYNOPSIS
    AIGRAV One-Click Cloud Synchronizer & Version Inspector (Multi-PC)
.DESCRIPTION
    Checks for AIGRAV updates on GitHub, displays changelog highlights and fixes,
    and asks user confirmation before applying updates (or auto-applies with -Force).
#>

param(
    [switch]$CheckOnly,
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

# 2. Read Local version.json
$localVersionFile = Join-Path $AigravRoot "version.json"
$localVersion = "0.0.0"
if (Test-Path $localVersionFile) {
    try {
        $localJson = Get-Content $localVersionFile -Raw | ConvertFrom-Json
        $localVersion = $localJson.version
    } catch {}
}
Write-Host "Versi Terpasang Saat Ini: v$localVersion" -ForegroundColor Gray

# 3. Fetch Remote Updates
Set-Location -Path $AigravRoot
Write-Host "`n[1/4] Memeriksa pembaruan di GitHub..." -ForegroundColor Yellow

try {
    git fetch origin main 2>$null | Out-Null
} catch {
}

# Compare remote vs local commit
$localCommit = git rev-parse HEAD
$remoteCommit = git rev-parse origin/main

if ($localCommit -eq $remoteCommit) {
    Write-Host "[OK] AIGRAV sudah berada pada versi terbaru (v$localVersion)!" -ForegroundColor Green
    if ($CheckOnly) { exit 0 }
} else {
    # Read remote version.json
    try {
        $remoteJsonRaw = git show origin/main:version.json 2>$null
        if ($remoteJsonRaw) {
            $remoteJson = $remoteJsonRaw | ConvertFrom-Json
            $remoteVersion = $remoteJson.version
            
            Write-Host "`n=========================================" -ForegroundColor Magenta
            Write-Host " 🔔 PEMBARUAN AIGRAV TERSEDIA: v$remoteVersion" -ForegroundColor Yellow
            Write-Host " Rilis: $($remoteJson.release_name) ($($remoteJson.release_date))" -ForegroundColor Gray
            Write-Host "=========================================" -ForegroundColor Magenta
            
            if ($remoteJson.highlights) {
                Write-Host "`n✨ Fitur Baru & Peningkatan:" -ForegroundColor Cyan
                foreach ($h in $remoteJson.highlights) {
                    Write-Host "  • $h" -ForegroundColor White
                }
            }
            if ($remoteJson.fixes) {
                Write-Host "`n🛠️ Perbaikan Bug:" -ForegroundColor Cyan
                foreach ($f in $remoteJson.fixes) {
                    Write-Host "  • $f" -ForegroundColor Gray
                }
            }
            Write-Host "=========================================`n" -ForegroundColor Magenta
        }
    } catch {}

    if ($CheckOnly) {
        Write-Host "Jalankan '.\sync-aigrav.ps1' untuk memasang pembaruan ini." -ForegroundColor Yellow
        exit 0
    }

    # Ask user confirmation if not forced
    if (-not $Force) {
        $answer = Read-Host "Apakah Anda ingin memperbarui AIGRAV sekarang? [Y/N]"
        if ($answer -notmatch "^[Yy]") {
            Write-Host "`nPembaruan dibatalkan oleh pengguna. Tetap menggunakan v$localVersion." -ForegroundColor Yellow
            exit 0
        }
    }
}

# 4. Apply Git Pull
Write-Host "`n[2/4] Mengunduh pembaruan dari GitHub..." -ForegroundColor Yellow
try {
    $pullOutput = git pull origin main 2>&1 | Out-String
    Write-Host $pullOutput.Trim() -ForegroundColor Green
}
catch {
    Write-Host "[WARNING] Git merge encountered an issue: $_" -ForegroundColor Yellow
}

# 5. Check / Compile C# Skills if needed
$bimAuditCs = Join-Path $AigravRoot "Revit\AIGRAV.RevitBridge\AIGRAV.Skills.BimAudit\BimAuditSkill.cs"
if (Test-Path $bimAuditCs) {
    Write-Host "`n[3/4] Memeriksa kompilasi Revit Bridge Skills..." -ForegroundColor Yellow
    $skillsDll = Join-Path $AigravRoot "Revit\AIGRAV.RevitBridge\AIGRAV.RevitBridge\bin\x64\Debug\net10.0-windows\Skills\AIGRAV.Skills.BimAudit.dll"
    if (-not (Test-Path $skillsDll) -or $Force) {
        Write-Host "Building skills for .NET 10 (Revit 2026)..." -ForegroundColor Gray
        dotnet build "$AigravRoot\Revit\AIGRAV.RevitBridge\AIGRAV.Skills.BimAudit\AIGRAV.Skills.BimAudit.csproj" -c Debug
        dotnet build "$AigravRoot\Revit\AIGRAV.RevitBridge\AIGRAV.Skills.SheetManager\AIGRAV.Skills.SheetManager.csproj" -c Debug
    } else {
        Write-Host "Skills DLLs ready and up to date." -ForegroundColor Green
    }
}

# 6. Trigger Live Revit Hot-Reload if Revit Bridge Listener is active
Write-Host "`n[4/4] Memeriksa sesi Revit aktif (Port 8765)..." -ForegroundColor Yellow
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
    Write-Host "[INFO] Revit tidak sedang aktif di Port 8765. Pembaruan akan otomatis aktif saat Revit dibuka." -ForegroundColor Gray
}

Write-Host "`n=========================================" -ForegroundColor Green
Write-Host "   AIGRAV Berhasil Disinkronisasi!       " -ForegroundColor Green
Write-Host "=========================================" -ForegroundColor Green
