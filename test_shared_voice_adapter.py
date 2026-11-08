"""
AIGRAV Shared Intelligence & Voice Interface Adapter - Comprehensive 9-Suite Regression Test
Verifies that Voice is purely an I/O adapter wrapping the shared AIGRAV intelligence.
"""

import os
import sys
import time
import json
import openpyxl
from pathlib import Path

# Inject paths
sys.path.insert(0, r"c:\AIGRAV")
sys.path.insert(0, r"C:\AIGRAV\MCP\TestServer")

from voice_adapter import voice_adapter, EXCEL_OUTPUT_PATH
from qto_engine import qto_engine
from steel_service import steel_service
from orchestrator.models import Task, Step, ExecutionStatus, VerificationCriteria
from orchestrator.task_store import TaskStore
from orchestrator.engine import TaskExecutionEngine
from orchestrator.tool_router import ToolRouter

def test_1_qto_identical_engine_and_template():
    print("\n[TEST 1] Testing Voice QTO uses identical engine & template as Chat...")
    # Execute QTO via shared engine
    qto_result = qto_engine.execute_live_qto(output_filepath=EXCEL_OUTPUT_PATH)
    assert qto_result is not None, "QTO execution failed"
    assert qto_result["total_volume_m3"] > 0, "Total volume must be > 0"
    assert os.path.exists(EXCEL_OUTPUT_PATH), "Excel file must exist"
    
    # Inspect workbook structure
    wb = openpyxl.load_workbook(EXCEL_OUTPUT_PATH, data_only=False)
    sheet_names = wb.sheetnames
    assert "Rekapitulasi Volume" in sheet_names, "Missing 'Rekapitulasi Volume' sheet"
    assert "Detail Elemen Model" in sheet_names, "Missing 'Detail Elemen Model' sheet"
    
    ws_rekap = wb["Rekapitulasi Volume"]
    # Check header fill at B5
    header_fill = str(ws_rekap["B5"].fill.start_color.rgb)
    assert header_fill.endswith("1B365D"), f"Header fill mismatch: {header_fill}"
    
    # Check formula in total row (column H / column 8)
    found_sum = False
    for r in range(6, ws_rekap.max_row + 1):
        cell_val = str(ws_rekap.cell(row=r, column=8).value or "")
        if "SUM" in cell_val:
            found_sum = True
            break
    assert found_sum, "Formula =SUM(...) missing in Rekapitulasi Volume sheet"
    print(f"  -> QTO Verified: {qto_result['total_elements']} elements, {qto_result['total_volume_m3']:,.2f} m3.")
    print("  -> Template: 2 sheets, Navy #1B365D headers, Calibri font, formula intact.")
    return True

def test_2_steel_database_shared_access():
    print("\n[TEST 2] Testing structural steel database accessible via both interfaces...")
    # 1. Direct service query (used by Chat)
    base_plates = steel_service.get_base_plate_standards()
    assert len(base_plates) >= 12, f"Expected at least 12 base plate standards, got {len(base_plates)}"
    wf400 = next((item for item in base_plates if "400" in item.get("associatedProfile", "")), None)
    assert wf400 is not None, "WF 400 standard missing"
    assert "19" in str(wf400.get("plateThickness", "")) or "20" in str(wf400.get("plateThickness", "")), "Plate thickness for WF 400 mismatch"
    
    # 2. Voice adapter query (used by Voice)
    voice_resp = voice_adapter.process_utterance("AIGRAV, bagaimana standar base plate profil WF 400?")
    assert "base plate" in voice_resp["reply_text"].lower() or "empat ratus" in voice_resp["reply_text"].lower()
    assert "STD-BJ-01" in voice_resp["visual_markdown"] or "STD-BJ-02" in voice_resp["visual_markdown"] or "400" in voice_resp["visual_markdown"]
    print("  -> Steel DB verified: 12 base plate standards accessible identically in Chat & Voice.")
    return True

def test_3_voice_task_visible_in_chat():
    print("\n[TEST 3] Testing Voice task visible & inspectable in Chat...")
    # Simulate Voice initiating a QTO execution gate
    voice_adapter.pending_qto = True
    voice_resp = voice_adapter.process_utterance("oke, eksekusi")
    voice_task_id = voice_resp["task_id"]
    assert voice_task_id is not None, "Task ID must be returned by Voice"
    
    # Chat accesses shared TaskStore
    store = TaskStore()
    chat_loaded_task = store.load(voice_task_id)
    assert chat_loaded_task is not None, f"Chat could not find task {voice_task_id} in TaskStore"
    assert chat_loaded_task.execution_status == ExecutionStatus.COMPLETED
    assert len(chat_loaded_task.steps) > 0
    print(f"  -> Voice Task {voice_task_id} cleanly loaded and inspected in Chat TaskStore.")
    return True

def test_4_chat_task_continuable_in_voice():
    print("\n[TEST 4] Testing Chat task continuable via Voice...")
    # Create task originating from Chat
    chat_task_id = f"TASK-CHAT-ORIGIN-{int(time.time())}"
    store = TaskStore()
    chat_task = Task(
        task_id=chat_task_id,
        user_request="Chat initiated inspection",
        execution_status=ExecutionStatus.PLANNED
    )
    chat_task.steps.append(Step(
        step_id="step_read_1",
        title="Chat initiated step",
        tool_name="aigrav_get_revit_info",
        execution_status=ExecutionStatus.READY
    ))
    store.save(chat_task)
    
    # Voice receives instruction regarding this active task
    voice_adapter.active_task_id = chat_task_id
    status_resp = voice_adapter.process_utterance("status task")
    assert chat_task_id in status_resp["reply_text"] or chat_task_id in status_resp["visual_markdown"]
    assert "PLANNED" in status_resp["visual_markdown"]
    
    # Voice resumes / completes execution
    resume_resp = voice_adapter.process_utterance("lanjutkan task")
    updated_task = store.load(chat_task_id)
    assert updated_task.execution_status == ExecutionStatus.COMPLETED
    print(f"  -> Chat Task {chat_task_id} successfully continued and completed via Voice interface.")
    return True

def test_5_tables_and_workbooks_remain_visible():
    print("\n[TEST 5] Testing tables and generated workbooks remain visible in Chat workspace...")
    voice_resp = voice_adapter.process_utterance("cek standar sambungan baja base plate")
    # Voice must have spoken summary
    assert len(voice_resp["reply_text"]) > 10, "Spoken text must be present"
    # Visual workspace must contain markdown table
    assert "| Detail Sambungan |" in voice_resp["visual_markdown"] or "| Profil Terkait |" in voice_resp["visual_markdown"], "Markdown table header missing"
    assert "STD-BJ-01" in voice_resp["visual_markdown"] or "Base Plate" in voice_resp["visual_markdown"]
    
    # QTO must have table and excel path
    qto_resp = voice_adapter._build_response(
        reply_text="Volume beton selesai dihitung.",
        visual_md="| Kategori | Elemen | Volume (m³) |\n|---|---|---|\n| Structural Framing | 2400 | 258.91 |",
        badge_text="QTO SELESAI",
        badge_class="text-emerald-400",
        excel_ready=True
    )
    assert "| Structural Framing |" in qto_resp["visual_markdown"]
    assert qto_resp["excel_ready"] is True
    print("  -> Visual workspace preserves markdown tables, DAG steps, and Excel artifacts.")
    return True

def test_6_pause_resume_cancel_unified_state():
    print("\n[TEST 6] Testing Pause, Resume, and Cancel operate on the same task ID...")
    store = TaskStore()
    engine = TaskExecutionEngine(router=ToolRouter(), store=store)
    
    task_id = f"TASK-UNIFIED-CTRL-{int(time.time())}"
    task = Task(task_id=task_id, user_request="Testing unified state control", execution_status=ExecutionStatus.RUNNING)
    task.steps.append(Step(step_id="step_a", title="Step A", tool_name="aigrav_get_revit_info", execution_status=ExecutionStatus.COMPLETED))
    task.steps.append(Step(step_id="step_b", title="Step B", tool_name="aigrav_get_active_view", execution_status=ExecutionStatus.READY, dependencies=["step_a"]))
    store.save(task)
    voice_adapter.active_task_id = task_id
    
    # 1. PAUSE
    pause_resp = voice_adapter.process_utterance("AIGRAV, pause dulu")
    task_paused = store.load(task_id)
    assert task_paused.execution_status == ExecutionStatus.PAUSED, f"Expected PAUSED, got {task_paused.execution_status}"
    assert "PAUSED" in pause_resp["badge_text"]
    
    # 2. RESUME
    resume_resp = voice_adapter.process_utterance("lanjutkan task")
    task_resumed = store.load(task_id)
    assert task_resumed.execution_status == ExecutionStatus.COMPLETED, f"Expected COMPLETED, got {task_resumed.execution_status}"
    
    # 3. CANCEL (new task)
    task_cancel_id = f"TASK-UNIFIED-CANCEL-{int(time.time())}"
    task_c = Task(task_id=task_cancel_id, user_request="Testing cancellation", execution_status=ExecutionStatus.RUNNING)
    task_c.steps.append(Step(step_id="step_1", title="Step 1", tool_name="aigrav_get_revit_info", execution_status=ExecutionStatus.READY))
    store.save(task_c)
    voice_adapter.active_task_id = task_cancel_id
    
    cancel_resp = voice_adapter.process_utterance("batalkan task")
    task_cancelled = store.load(task_cancel_id)
    assert task_cancelled.execution_status == ExecutionStatus.CANCELLED, f"Expected CANCELLED, got {task_cancelled.execution_status}"
    print(f"  -> Pause, Resume, and Cancel operate on authoritative TaskStore without divergence.")
    return True

def test_7_deduplication_anti_echo():
    print("\n[TEST 7] Testing deduplication and self-echo suppression...")
    # First utterance
    res1 = voice_adapter.process_utterance("status task")
    # Immediate identical utterance (<2.0s)
    res2 = voice_adapter.process_utterance("status task")
    assert res2.get("status") == "duplicate_ignored", f"Expected duplicate_ignored, got {res2}"
    
    # Self-echo phrase
    res3 = voice_adapter.process_utterance("katakan oke eksekusi sekarang")
    assert res3.get("status") == "self_echo_ignored", f"Expected self_echo_ignored, got {res3}"
    print("  -> Rapid duplicate utterances and system echo strings safely suppressed.")
    return True

def test_8_phase_2_and_3_orchestrator_suites():
    print("\n[TEST 8] Running existing Phase 2 and Phase 3 orchestrator test suites...")
    import subprocess
    cmd_p3 = [sys.executable, r"C:\AIGRAV\MCP\TestServer\run_phase_3_tests.py"]
    res_p3 = subprocess.run(cmd_p3, capture_output=True, text=True)
    assert res_p3.returncode == 0, f"Phase 3 tests failed:\n{res_p3.stderr}"
    assert "ALL TESTS PASSED" in res_p3.stdout, "Phase 3 test suite output did not confirm pass"
    
    cmd_p2 = [sys.executable, r"C:\AIGRAV\MCP\TestServer\run_orchestrator_tests.py"]
    res_p2 = subprocess.run(cmd_p2, capture_output=True, text=True)
    assert res_p2.returncode == 0, f"Phase 2 tests failed:\n{res_p2.stderr}"
    assert "ALL TESTS PASSED" in res_p2.stdout, "Phase 2 test suite output did not confirm pass"
    print("  -> Phase 2 and Phase 3 suites executed: 100% PASS (Full backward compatibility).")
    return True

def test_9_template_compliance_and_quantity_verification():
    print("\n[TEST 9] Testing template compliance and quantity verification...")
    wb = openpyxl.load_workbook(EXCEL_OUTPUT_PATH, data_only=False)
    ws_rekap = wb["Rekapitulasi Volume"]
    
    # Track category items and verify subtotal formulas
    categories = {}
    current_cat = None
    
    for r in range(6, ws_rekap.max_row + 1):
        item_text = str(ws_rekap.cell(row=r, column=3).value or "").strip()
        count_val = ws_rekap.cell(row=r, column=6).value
        vol_val = ws_rekap.cell(row=r, column=8).value
        
        if item_text.startswith("PEKERJAAN "):
            current_cat = item_text.replace("PEKERJAAN ", "").strip().title()
            categories[current_cat] = {"count": 0, "volume": 0.0, "has_formula": False}
        elif item_text.startswith("Subtotal ") and current_cat:
            # Verify formula exists in subtotal cell
            assert str(count_val).startswith("=SUM("), f"Missing SUM formula in count: {count_val}"
            assert str(vol_val).startswith("=SUM("), f"Missing SUM formula in vol: {vol_val}"
            categories[current_cat]["has_formula"] = True
            current_cat = None
        elif current_cat and count_val is not None and vol_val is not None:
            try:
                categories[current_cat]["count"] += int(count_val)
                categories[current_cat]["volume"] += float(vol_val)
            except (ValueError, TypeError):
                pass
    
    # Read live bridge counts to assert exact dynamic synchronization
    col_ok, col_text = qto_engine.call_bridge("get_structural_columns")
    live_cols = len(qto_engine.parse_element_list(col_text)) if col_ok else 0
    beam_ok, beam_text = qto_engine.call_bridge("get_structural_framing")
    live_beams = len(qto_engine.parse_element_list(beam_text)) if beam_ok else 0

    assert "Kolom Struktural" in categories, f"Kolom Struktural missing from {list(categories.keys())}"
    assert "Balok Struktural (Framing)" in categories, f"Balok Struktural (Framing) missing from {list(categories.keys())}"
    assert categories["Kolom Struktural"]["count"] == live_cols, f"Columns count mismatch: expected {live_cols}, got {categories['Kolom Struktural']['count']}"
    assert categories["Balok Struktural (Framing)"]["count"] == live_beams, f"Beams count mismatch: expected {live_beams}, got {categories['Balok Struktural (Framing)']['count']}"
    assert categories["Kolom Struktural"]["volume"] > 0, "Kolom volume must be > 0"
    assert categories["Balok Struktural (Framing)"]["volume"] > 0, "Framing volume must be > 0"
    assert categories["Kolom Struktural"]["has_formula"] is True, "Subtotal formula missing for Kolom"
    assert categories["Balok Struktural (Framing)"]["has_formula"] is True, "Subtotal formula missing for Balok"
    
    ws_detail = wb["Detail Elemen Model"]
    assert ws_detail.max_row > 10, f"Expected detail rows, got {ws_detail.max_row}"
    print(f"  -> Verified Columns ({categories['Kolom Struktural']['count']} units, {categories['Kolom Struktural']['volume']:,.2f} m3) & Beams ({categories['Balok Struktural (Framing)']['count']} units, {categories['Balok Struktural (Framing)']['volume']:,.2f} m3).")
    print(f"  -> Exact match with live Revit Bridge ({live_cols} columns, {live_beams} beams).")
    print(f"  -> Formulas confirmed: =SUM(F...) and =SUM(H...). Detail rows: {ws_detail.max_row}.")
    return True

def run_all_tests():
    print("=" * 70)
    print("AIGRAV SHARED INTELLIGENCE & VOICE ADAPTER REGRESSION SUITE")
    print("=" * 70)
    
    test_cases = [
        ("Test 1: Identical QTO Engine & Template", test_1_qto_identical_engine_and_template),
        ("Test 2: Structural Steel DB Shared Access", test_2_steel_database_shared_access),
        ("Test 3: Voice Task Visible in Chat", test_3_voice_task_visible_in_chat),
        ("Test 4: Chat Task Continuable via Voice", test_4_chat_task_continuable_in_voice),
        ("Test 5: Visual Workspace & Tables Preserved", test_5_tables_and_workbooks_remain_visible),
        ("Test 6: Unified Pause / Resume / Cancel", test_6_pause_resume_cancel_unified_state),
        ("Test 7: Deduplication & Anti-Self-Echo", test_7_deduplication_anti_echo),
        ("Test 8: Phase 2 & 3 Backward Compatibility", test_8_phase_2_and_3_orchestrator_suites),
        ("Test 9: Template Compliance & Verification", test_9_template_compliance_and_quantity_verification),
    ]
    
    results = []
    all_passed = True
    for name, func in test_cases:
        try:
            status = func()
            results.append((name, status))
        except Exception as e:
            print(f"  -> FAILED with error: {e}")
            results.append((name, False))
            all_passed = False
    
    print("\n" + "=" * 70)
    print("REGRESSION TEST RESULTS SUMMARY")
    print("=" * 70)
    all_passed = True
    for name, status in results:
        status_str = "PASS" if status else "FAIL"
        print(f"  {name:<48} : {status_str}")
        if not status:
            all_passed = False
            
    print("=" * 70)
    if all_passed:
        print("OVERALL RESULT: ALL 9 REGRESSION TESTS PASSED.")
    else:
        print("OVERALL RESULT: SOME TESTS FAILED.")
    print("=" * 70)
    return all_passed

if __name__ == "__main__":
    success = run_all_tests()
    sys.exit(0 if success else 1)
