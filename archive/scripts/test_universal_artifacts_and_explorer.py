"""
Test Suite: Universal Output File Delivery & Auto-Open Windows Explorer
Tests all requirements of AIGRAV Hotfix:
1. Strict Post-Generation Artifact Verification
2. Safe Windows Explorer Auto-Opening & Grouping
3. Strict Task & Artifact Boundaries (No global leakage)
4. Idempotency Guard (No repeated popups)
5. Read-Only / Chat / Canceled Task Isolation (No explorer open)
6. Natural Voice Delivery in Indonesian
7. Zero Legacy QTO Button in UI & Server Endpoints
"""

import os
import sys
import json
import time
import subprocess
from pathlib import Path

# Add project root to path
sys.path.insert(0, r"C:\AIGRAV")
sys.path.insert(0, r"C:\AIGRAV\MCP\TestServer")

from voice_adapter import (
    voice_adapter,
    TaskArtifact,
    verify_output_artifact,
    safe_auto_open_in_explorer,
    EXCEL_OUTPUT_PATH,
)


def test_1_artifact_verification_model():
    print("\n[TEST 1] Testing Universal Artifact Metadata & Verification Logic...")
    
    # 1. Existing valid Excel file
    art_xlsx = verify_output_artifact("TASK-TEST-1", EXCEL_OUTPUT_PATH, "spreadsheet")
    assert art_xlsx.exists is True, "Valid Excel file must exist"
    assert art_xlsx.validation_status == "verified", "Valid Excel file must be verified"
    assert art_xlsx.file_extension == ".xlsx", "Extension must be .xlsx"
    assert art_xlsx.size_bytes > 0, "File size must be positive"
    assert "spreadsheet" in art_xlsx.artifact_type, "Artifact type must be spreadsheet"
    assert "openxml" in art_xlsx.mime_type or "spreadsheet" in art_xlsx.mime_type, "MIME type must be spreadsheet"
    
    # Test dictionary conversion
    d = art_xlsx.to_dict()
    assert d["artifact_id"] == art_xlsx.artifact_id
    assert d["task_id"] == "TASK-TEST-1"
    assert d["validation_status"] == "verified"
    print("  -> Verified: Valid Excel artifact metadata contract fulfilled.")

    # 2. Non-existent file
    art_missing = verify_output_artifact("TASK-TEST-2", r"C:\AIGRAV\non_existent_file.pdf", "report")
    assert art_missing.exists is False, "Missing file must report exists=False"
    assert art_missing.validation_status == "failed", "Missing file must report validation_status='failed'"
    print("  -> Verified: Missing file correctly rejected with validation_status='failed'.")

    # 3. Directory path (not a file)
    art_dir = verify_output_artifact("TASK-TEST-3", r"C:\AIGRAV\audio_cache")
    assert art_dir.validation_status == "failed", "Directory must not be verified as a file"
    print("  -> Verified: Directory target correctly rejected.")

    # 4. Zero-byte file
    zero_file = r"C:\AIGRAV\test_zero_byte.tmp"
    with open(zero_file, "wb") as f:
        pass
    try:
        art_zero = verify_output_artifact("TASK-TEST-4", zero_file, "data")
        assert art_zero.validation_status == "failed", "Zero-byte file must fail verification"
        print("  -> Verified: Zero-byte file correctly rejected.")
    finally:
        if os.path.exists(zero_file):
            os.remove(zero_file)

    # 5. Corrupted header file (.xlsx with non-zip content)
    corrupt_file = r"C:\AIGRAV\test_corrupt.xlsx"
    with open(corrupt_file, "w", encoding="utf-8") as f:
        f.write("NOT_A_ZIP_HEADER_PLAIN_TEXT")
    try:
        art_corrupt = verify_output_artifact("TASK-TEST-5", corrupt_file, "spreadsheet")
        assert art_corrupt.validation_status == "failed", "Corrupt xlsx header must fail verification"
        print("  -> Verified: Header validation correctly detected corrupted file.")
    finally:
        if os.path.exists(corrupt_file):
            os.remove(corrupt_file)


def test_2_qto_execution_and_auto_open():
    print("\n[TEST 2] Testing QTO Gated Execution with Auto-Open Windows Explorer...")
    
    # Reset state
    voice_adapter.pending_qto = False
    voice_adapter.pending_qto_categories = None

    # Step 1: User requests QTO for slabs and foundations
    prompt = "Hitung volume plat lantai dan pondasi"
    res1 = voice_adapter.process_utterance(prompt)
    assert voice_adapter.pending_qto is True, "Pending QTO must be True"
    assert res1.get("badge_text") == "MENUNGGU: 'OKE EKSEKUSI'"
    assert "plat lantai" in res1["reply_text"].lower() or "pondasi" in res1["reply_text"].lower()
    # Read-only planning turn must NOT produce artifacts or open Explorer
    assert len(res1.get("artifacts", [])) == 0, "Planning turn must not produce artifacts"
    assert res1.get("auto_opened") is False, "Planning turn must not open Explorer"

    # Step 2: User confirms execution
    res2 = voice_adapter.process_utterance("oke, eksekusi")
    assert res2.get("badge_text") == "EKSEKUSI QTO SELESAI"
    assert res2.get("task_status") == "COMPLETED"
    
    # Artifacts must be present and verified
    artifacts = res2.get("artifacts", [])
    assert len(artifacts) >= 1, "At least 1 verified artifact must be returned"
    qto_art = artifacts[0]
    assert qto_art["validation_status"] == "verified"
    assert qto_art["file_extension"] == ".xlsx"
    assert qto_art["opened_in_explorer"] is True
    assert res2.get("auto_opened") is True, "auto_opened must be True"

    # Natural Indonesian speech reporting
    speech = res2["reply_text"]
    assert "diverifikasi" in speech.lower() or "terverifikasi" in speech.lower()
    assert "membuka folder output" in speech.lower() or "sudah membuka" in speech.lower()
    assert str(EXCEL_OUTPUT_PATH) in speech or os.path.basename(EXCEL_OUTPUT_PATH) in speech
    print(f"  -> Natural Voice Reply: \"{speech}\"")
    print("  -> Verified: QTO execution successfully produced verified artifact and triggered Explorer auto-open.")


def test_3_idempotency_and_no_duplicate_popups():
    print("\n[TEST 3] Testing Idempotency & Duplicate Popup Prevention...")
    active_tid = voice_adapter.active_task_id
    assert active_tid in voice_adapter.opened_task_ids, "Active task must be registered in opened_task_ids"

    # Repeating execution or status inquiry should NOT open Explorer again
    artifacts = voice_adapter.task_artifacts.get(active_tid, [])
    assert len(artifacts) > 0, "Artifacts must exist for task"
    
    # Calling safe_auto_open_in_explorer again without force must return False
    opened_again, targets = safe_auto_open_in_explorer(artifacts, task_id=active_tid, force=False)
    assert opened_again is False, "Idempotency must prevent re-opening explorer"
    assert len(targets) == 0
    print("  -> Verified: Idempotency guard strictly prevented duplicate Explorer windows.")


def test_4_read_only_and_general_chat_isolation():
    print("\n[TEST 4] Testing Read-Only Query & General Chat Isolation...")
    
    # 1. Direct count query
    res_count = voice_adapter.process_utterance("Berapa jumlah kolom pada model?")
    assert ("572" in res_count["reply_text"] or "28" in res_count["reply_text"]) and "kolom" in res_count["reply_text"]
    assert len(res_count.get("artifacts", [])) == 0, "Read-only query must have 0 artifacts"
    assert res_count.get("auto_opened") is False, "Read-only query must not open Explorer"
    print("  -> Verified: Read-only query produced 0 artifacts and did not invoke Explorer.")

    # 2. General conversation / emotional venting
    res_vent = voice_adapter.process_utterance("Hari ini capek sekali di proyek konstruksi")
    assert res_vent["mode"] == "general"
    assert len(res_vent.get("artifacts", [])) == 0, "General chat must have 0 artifacts"
    assert res_vent.get("auto_opened") is False, "General chat must not open Explorer"
    print("  -> Verified: General chat produced 0 artifacts and did not invoke Explorer.")


def test_5_canceled_task_isolation():
    print("\n[TEST 5] Testing Canceled Task Isolation...")
    
    # User prepares QTO
    voice_adapter.process_utterance("Hitung volume dinding")
    assert voice_adapter.pending_qto is True

    # User cancels task
    res_cancel = voice_adapter.process_utterance("batalkan task")
    assert voice_adapter.pending_qto is False
    assert len(res_cancel.get("artifacts", [])) == 0
    assert res_cancel.get("auto_opened") is False
    print("  -> Verified: Canceled task never triggered Explorer or reported success.")


def test_6_multi_file_grouping_logic():
    print("\n[TEST 6] Testing Multi-Artifact Grouping & Folder Opening Logic...")
    
    # Create two temporary files in the same folder
    test_dir = r"C:\AIGRAV\TempTestFolder"
    os.makedirs(test_dir, exist_ok=True)
    f1 = os.path.join(test_dir, "report1.pdf")
    f2 = os.path.join(test_dir, "report2.pdf")
    
    with open(f1, "wb") as f:
        f.write(b"%PDF-1.4 test report 1")
    with open(f2, "wb") as f:
        f.write(b"%PDF-1.4 test report 2")

    try:
        art1 = verify_output_artifact("TASK-MULTI", f1, "report")
        art2 = verify_output_artifact("TASK-MULTI", f2, "report")
        assert art1.validation_status == "verified"
        assert art2.validation_status == "verified"

        # Multi-file auto-opening should open the parent directory once
        success, opened_paths = safe_auto_open_in_explorer([art1, art2], task_id="TASK-MULTI")
        assert success is True
        assert len(opened_paths) == 1, "Multiple files in same folder must open directory once"
        assert os.path.normpath(test_dir) in [os.path.normpath(p) for p in opened_paths]
        assert art1.opened_in_explorer is True
        assert art2.opened_in_explorer is True
        print(f"  -> Verified: 2 files in single directory opened folder once at: {opened_paths[0]}")
    finally:
        if os.path.exists(f1): os.remove(f1)
        if os.path.exists(f2): os.remove(f2)
        if os.path.exists(test_dir): os.rmdir(test_dir)


def test_7_ui_and_server_cleanliness():
    print("\n[TEST 7] Testing UI & Server Cleanliness (Zero Legacy QTO Button)...")
    
    # 1. HTML file checks
    html_path = r"C:\AIGRAV\aigrav_voice_hud.html"
    with open(html_path, "r", encoding="utf-8") as f:
        html = f.read()

    assert "excelActionContainer" not in html, "excelActionContainer must be completely removed"
    assert "BUKA EXCEL QTO RESMI" not in html, "BUKA EXCEL QTO RESMI button must be removed"
    assert "openExcelFile" not in html, "openExcelFile function must be removed"
    assert "data.excel_ready" not in html, "UI must not depend on excel_ready"
    assert "ARTIFAK OUTPUT TERVERIFIKASI" in html, "Rich universal artifact presentation must be present"
    print("  -> Verified: HTML contains zero static Excel button and includes universal artifact presentation.")

    # 2. Server file checks
    server_path = r"C:\AIGRAV\server.py"
    with open(server_path, "r", encoding="utf-8") as f:
        server_code = f.read()

    assert "/api/open-excel" not in server_code, "/api/open-excel must be removed"
    assert "total_verified_artifacts" in server_code, "server.py telemetry must report universal artifacts"
    print("  -> Verified: server.py contains zero /api/open-excel endpoint and exposes universal artifact telemetry.")


def run_all_tests():
    print("=" * 75)
    print("AIGRAV HOTFIX REGRESSION SUITE: UNIVERSAL ARTIFACT DELIVERY & AUTO-OPEN")
    print("=" * 75)
    test_1_artifact_verification_model()
    test_2_qto_execution_and_auto_open()
    test_3_idempotency_and_no_duplicate_popups()
    test_4_read_only_and_general_chat_isolation()
    test_5_canceled_task_isolation()
    test_6_multi_file_grouping_logic()
    test_7_ui_and_server_cleanliness()
    print("\n" + "=" * 75)
    print("ALL 7 TEST SUITES PASSED (100% SUCCESS)!")
    print("=" * 75)


if __name__ == "__main__":
    run_all_tests()
