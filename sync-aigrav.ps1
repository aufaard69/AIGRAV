<#
.SYNOPSIS
    AIGRAV One-Click Cloud Synchronizer & Version Inspector (Multi-PC)
.DESCRIPTION
    Checks for AIGRAV updates on GitHub, displays changelog highlights and fixes,
    pulls updates, builds AIGRAV.RevitBridge solution, and triggers hot-reload
    across active Revit Bridge ports (8765-8775).
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
} catch {}

# Compare remote vs local commit
$localCommit = git rev-parse HEAD
$remoteCommit = git rev-parse origin/main

if ($localCommit -eq $remoteCommit) {
    Write-Host "[OK] AIGRAV sudah berada pada versi terbaru (v$localVersion)!" -ForegroundColor Green
    if ($CheckOnly) { exit 0 }
} else {
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

# 5. Build Revit Bridge Solution & Skills (.NET 10 / x64)
Write-Host "`n[3/4] Memeriksa kompilasi Revit Bridge & Skills..." -ForegroundColor Yellow
$dotnetCmd = $null
$dotnetInfo = Get-Command dotnet -ErrorAction SilentlyContinue
if ($dotnetInfo) {
    $dotnetCmd = $dotnetInfo.Source
}
if (-not $dotnetCmd) {
    $localDotnet = Join-Path $env:LOCALAPPDATA "Microsoft\dotnet\dotnet.exe"
    if (Test-Path $localDotnet) { $dotnetCmd = $localDotnet }
}

$slnxPath = Join-Path $AigravRoot "Revit\AIGRAV.RevitBridge\AIGRAV.RevitBridge.slnx"
$runningRevit = Get-Process Revit -ErrorAction SilentlyContinue

if (-not $dotnetCmd) {
    Write-Host "[INFO] .NET SDK tidak ditemukan di PATH atau %LOCALAPPDATA%\Microsoft\dotnet." -ForegroundColor Gray
} elseif ($runningRevit) {
    Write-Host "[INFO] Autodesk Revit sedang terbuka (PID: $($runningRevit.Id -join ', ')). Tutup Revit untuk rebuild AIGRAV.RevitBridge.dll, atau build Skills saja." -ForegroundColor Yellow
} elseif (Test-Path $slnxPath) {
    Write-Host "Building $slnxPath (Debug|x64)..." -ForegroundColor Gray
    & $dotnetCmd build $slnxPath -c Debug -p:Platform=x64 -p:NoWarn=MSB3277 -v:minimal
    if ($LASTEXITCODE -eq 0) {
        Write-Host "[OK] Seluruh proyek AIGRAV.RevitBridge berhasil dikompilasi." -ForegroundColor Green
    } else {
        Write-Host "[WARNING] Build selesai dengan peringatan/error (ExitCode: $LASTEXITCODE)." -ForegroundColor Yellow
    }
}

# 6. Trigger Live Revit Hot-Reload across active multi-port sessions (8765-8775)
Write-Host "`n[4/4] Memeriksa sesi Revit aktif (Port 8765-8775)..." -ForegroundColor Yellow
$reloaded = 0
for ($port = 8765; $port -le 8775; $port++) {
    try {
        $req = [System.Net.WebRequest]::Create("http://127.0.0.1:$port/")
        $req.Method = "POST"
        $req.ContentType = "application/json"
        $req.Timeout = 1500
        $bytes = [System.Text.Encoding]::UTF8.GetBytes('{"tool":"aigrav_reload_skills"}')
        $req.ContentLength = $bytes.Length
        $stream = $req.GetRequestStream()
        $stream.Write($bytes, 0, $bytes.Length)
        $stream.Close()

        $resp = $req.GetResponse()
        $resp.Close()
        $reloaded++
        Write-Host "  [SUCCESS] Live Revit Skill Hot-Reload pada Port $port." -ForegroundColor Green
    } catch {}
}
if ($reloaded -eq 0) {
    Write-Host "[INFO] Tidak ada sesi Revit aktif yang merespons. Pembaruan akan aktif saat Revit dibuka." -ForegroundColor Gray
}

Write-Host "`n=========================================" -ForegroundColor Green
Write-Host "   AIGRAV Berhasil Disinkronisasi!       " -ForegroundColor Green
Write-Host "=========================================" -ForegroundColor Green