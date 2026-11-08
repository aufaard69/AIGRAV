# AIGRAV Desktop Floating HUD Launcher
# Checks backend status, starts backend if needed, and launches Electron frameless window.

$ErrorActionPreference = "Continue"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   AIGRAV AUTONOMOUS BIM - FRAMELESS FLOATING HUD" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Ensure Node.js is in PATH and Load Cached Antigravity Environment
$nodeDir = "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Microsoft\VisualStudio\NodeJs"
if (Test-Path $nodeDir) {
    $env:PATH = "$nodeDir;$env:PATH"
}

$rtEnvFile = "C:\AIGRAV\KnowledgeBase\antigravity_runtime_env.json"
if (Test-Path $rtEnvFile) {
    try {
        $rtJson = Get-Content $rtEnvFile -Raw | ConvertFrom-Json
        if ($rtJson.ANTIGRAVITY_LS_ADDRESS) { $env:ANTIGRAVITY_LS_ADDRESS = $rtJson.ANTIGRAVITY_LS_ADDRESS }
        if ($rtJson.ANTIGRAVITY_CSRF_TOKEN) { $env:ANTIGRAVITY_CSRF_TOKEN = $rtJson.ANTIGRAVITY_CSRF_TOKEN }
    } catch {}
}

# 2. Check Backend Health
Write-Host "[1/2] Memeriksa status AIGRAV Backend (Port 8080)..." -ForegroundColor Yellow
$backendRunning = $false
try {
    $resp = Invoke-RestMethod -Uri "http://127.0.0.1:8080/api/status" -TimeoutSec 4 -ErrorAction Stop
    if ($resp.antigravity_session_id -and $resp.antigravity_session_id -ne "session_initializing") {
        $backendRunning = $true
        Write-Host "  -> Backend sudah aktif (Session: $($resp.antigravity_session_id.Substring(0,8))...)" -ForegroundColor Green
    }
} catch {
    $backendRunning = $false
}

if (-not $backendRunning) {
    Write-Host "  -> Backend belum berjalan. Menjalankan server.py di latar belakang..." -ForegroundColor Yellow
    $pyExe = "C:\Users\sapuser\AppData\Local\Programs\Python\Python314\python.exe"
    $serverPy = "C:\AIGRAV\server.py"
    $outLog = "C:\AIGRAV\server_out.log"
    $errLog = "C:\AIGRAV\server_err.log"
    Start-Process -FilePath $pyExe -ArgumentList $serverPy -WorkingDirectory "C:\AIGRAV" -RedirectStandardOutput $outLog -RedirectStandardError $errLog -WindowStyle Hidden
    
    # Wait for backend to be ready (up to 20 attempts @ 1000ms)
    $retries = 0
    while ($retries -lt 20) {
        Start-Sleep -Milliseconds 1000
        try {
            $resp = Invoke-RestMethod -Uri "http://127.0.0.1:8080/api/status" -TimeoutSec 4 -ErrorAction Stop
            if ($resp.antigravity_session_id -and $resp.antigravity_session_id -ne "session_initializing") {
                Write-Host "  -> Backend berhasil terhubung! (Session: $($resp.antigravity_session_id.Substring(0,8))...)" -ForegroundColor Green
                $backendRunning = $true
                break
            }
        } catch {
            $retries++
        }
    }
}

# 3. Launch Electron Desktop HUD
Write-Host "[2/2] Membuka Frameless Desktop HUD Shell..." -ForegroundColor Cyan
$hudDir = "C:\AIGRAV\DesktopHUD"
$electronCli = Join-Path $hudDir "node_modules\electron\cli.js"

if (Test-Path $electronCli) {
    node $electronCli $hudDir
} else {
    Write-Host "[ERROR]: Electron binary tidak ditemukan di $electronCli" -ForegroundColor Red
}
