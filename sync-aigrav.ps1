<#
.SYNOPSIS
    AIGRAV One-Click Cloud Synchronizer & Version Inspector (Multi-PC)
.DESCRIPTION
    Checks for AIGRAV updates on GitHub, displays changelog highlights and fixes,
    pulls updates, waits for Revit to close if needed, builds AIGRAV.RevitBridge solution,
    and triggers hot-reload across active Revit Bridge ports (8765-8775).
#>

param(
    [switch]$CheckOnly,
    [switch]$Force,
    [switch]$WaitForRevitClose,
    [switch]$SkipPull
)

$ErrorActionPreference = "Stop"
$AigravRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$SelfScriptPath = $MyInvocation.MyCommand.Path

function Get-FileSha256OrEmpty {
    param([string]$Path)
    if (Test-Path $Path) {
        return (Get-FileHash -Path $Path -Algorithm SHA256).Hash
    }
    return ""
}

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

Set-Location -Path $AigravRoot

if (-not $SkipPull) {
    # 3. Fetch Remote Updates
    Write-Host "`n[1/4] Memeriksa pembaruan di GitHub..." -ForegroundColor Yellow

    git fetch origin main 2>$null | Out-Null

    # Compare remote vs local commit
    $localCommit = git rev-parse HEAD
    $remoteCommit = git rev-parse origin/main

    if ($localCommit -eq $remoteCommit) {
        Write-Host "[OK] Kode lokal sudah berada pada commit terbaru GitHub (v$localVersion)." -ForegroundColor Green
        if ($CheckOnly) { exit 0 }
    } else {
        try {
            $remoteJsonRaw = git show origin/main:version.json 2>$null
            if ($remoteJsonRaw) {
                $remoteJson = $remoteJsonRaw | ConvertFrom-Json
                $remoteVersion = $remoteJson.version

                Write-Host "`n=========================================" -ForegroundColor Magenta
                Write-Host " PEMBARUAN AIGRAV TERSEDIA: v$remoteVersion" -ForegroundColor Yellow
                Write-Host " Rilis: $($remoteJson.release_name) ($($remoteJson.release_date))" -ForegroundColor Gray
                Write-Host "=========================================" -ForegroundColor Magenta

                if ($remoteJson.highlights) {
                    Write-Host "`nFitur Baru & Peningkatan:" -ForegroundColor Cyan
                    foreach ($h in $remoteJson.highlights) {
                        Write-Host "  - $h" -ForegroundColor White
                    }
                }
                if ($remoteJson.fixes) {
                    Write-Host "`nPerbaikan Bug:" -ForegroundColor Cyan
                    foreach ($f in $remoteJson.fixes) {
                        Write-Host "  - $f" -ForegroundColor Gray
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
    $prePullHash = Get-FileSha256OrEmpty -Path $SelfScriptPath
    $pullOutput = git pull origin main 2>&1 | Out-String
    $pullExit = $LASTEXITCODE
    if ($pullOutput.Trim()) {
        Write-Host $pullOutput.Trim() -ForegroundColor $(if ($pullExit -eq 0) { "Green" } else { "Red" })
    }
    if ($pullExit -ne 0) {
        Write-Host "`n[ERROR] Git pull gagal (ExitCode: $pullExit). Selesaikan perubahan lokal / merge conflict terlebih dahulu sebelum melanjutkan build." -ForegroundColor Red
        exit $pullExit
    }

    $postPullHash = Get-FileSha256OrEmpty -Path $SelfScriptPath
    if ($prePullHash -ne $postPullHash) {
        Write-Host "`n[INFO] sync-aigrav.ps1 diperbarui dari GitHub. Melanjutkan dengan versi skrip terbaru..." -ForegroundColor Cyan
        $forwardArgs = @("-ExecutionPolicy", "Bypass", "-File", $SelfScriptPath, "-SkipPull", "-Force")
        if ($WaitForRevitClose) { $forwardArgs += "-WaitForRevitClose" }
        & powershell.exe @forwardArgs
        exit $LASTEXITCODE
    }
}

# Refresh local version after pull
if (Test-Path $localVersionFile) {
    try {
        $localJson = Get-Content $localVersionFile -Raw | ConvertFrom-Json
        $localVersion = $localJson.version
    } catch {}
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

if ($runningRevit) {
    $pids = ($runningRevit.Id -join ', ')
    if ($WaitForRevitClose) {
        Write-Host "[ACTION REQUIRED] Autodesk Revit masih terbuka (PID: $pids)." -ForegroundColor Yellow
        Write-Host "Tutup semua jendela Autodesk Revit sekarang agar AIGRAV.RevitBridge.dll (UI & Bridge) dapat dikompilasi ulang..." -ForegroundColor Cyan
        while ($runningRevit) {
            Start-Sleep -Seconds 2
            $runningRevit = Get-Process Revit -ErrorAction SilentlyContinue
        }
        Write-Host "[OK] Revit sudah ditutup. Melanjutkan proses kompilasi..." -ForegroundColor Green
    } elseif (-not $Force) {
        $waitAns = Read-Host "Autodesk Revit sedang terbuka (PID: $pids). Tutup Revit lalu tekan [Y] untuk menunggu & build penuh, atau [N] untuk lewati build"
        if ($waitAns -match "^[Yy]") {
            while ($runningRevit) {
                Write-Host "Menunggu Autodesk Revit ditutup..." -ForegroundColor Gray
                Start-Sleep -Seconds 2
                $runningRevit = Get-Process Revit -ErrorAction SilentlyContinue
            }
        }
    }
}

$buildSucceeded = $false
if (-not $dotnetCmd) {
    Write-Host "[ERROR] .NET SDK tidak ditemukan di PATH atau %LOCALAPPDATA%\Microsoft\dotnet. Instal .NET 10 SDK agar binary Revit Bridge dapat dikompilasi." -ForegroundColor Red
    exit 1
} elseif ($runningRevit) {
    Write-Host "[WARNING] Autodesk Revit masih terbuka (PID: $($runningRevit.Id -join ', ')). AIGRAV.RevitBridge.dll terkunci dan TIDAK dapat diperbarui sampai Revit ditutup!" -ForegroundColor Red
    Write-Host "Tutup Revit lalu jalankan kembali '.\sync-aigrav.ps1' agar pembaruan UI & Bridge aktif." -ForegroundColor Yellow
    exit 2
} elseif (Test-Path $slnxPath) {
    # Prevent duplicate RebarExtension shadowing in Skills folder
    $skillsRebarPattern = Join-Path $AigravRoot "Revit\AIGRAV.RevitBridge\AIGRAV.RevitBridge\bin\x64\Debug\net10.0-windows\Skills\AIGRAV.Skills.RebarExtension.*"
    Remove-Item $skillsRebarPattern -Force -ErrorAction SilentlyContinue

    Write-Host "Building $slnxPath (Debug|x64)..." -ForegroundColor Gray
    & $dotnetCmd build $slnxPath -c Debug -p:Platform=x64 -p:NoWarn=MSB3277 -v:minimal
    if ($LASTEXITCODE -eq 0) {
        $buildSucceeded = $true
        Write-Host "[OK] Seluruh proyek AIGRAV.RevitBridge berhasil dikompilasi (v$localVersion)." -ForegroundColor Green
    } else {
        Write-Host "[ERROR] Build gagal (ExitCode: $LASTEXITCODE)." -ForegroundColor Red
        exit $LASTEXITCODE
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
    Write-Host "[INFO] Tidak ada sesi Revit aktif. Buka Autodesk Revit untuk memakai AIGRAV v$localVersion." -ForegroundColor Gray
}

if ($buildSucceeded) {
    Write-Host "`n=========================================" -ForegroundColor Green
    Write-Host "   AIGRAV v$localVersion Berhasil Disinkronisasi!  " -ForegroundColor Green
    Write-Host "=========================================" -ForegroundColor Green
}
