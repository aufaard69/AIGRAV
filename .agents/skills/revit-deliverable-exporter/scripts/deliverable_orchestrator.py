#!/usr/bin/env python3
"""
AIGRAV Deliverable Orchestrator
Standard Operating Engine for Revit Export (CAD, XLS, PDF, RVT) & Google Drive Synchronization.

Key Rules Enforced:
1. Pre-execution review of all export targets and potential replacements.
2. Mandatory local history backup: existing conflicting files are MOVED to `_history/`
   tagged with their exact last-modified timestamp (Zero local data loss).
3. Google Drive replace-in-place: existing files with matching names on Drive are replaced,
   and this behavior is explicitly highlighted in the pre-execution review.
"""

import os
import sys
import json
import time
import shutil
import argparse
import subprocess
from datetime import datetime
from pathlib import Path

# OpenPyXL for schedule styling
try:
    import openpyxl
    from openpyxl.styles import Font, PatternFill, Alignment, Border, Side
    from openpyxl.utils import get_column_letter
    HAS_OPENPYXL = True
except ImportError:
    HAS_OPENPYXL = False


def format_timestamp(ts):
    return datetime.fromtimestamp(ts).strftime("%Y-%m-%d %H:%M:%S")


def format_timestamp_tag(ts):
    return datetime.fromtimestamp(ts).strftime("%Y%m%d_%H%M%S")


def get_gdrive_cmd():
    """Locate the gdrive executable or wrapper."""
    custom_path = r"C:\Users\SAPUSER1\.gemini\config\plugins\gdrive\skills\gdrive\scripts\gdrive.cmd"
    if os.path.exists(custom_path):
        return custom_path
    cmd = shutil.which("gdrive")
    if cmd:
        return cmd
    return None


def run_gdrive_command(args):
    """Execute a gdrive command and return output."""
    gdrive_exe = get_gdrive_cmd()
    if not gdrive_exe:
        return {"ok": False, "error": "gdrive CLI not found in environment or custom plugin path."}
    
    full_cmd = [gdrive_exe] + args
    try:
        proc = subprocess.run(
            full_cmd,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            shell=True,
            timeout=60
        )
        if proc.returncode != 0:
            return {"ok": False, "error": proc.stderr.strip() or proc.stdout.strip()}
        return {"ok": True, "output": proc.stdout.strip()}
    except Exception as e:
        return {"ok": False, "error": str(e)}


def list_drive_files(folder_id):
    """List files currently residing in the specified Google Drive folder."""
    if not folder_id:
        return {}
    res = run_gdrive_command(["readonly", "ls", folder_id, "--max", "200"])
    if not res["ok"]:
        return {}
    
    files = {}
    lines = res["output"].splitlines()
    for line in lines:
        parts = line.strip().split()
        if len(parts) >= 2:
            f_id = parts[0]
            f_name = " ".join(parts[1:])
            files[f_name] = f_id
    return files


def generate_pre_execution_review(manifest_path):
    """
    Generate a thorough pre-execution review report:
    - Lists files slated for export.
    - Checks local directory for existing files to be moved to _history.
    - Checks Google Drive for existing files that will be replaced.
    """
    with open(manifest_path, "r", encoding="utf-8") as f:
        manifest = json.load(f)

    project_name = manifest.get("project_name", "AIGRAV_Project")
    local_dir = manifest.get("local_output_dir", "")
    drive_folder_id = manifest.get("gdrive_folder_id", "")
    formats = manifest.get("formats", ["pdf", "cad", "xls", "rvt"])
    items = manifest.get("items", [])

    os.makedirs(local_dir, exist_ok=True)
    history_dir = os.path.join(local_dir, "_history")
    
    # Query Drive items
    drive_existing = list_drive_files(drive_folder_id) if drive_folder_id else {}

    review_data = {
        "project_name": project_name,
        "local_dir": local_dir,
        "history_dir": history_dir,
        "drive_folder_id": drive_folder_id,
        "formats": formats,
        "planned_files": [],
        "local_conflicts_to_archive": [],
        "drive_conflicts_to_replace": [],
        "warnings": []
    }

    # Deduce expected filenames
    for item in items:
        base_name = item.get("target_filename") or f"{project_name}_{item.get('number', item.get('name'))}"
        item_type = item.get("type", "sheet")

        if item_type == "sheet":
            if "pdf" in formats:
                f_name = f"{base_name}.pdf"
                review_data["planned_files"].append({"name": f_name, "type": "PDF Drawing", "format": "PDF"})
            if "cad" in formats:
                f_name = f"{base_name}.dwg"
                review_data["planned_files"].append({"name": f_name, "type": "CAD Drawing", "format": "DWG"})
        elif item_type == "schedule":
            if "xls" in formats:
                f_name = f"{base_name}.xlsx"
                review_data["planned_files"].append({"name": f_name, "type": "QTO Schedule", "format": "XLSX"})
        elif item_type == "model":
            if "rvt" in formats:
                f_name = f"{base_name}_detached.rvt"
                review_data["planned_files"].append({"name": f_name, "type": "Revit Model Archive", "format": "RVT"})

    # Check local conflicts
    for planned in review_data["planned_files"]:
        f_name = planned["name"]
        local_path = os.path.join(local_dir, f_name)
        if os.path.exists(local_path):
            mtime = os.path.getmtime(local_path)
            review_data["local_conflicts_to_archive"].append({
                "filename": f_name,
                "current_path": local_path,
                "modified_time_iso": format_timestamp(mtime),
                "modified_tag": format_timestamp_tag(mtime),
                "planned_archive_dest": os.path.join(
                    history_dir,
                    datetime.fromtimestamp(mtime).strftime("%Y-%m-%d"),
                    f"{Path(f_name).stem}_MOD_{format_timestamp_tag(mtime)}{Path(f_name).suffix}"
                )
            })

        # Check Drive conflicts
        if f_name in drive_existing:
            review_data["drive_conflicts_to_replace"].append({
                "filename": f_name,
                "drive_file_id": drive_existing[f_name]
            })

    if review_data["drive_conflicts_to_replace"]:
        review_data["warnings"].append(
            f"PERINGATAN GOOGLE DRIVE: Ditemukan {len(review_data['drive_conflicts_to_replace'])} file dengan nama identik di folder Drive tujuan. Sesuai protokol, file-file ini akan LANGSUNG DI-REPLACE (in-place update)."
        )

    return review_data


def archive_local_conflicts(local_dir, conflicts):
    """
    Safely move existing conflicting files to local _history folder.
    Preserves exact LastModified timestamp in the destination filename.
    """
    archived = []
    for conflict in conflicts:
        src = conflict["current_path"]
        if not os.path.exists(src):
            continue
        dest = conflict["planned_archive_dest"]
        os.makedirs(os.path.dirname(dest), exist_ok=True)
        shutil.move(src, dest)
        archived.append({
            "filename": conflict["filename"],
            "moved_from": src,
            "moved_to": dest,
            "modified_time": conflict["modified_time_iso"]
        })
    return archived


def convert_schedule_text_to_xlsx(input_text_path, output_xlsx_path, schedule_title="QTO Schedule"):
    """
    Reads a Revit tab-separated schedule export and produces an aesthetically
    formatted Excel workbook (.xlsx) with styled headers, borders, and auto-adjusted column widths.
    """
    if not HAS_OPENPYXL:
        raise RuntimeError("openpyxl library is required for schedule formatting.")

    if not os.path.exists(input_text_path):
        raise FileNotFoundError(f"Source schedule file not found: {input_text_path}")

    wb = openpyxl.Workbook()
    ws = wb.active
    ws.title = schedule_title[:31]

    # Styling definitions
    header_fill = PatternFill(start_color="1F497D", end_color="1F497D", fill_type="solid")  # Navy Blue
    header_font = Font(name="Segoe UI", size=11, bold=True, color="FFFFFF")
    data_font = Font(name="Segoe UI", size=10)
    thin_border = Border(
        left=Side(style="thin", color="D9D9D9"),
        right=Side(style="thin", color="D9D9D9"),
        top=Side(style="thin", color="D9D9D9"),
        bottom=Side(style="thin", color="D9D9D9")
    )

    with open(input_text_path, "r", encoding="utf-16", errors="replace") as f:
        content = f.read()
    if not content.strip():
        with open(input_text_path, "r", encoding="utf-8", errors="replace") as f:
            content = f.read()

    lines = content.strip().splitlines()
    row_idx = 1
    is_header = True

    for line in lines:
        if not line.strip():
            continue
        cells = [c.strip().strip('"') for c in line.split("\t")]
        for col_idx, cell_value in enumerate(cells, 1):
            cell = ws.cell(row=row_idx, column=col_idx, value=cell_value)
            cell.border = thin_border
            if is_header:
                cell.fill = header_fill
                cell.font = header_font
                cell.alignment = Alignment(horizontal="center", vertical="center", wrap_text=True)
            else:
                cell.font = data_font
                # Align numbers right, text left
                if cell_value.replace(".", "", 1).replace("-", "", 1).isdigit():
                    cell.alignment = Alignment(horizontal="right", vertical="center")
                else:
                    cell.alignment = Alignment(horizontal="left", vertical="center")
        if is_header:
            is_header = False
            ws.row_dimensions[row_idx].height = 28
        else:
            ws.row_dimensions[row_idx].height = 20
        row_idx += 1

    # Auto-adjust column widths
    for col in ws.columns:
        max_len = 0
        col_letter = get_column_letter(col[0].column)
        for cell in col:
            val_str = str(cell.value or "")
            if len(val_str) > max_len:
                max_len = len(val_str)
        ws.column_dimensions[col_letter].width = max(max_len + 4, 12)

    os.makedirs(os.path.dirname(output_xlsx_path), exist_ok=True)
    wb.save(output_xlsx_path)
    return output_xlsx_path


def sync_file_to_drive(local_file_path, drive_folder_id, drive_file_id=None):
    """
    Uploads or updates a file in Google Drive.
    If drive_file_id exists, replaces in-place via `mutate update`.
    Otherwise, uploads via `mutate upload`.
    """
    if not os.path.exists(local_file_path):
        return {"ok": False, "error": f"Local file not found: {local_file_path}"}

    if drive_file_id:
        # Replace in-place
        res = run_gdrive_command(["mutate", "update", drive_file_id, local_file_path])
        action = "REPLACE"
    else:
        # Upload new
        res = run_gdrive_command(["mutate", "upload", local_file_path, "--parent", drive_folder_id])
        action = "UPLOAD"

    return {
        "ok": res["ok"],
        "action": action,
        "filename": os.path.basename(local_file_path),
        "output": res.get("output", ""),
        "error": res.get("error", "")
    }


def main():
    parser = argparse.ArgumentParser(description="AIGRAV Deliverable Orchestrator")
    subparsers = parser.add_subparsers(dest="action", required=True)

    # Review command
    p_review = subparsers.add_parser("review", help="Generate pre-execution review & replacement warnings")
    p_review.add_argument("--manifest", required=True, help="Path to deliverable manifest JSON")

    # Archive command
    p_archive = subparsers.add_parser("archive", help="Archive conflicting local files to _history")
    p_archive.add_argument("--manifest", required=True, help="Path to deliverable manifest JSON")

    # Format schedule command
    p_sched = subparsers.add_parser("format-schedule", help="Convert raw schedule export to styled XLSX")
    p_sched.add_argument("--input", required=True, help="Path to raw tab-delimited schedule text")
    p_sched.add_argument("--output", required=True, help="Path to target .xlsx file")
    p_sched.add_argument("--title", default="QTO Schedule", help="Worksheet title")

    # Sync drive command
    p_sync = subparsers.add_parser("sync-drive", help="Sync local deliverable file to Google Drive")
    p_sync.add_argument("--file", required=True, help="Path to local file")
    p_sync.add_argument("--folder-id", required=True, help="Google Drive destination folder ID")
    p_sync.add_argument("--file-id", default=None, help="Existing Drive file ID for replace-in-place")

    args = parser.parse_args()

    if args.action == "review":
        review = generate_pre_execution_review(args.manifest)
        print(json.dumps(review, indent=2))

    elif args.action == "archive":
        review = generate_pre_execution_review(args.manifest)
        conflicts = review.get("local_conflicts_to_archive", [])
        archived = archive_local_conflicts(review["local_dir"], conflicts)
        print(json.dumps({"status": "success", "archived_count": len(archived), "archived_items": archived}, indent=2))

    elif args.action == "format-schedule":
        out = convert_schedule_text_to_xlsx(args.input, args.output, args.title)
        print(json.dumps({"status": "success", "output_file": out}, indent=2))

    elif args.action == "sync-drive":
        res = sync_file_to_drive(args.file, args.folder_id, args.file_id)
        print(json.dumps(res, indent=2))


if __name__ == "__main__":
    main()
