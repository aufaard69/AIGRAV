<#
.SYNOPSIS
  Calls AIGRAV Revit Bridge to import DWGs and place Drafting Views onto Sheets.
.EXAMPLE
  .\create_views_and_place.ps1 -ViewsJsonPath "..\resources\cad_views.json"
#>

param(
    [string]$BridgeUrl = "http://127.0.0.1:8765/",
    [string]$ViewsJsonPath = ""
)

$argument = ""
if ($ViewsJsonPath -and (Test-Path $ViewsJsonPath)) {
    $argument = Get-Content $ViewsJsonPath -Raw
}

$body = @{
    Tool = "aigrav_create_drafting_views_and_place"
    Argument = $argument
} | ConvertTo-Json

Write-Host "[AIGRAV] Sending request to Revit Bridge..." -ForegroundColor Cyan
$response = Invoke-RestMethod -Uri $BridgeUrl -Method Post -Body $body -ContentType "application/json" -TimeoutSec 60
$response.Result
