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
import re
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
    custom_path = os.path.expanduser(
        os.path.join("~", ".gemini", "config", "plugins", "gdrive", "skills", "gdrive", "scripts", "gdrive.cmd")
    )
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


def _read_schedule_text(input_text_path):
    """Read Revit schedule TSV export safely across UTF-8 BOM, UTF-16, and Latin-1 encodings."""
    for enc in ("utf-8-sig", "utf-8", "utf-16", "utf-16-le", "latin1"):
        try:
            with open(input_text_path, "r", encoding=enc, errors="replace") as f:
                content = f.read()
            if content.strip():
                return content
        except Exception:
            continue
    return ""


def _wbs_sort_key(sched_name):
    """Natural sort key prioritizing WBS hierarchy (e.g., A.1.1, SP-D.1.1a, AR-B.2, C.3.1.22, KDB)."""
    name = (sched_name or "").strip()
    if re.match(r"^(?:KDB|Summary|Rekap)", name, re.IGNORECASE):
        return ("ZZ", 0, 99, 99, 99, "", name)

    match = re.match(r"^(?:([A-Z]{2,3})-)?([A-Z])\.(\d+)(?:\.(\d+))?(?:\.(\d+))?([a-z])?(?:\b|\s|$)", name)
    if match:
        disc = match.group(1) or ""
        letter = match.group(2)
        n1 = int(match.group(3))
        n2 = int(match.group(4)) if match.group(4) else 0
        n3 = int(match.group(5)) if match.group(5) else 0
        suffix = match.group(6) or ""
        return (disc, 0, ord(letter), n1, n2, n3, suffix, name)

    return ("ZZ", 1, 999, 99, 99, 99, "", name)


_UNIT_SUFFIX_PATTERNS = [
    (re.compile(r"^(-?(?:\d{1,3}(?:,\d{3})+|\d+)(?:\.\d+)?)\s*%$"), 0.01, "0.00%", "%"),
    (re.compile(r"^(-?(?:\d{1,3}(?:,\d{3})+|\d+)(?:\.\d+)?)\s*m[³3]$", re.IGNORECASE), 1.0, '#,##0.00" m³"', "m³"),
    (re.compile(r"^(-?(?:\d{1,3}(?:,\d{3})+|\d+)(?:\.\d+)?)\s*m[²2]$", re.IGNORECASE), 1.0, '#,##0.00" m²"', "m²"),
    (re.compile(r"^(-?(?:\d{1,3}(?:,\d{3})+|\d+)(?:\.\d+)?)\s*kg$", re.IGNORECASE), 1.0, '#,##0.00" kg"', "kg"),
    (re.compile(r"^(-?(?:\d{1,3}(?:,\d{3})+|\d+)(?:\.\d+)?)\s*mm$", re.IGNORECASE), 1.0, '#,##0" mm"', "mm"),
    (re.compile(r"^(-?(?:\d{1,3}(?:,\d{3})+|\d+)(?:\.\d+)?)\s*m$", re.IGNORECASE), 1.0, '#,##0.00" m"', "m"),
]
_PLAIN_NUM_RE = re.compile(r"^(-?(?:\d{1,3}(?:,\d{3})+|\d+)(?:\.\d+)?)$")


def _parse_numeric_and_unit(raw_val):
    """Parse numeric Revit schedule cell values cleanly into (number, excel_num_format, unit_label)."""
    if raw_val is None:
        return None, None, ""
    text = str(raw_val).strip()
    if not text or text in {"-", "—", "N/A"}:
        return None, None, ""

    for pat, scale, num_fmt, unit in _UNIT_SUFFIX_PATTERNS:
        m = pat.match(text)
        if m:
            num = float(m.group(1).replace(",", "")) * scale
            return num, num_fmt, unit

    m_plain = _PLAIN_NUM_RE.match(text)
    if m_plain:
        cleaned = m_plain.group(1).replace(",", "")
        if "." in cleaned:
            return float(cleaned), "#,##0.00", ""
        return int(cleaned), "#,##0", ""

    return None, None, ""


def _is_summable_column(header_name, unit_hint):
    """Identify columns safe for Excel =SUM() aggregation (avoids summing Diameters/Lengths per item unless Cut Length)."""
    h = (header_name or "").lower()
    if any(k in h for k in ["volume", "area", "luas", "berat", "weight", "qty", "number", "count", "jumlah", "total bar"]):
        return True
    if unit_hint in {"m³", "m²", "kg"}:
        return True
    if ("length" in h or "panjang" in h) and ("total" in h or "cut" in h):
        return True
    return False


def build_qto_workbook(tsv_dir, output_xlsx_path, exclude_keywords=None):
    """
    Compile exported Revit schedule TSV files into a multi-tab Smart BOQ Excel workbook (.xlsx):
    1. 'Executive BOQ Summary' tab with live cross-sheet formula links (='Tab'!Cell)
    2. 'All Schedules' combined audit tab
    3. Individual WBS-sorted schedule tabs with true numeric cells and live =SUM() Total rows.
    """
    if not HAS_OPENPYXL:
        raise RuntimeError("openpyxl library is required for QTO workbook compilation.")
    if not os.path.isdir(tsv_dir):
        raise FileNotFoundError(f"TSV directory not found: {tsv_dir}")

    excludes = [k.lower() for k in (exclude_keywords if exclude_keywords is not None else ["daftar gambar", "keynote"])]
    raw_files = [f for f in os.listdir(tsv_dir) if f.lower().endswith(".txt")]
    selected = []
    excluded = []

    for fname in raw_files:
        stem = os.path.splitext(fname)[0].strip()
        stem_lower = stem.lower()
        if any(ex in stem_lower for ex in excludes if ex):
            excluded.append(stem)
            continue
        selected.append((stem, fname))

    selected.sort(key=lambda item: _wbs_sort_key(item[0]))

    wb = openpyxl.Workbook()
    ws_summary = wb.active
    ws_summary.title = "Executive BOQ Summary"
    ws_all = wb.create_sheet(title="All Schedules")

    font_title = Font(name="Segoe UI", size=11, bold=True, color="1F497D")
    fill_title = PatternFill(start_color="E8EEF5", end_color="E8EEF5", fill_type="solid")
    font_header = Font(name="Segoe UI", size=10, bold=True, color="FFFFFF")
    fill_header = PatternFill(start_color="1F497D", end_color="1F497D", fill_type="solid")
    font_data = Font(name="Segoe UI", size=10)
    font_total = Font(name="Segoe UI", size=10, bold=True)
    fill_total = PatternFill(start_color="F2F2F2", end_color="F2F2F2", fill_type="solid")
    font_empty = Font(name="Segoe UI", size=10, bold=True, color="9C0006")
    fill_empty = PatternFill(start_color="FFC7CE", end_color="FFC7CE", fill_type="solid")
    font_ok = Font(name="Segoe UI", size=10, bold=True, color="006100")
    fill_ok = PatternFill(start_color="C6EFCE", end_color="C6EFCE", fill_type="solid")
    thin_border = Border(
        left=Side(style="thin", color="D9D9D9"),
        right=Side(style="thin", color="D9D9D9"),
        top=Side(style="thin", color="D9D9D9"),
        bottom=Side(style="thin", color="D9D9D9"),
    )

    cur_row = 1
    used_titles = {"Executive BOQ Summary", "All Schedules"}
    compiled_count = 0
    summary_entries = []

    for sched_name, fname in selected:
        content = _read_schedule_text(os.path.join(tsv_dir, fname))
        raw_lines = content.splitlines()
        parsed_rows = []
        for line in raw_lines:
            if not line.strip():
                continue
            cells = [c.strip().strip('"') for c in line.split("\t")]
            if any(c != "" for c in cells):
                parsed_rows.append(cells)

        if not parsed_rows:
            continue

        safe_title = re.sub(r"[:\\/?*\[\]]", "_", sched_name)[:31].strip() or f"Sheet_{compiled_count + 1}"
        if safe_title in used_titles:
            safe_title = f"{safe_title[:27]}_{len(used_titles)}"
        used_titles.add(safe_title)

        ws_single = wb.create_sheet(title=safe_title)
        cell_t = ws_all.cell(row=cur_row, column=1, value=sched_name)
        cell_t.font = font_title
        cur_row += 1

        # Detect optional Revit Schedule Title banner in row 0
        has_banner = len(parsed_rows) >= 2 and (
            sum(1 for c in parsed_rows[0] if c) == 1
            and parsed_rows[0][0].lower().startswith(sched_name[:12].lower())
        )
        header_idx = 1 if has_banner else 0
        headers = parsed_rows[header_idx]
        data_rows = parsed_rows[header_idx + 1:]

        # Detect whether the last row is Revit's existing Grand Total row
        has_existing_total = False
        if len(data_rows) >= 1:
            last_row = data_rows[-1]
            has_total_word = any("total" in c.lower() for c in last_row)
            leading_blank_numeric = (
                len(last_row) >= 2
                and last_row[0] == ""
                and any(_parse_numeric_and_unit(c)[0] is not None for c in last_row[1:])
            )
            if has_total_word or (leading_blank_numeric and len(data_rows) >= 2):
                has_existing_total = True

        body_rows = data_rows[:-1] if has_existing_total else data_rows
        existing_total_row = data_rows[-1] if has_existing_total else None

        s_row = 1
        if has_banner:
            for c_idx in range(1, max(len(headers), len(parsed_rows[0])) + 1):
                val = parsed_rows[0][c_idx - 1] if c_idx - 1 < len(parsed_rows[0]) else ""
                c1 = ws_single.cell(row=s_row, column=c_idx, value=val if c_idx == 1 else None)
                c1.font = font_title
                c1.fill = fill_title
                c1.border = thin_border
            ws_single.row_dimensions[s_row].height = 24
            s_row += 1

        # Write Column Header row
        header_row_single = s_row
        for c_idx, h_val in enumerate(headers, 1):
            for cell in (ws_single.cell(row=s_row, column=c_idx, value=h_val), ws_all.cell(row=cur_row, column=c_idx, value=h_val)):
                cell.border = thin_border
                cell.fill = fill_header
                cell.font = font_header
                cell.alignment = Alignment(horizontal="center", vertical="center", wrap_text=True)
        ws_single.row_dimensions[s_row].height = 24
        s_row += 1
        cur_row += 1

        data_start_row = s_row
        col_formats = {}
        col_units = {}

        for row_cells in body_rows:
            for c_idx, raw_val in enumerate(row_cells, 1):
                num_val, num_fmt, unit = _parse_numeric_and_unit(raw_val)
                if num_val is not None:
                    if c_idx not in col_formats:
                        col_formats[c_idx] = num_fmt
                    if unit and c_idx not in col_units:
                        col_units[c_idx] = unit
                    write_val = num_val
                else:
                    write_val = raw_val

                for cell in (ws_single.cell(row=s_row, column=c_idx, value=write_val), ws_all.cell(row=cur_row, column=c_idx, value=write_val)):
                    cell.border = thin_border
                    cell.font = font_data
                    if num_val is not None:
                        cell.number_format = num_fmt
                        cell.alignment = Alignment(horizontal="right", vertical="center")
                    else:
                        cell.alignment = Alignment(horizontal="left", vertical="center")
            s_row += 1
            cur_row += 1

        data_end_row = s_row - 1
        total_row_single = None

        # Write Live Excel =SUM() Total Row
        if len(body_rows) >= 1:
            total_row_single = s_row
            max_cols = max(len(headers), len(existing_total_row or []))
            wrote_label = False
            for c_idx in range(1, max_cols + 1):
                h_name = headers[c_idx - 1] if c_idx - 1 < len(headers) else ""
                orig_tot = existing_total_row[c_idx - 1] if existing_total_row and c_idx - 1 < len(existing_total_row) else ""
                orig_num, orig_fmt, orig_unit = _parse_numeric_and_unit(orig_tot)
                unit_hint = orig_unit or col_units.get(c_idx, "")
                fmt_hint = orig_fmt or col_formats.get(c_idx, "#,##0.00")

                should_sum = (orig_num is not None) or (existing_total_row is None and c_idx in col_formats and _is_summable_column(h_name, unit_hint))
                col_letter = get_column_letter(c_idx)

                if should_sum:
                    single_val = f"=SUM({col_letter}{data_start_row}:{col_letter}{data_end_row})"
                    all_val = orig_num if orig_num is not None else single_val
                elif not wrote_label and c_idx == 1:
                    single_val = orig_tot if orig_tot else "Total"
                    all_val = single_val
                    wrote_label = True
                else:
                    single_val = orig_tot or None
                    all_val = single_val

                c1 = ws_single.cell(row=s_row, column=c_idx, value=single_val)
                c2 = ws_all.cell(row=cur_row, column=c_idx, value=all_val)
                for cell in (c1, c2):
                    cell.border = thin_border
                    cell.fill = fill_total
                    cell.font = font_total
                    if should_sum:
                        cell.number_format = fmt_hint
                        cell.alignment = Alignment(horizontal="right", vertical="center")
                    else:
                        cell.alignment = Alignment(horizontal="left", vertical="center")
            s_row += 1
            cur_row += 1

        # Collect metrics for Executive BOQ Summary tab
        metric_links = []
        if total_row_single is not None:
            for c_idx in range(1, len(headers) + 1):
                h_name = headers[c_idx - 1]
                unit_hint = col_units.get(c_idx, "")
                fmt_hint = col_formats.get(c_idx)
                cell_val = ws_single.cell(row=total_row_single, column=c_idx).value
                if isinstance(cell_val, str) and cell_val.startswith("=SUM(") and _is_summable_column(h_name, unit_hint):
                    col_letter = get_column_letter(c_idx)
                    escaped_tab = safe_title.replace("'", "''")
                    metric_links.append((h_name, f"='{escaped_tab}'!{col_letter}{total_row_single}", unit_hint or h_name, fmt_hint or "#,##0.00"))

        summary_entries.append({
            "name": sched_name,
            "tab": safe_title,
            "data_rows": len(body_rows),
            "metrics": metric_links[:2],
        })

        ws_single.freeze_panes = f"A{header_row_single + 1}"
        cur_row += 2
        compiled_count += 1

        for col in ws_single.columns:
            max_len = max((len(str(c.value or "")) if not (isinstance(c.value, str) and c.value.startswith("=")) else 14) for c in col)
            ws_single.column_dimensions[get_column_letter(col[0].column)].width = min(max(max_len + 3, 12), 48)

    # Populate Executive BOQ Summary Tab (Tab 1)
    ws_summary.cell(row=1, column=1, value="AIGRAV QTO v4.0 — EXECUTIVE BOQ SUMMARY").font = Font(name="Segoe UI", size=13, bold=True, color="1F497D")
    sum_headers = ["No", "WBS / Schedule Name", "Worksheet Tab", "Data Rows", "Primary Quantity", "Label / Unit 1", "Secondary Quantity", "Label / Unit 2", "Status"]
    for c_idx, h in enumerate(sum_headers, 1):
        c = ws_summary.cell(row=3, column=c_idx, value=h)
        c.fill = fill_header
        c.font = font_header
        c.border = thin_border
        c.alignment = Alignment(horizontal="center", vertical="center")
    ws_summary.row_dimensions[3].height = 24

    for idx, entry in enumerate(summary_entries, 1):
        r = idx + 3
        row_vals = [
            idx,
            entry["name"],
            entry["tab"],
            entry["data_rows"],
            entry["metrics"][0][1] if len(entry["metrics"]) >= 1 else None,
            entry["metrics"][0][2] if len(entry["metrics"]) >= 1 else "",
            entry["metrics"][1][1] if len(entry["metrics"]) >= 2 else None,
            entry["metrics"][1][2] if len(entry["metrics"]) >= 2 else "",
            "EMPTY" if entry["data_rows"] == 0 else "OK",
        ]
        for c_idx, val in enumerate(row_vals, 1):
            cell = ws_summary.cell(row=r, column=c_idx, value=val)
            cell.border = thin_border
            cell.font = font_data
            if c_idx in (1, 4):
                cell.alignment = Alignment(horizontal="center", vertical="center")
            elif c_idx == 5 and len(entry["metrics"]) >= 1:
                cell.number_format = entry["metrics"][0][3]
                cell.alignment = Alignment(horizontal="right", vertical="center")
            elif c_idx == 7 and len(entry["metrics"]) >= 2:
                cell.number_format = entry["metrics"][1][3]
                cell.alignment = Alignment(horizontal="right", vertical="center")
            elif c_idx == 9:
                cell.alignment = Alignment(horizontal="center", vertical="center")
                if val == "EMPTY":
                    cell.fill = fill_empty
                    cell.font = font_empty
                else:
                    cell.fill = fill_ok
                    cell.font = font_ok
            else:
                cell.alignment = Alignment(horizontal="left", vertical="center")

    ws_summary.freeze_panes = "A4"
    for ws in (ws_summary, ws_all):
        for col in ws.columns:
            max_len = max((len(str(c.value or "")) if not (isinstance(c.value, str) and c.value.startswith("=")) else 14) for c in col)
            ws.column_dimensions[get_column_letter(col[0].column)].width = min(max(max_len + 3, 12), 52)

    os.makedirs(os.path.dirname(os.path.abspath(output_xlsx_path)), exist_ok=True)
    wb.save(output_xlsx_path)
    return {
        "output_file": output_xlsx_path,
        "compiled_schedules": compiled_count,
        "excluded_schedules": excluded,
        "total_tabs": len(wb.sheetnames),
        "empty_schedules": sum(1 for e in summary_entries if e["data_rows"] == 0),
    }


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

    content = _read_schedule_text(input_text_path)

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

    # Format single schedule command
    p_sched = subparsers.add_parser("format-schedule", help="Convert raw schedule export to styled XLSX")
    p_sched.add_argument("--input", required=True, help="Path to raw tab-delimited schedule text")
    p_sched.add_argument("--output", required=True, help="Path to target .xlsx file")
    p_sched.add_argument("--title", default="QTO Schedule", help="Worksheet title")

    # Compile multi-tab WBS QTO workbook command
    p_wb = subparsers.add_parser("build-qto-workbook", help="Compile TSV folder into multi-tab styled WBS QTO workbook")
    p_wb.add_argument("--tsv-dir", required=True, help="Directory containing exported Revit schedule .txt files")
    p_wb.add_argument("--output", required=True, help="Path to target multi-tab .xlsx workbook")
    p_wb.add_argument("--exclude", nargs="*", default=["daftar gambar", "keynote"], help="Schedule name substrings to exclude")

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

    elif args.action == "build-qto-workbook":
        summary = build_qto_workbook(args.tsv_dir, args.output, args.exclude)
        print(json.dumps({"status": "success", **summary}, indent=2))

    elif args.action == "sync-drive":
        res = sync_file_to_drive(args.file, args.folder_id, args.file_id)
        print(json.dumps(res, indent=2))


if __name__ == "__main__":
    main()
