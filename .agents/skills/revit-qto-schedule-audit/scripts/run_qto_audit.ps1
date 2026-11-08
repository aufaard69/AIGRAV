param(
    [ValidateSet("audit", "cleanup", "anti-double-count", "batch1", "batch2", "batch3", "inspect")]
    [string]$Action = "audit",
    [string]$TargetSchedule = ""
)

$basePath = "C:\AIGRAV\call_revit.ps1"

switch ($Action) {
    "audit" {
        Write-Host "Running QA/QC Audit on all QTO Schedules..." -ForegroundColor Cyan
        powershell -ExecutionPolicy Bypass -File $basePath -Tool aigrav_qa_audit_all_schedules -Argument $TargetSchedule
    }
    "cleanup" {
        Write-Host "Running Cleanup of Orphaned/Copy Schedules..." -ForegroundColor Yellow
        powershell -ExecutionPolicy Bypass -File $basePath -Tool aigrav_cleanup_orphaned_copies
    }
    "anti-double-count" {
        Write-Host "Applying Anti-Double Counting Filters to Bangunan Utama..." -ForegroundColor Green
        powershell -ExecutionPolicy Bypass -File $basePath -Tool aigrav_apply_anti_double_counting
    }
    "batch1" {
        Write-Host "Executing Batch 1 (Carton & Cups + Production Office)..." -ForegroundColor Cyan
        powershell -ExecutionPolicy Bypass -File $basePath -Tool aigrav_exec_subagent1_batch1
    }
    "batch2" {
        Write-Host "Executing Batch 2 (Waterline + Power House)..." -ForegroundColor Cyan
        powershell -ExecutionPolicy Bypass -File $basePath -Tool aigrav_exec_subagent2_batch2
    }
    "batch3" {
        Write-Host "Executing Batch 3 (Chiller Area 1 & 2)..." -ForegroundColor Cyan
        powershell -ExecutionPolicy Bypass -File $basePath -Tool aigrav_exec_subagent3_batch3
    }
    "inspect" {
        if (-not $TargetSchedule) {
            Write-Error "Please specify -TargetSchedule '<Schedule Name>'"
            return
        }
        Write-Host "Inspecting Definition of '$TargetSchedule'..." -ForegroundColor Cyan
        powershell -ExecutionPolicy Bypass -File $basePath -Tool aigrav_inspect_schedule_def -Argument $TargetSchedule
    }
}
