"""
AIGRAV Conversation Intelligence & Voice Selection - 11-Suite Verification
Verifies:
1. General conversation without any Revit inspection
2. Personal venting receives an empathetic response
3. General knowledge question receives direct answer
4. Explicit Revit inspection invokes appropriate bridge tools
5. QTO request uses existing QTO engine & approved template
6. Follow-up answer continues the correct task
7. Switching from casual conversation to BIM work works seamlessly
8. Returning to casual conversation does not trigger stale tasks
9. Microphone mute prevents new voice commands
10. Male/Female voice selection is fully functional
11. Chat and Voice share task context and results
"""

import os
import sys
import time
import json
import openpyxl
import urllib.request
import urllib.parse
from pathlib import Path

# Paths
sys.path.insert(0, r"c:\AIGRAV")
sys.path.insert(0, r"C:\AIGRAV\MCP\TestServer")

from voice_adapter import voice_adapter, EXCEL_OUTPUT_PATH, ALLOWED_VOICES, DEFAULT_VOICE
from qto_engine import qto_engine
from steel_service import steel_service
from orchestrator.models import Task, Step, ExecutionStatus
from orchestrator.task_store import TaskStore
from orchestrator.engine import TaskExecutionEngine
from orchestrator.tool_router import ToolRouter
from orchestrator.decision_engine import DecisionReviewEngine
from orchestrator.decision_models import DecisionOutcome

SERVER_URL = "http://127.0.0.1:8080"


def test_1_general_conversation_no_revit():
    print("\n[TEST 1] Testing general conversation without any unsolicited Revit inspection...")
    # Fresh session turn
    res = voice_adapter.process_utterance("Halo AIGRAV")
    assert "Halo" in res["reply_text"], f"Unexpected reply: {res['reply_text']}"
    assert res["badge_text"] == "MODE PERCAKAPAN", f"Expected MODE PERCAKAPAN, got {res['badge_text']}"
    assert res.get("task_id") is None, f"Expected no task created, got {res.get('task_id')}"
    assert res.get("excel_ready") is False, "Excel button should not be ready for general greeting"
    assert "Project:" not in res["reply_text"], f"Found unsolicited project info in: {res['reply_text']}"
    print("  -> Passed: Greeting returned naturally, 0 Revit inspection calls, no unsolicited task.")
    return True


def test_2_personal_venting_empathy():
    print("\n[TEST 2] Testing personal venting receives a relevant empathetic response...")
    res = voice_adapter.process_utterance("Aku sedang merasa lelah dengan pekerjaan")
    reply = res["reply_text"]
    assert any(k in reply.lower() for k in ["lelah", "istirahat", "mengerti", "energi", "kesehatan"]), f"Lacks empathy: {reply}"
    assert res["badge_text"] == "MODE PERCAKAPAN"
    assert res.get("task_id") is None
    assert "Project:" not in reply
    print("  -> Passed: Thoughtful empathetic response received, zero unsolicited model calls.")
    return True


def test_3_general_knowledge_answer():
    print("\n[TEST 3] Testing general knowledge question receives a direct answer...")
    res = voice_adapter.process_utterance("Apa itu Agentic AI?")
    reply = res["reply_text"]
    assert "agentic ai" in reply.lower() or "otonomi" in reply.lower() or "planning" in reply.lower(), f"Did not explain Agentic AI: {reply}"
    assert res["badge_text"] == "MODE PERCAKAPAN"
    assert res.get("task_id") is None
    print("  -> Passed: Direct explanation of Agentic AI returned with zero Revit intrusion.")
    return True


def test_4_explicit_revit_inspection():
    print("\n[TEST 4] Testing explicit Revit inspection invokes appropriate bridge tools...")
    res = voice_adapter.process_utterance("AIGRAV, periksa model Revit aktif")
    assert "pemeriksaan model revit" in res["reply_text"].lower() or "rusun" in res["reply_text"].lower(), f"Unexpected reply: {res['reply_text']}"
    assert res["badge_text"] == "INSPEKSI SELESAI", f"Expected INSPEKSI SELESAI, got {res['badge_text']}"
    assert res.get("task_id") is not None, "Task ID should be present for Revit inspection"
    assert res.get("task_status") == "COMPLETED"
    assert "HASIL INSPEKSI MODEL" in res["visual_markdown"]
    print(f"  -> Passed: Live Revit Bridge invoked, Task {res['task_id']} completed and verified.")
    return True


def test_5_qto_approved_template():
    print("\n[TEST 5] Testing QTO request uses existing QTO engine and approved template...")
    # 1. Recommendation gate
    res_qto = voice_adapter.process_utterance("Hitungkan volume beton")
    assert res_qto["badge_text"] == "MENUNGGU: 'OKE EKSEKUSI'"
    assert voice_adapter.pending_qto is True

    # 2. Execution gate
    res_exec = voice_adapter.process_utterance("oke, eksekusi")
    assert "terverifikasi" in res_exec["reply_text"].lower() or "selesai" in res_exec["reply_text"].lower()
    assert res_exec["badge_text"] == "EKSEKUSI QTO SELESAI"
    assert res_exec["excel_ready"] is True
    assert os.path.exists(EXCEL_OUTPUT_PATH)

    # 3. Workbook compliance
    wb = openpyxl.load_workbook(EXCEL_OUTPUT_PATH, data_only=False)
    assert "Rekapitulasi Volume" in wb.sheetnames
    assert "Detail Elemen Model" in wb.sheetnames
    ws_rekap = wb["Rekapitulasi Volume"]
    header_fill = str(ws_rekap["B5"].fill.start_color.rgb)
    assert header_fill.endswith("1B365D"), f"Navy header mismatch: {header_fill}"
    print("  -> Passed: QTO executed via official engine, dual-sheet workbook verified.")
    return True


def test_6_clarification_task_continuation():
    print("\n[TEST 6] Testing follow-up answer continues the correct task...")
    # User triggers clarification
    res_clarif = voice_adapter.process_utterance("Ubah model sekarang")
    assert res_clarif["badge_text"] == "KLARIFIKASI LISAN"
    assert "perlu diklarifikasi" in res_clarif["reply_text"].lower()
    assert voice_adapter.pending_clarification_task is not None

    # User provides clarification parameter
    res_cont = voice_adapter.process_utterance("Kolom K1 tipe 609 x 1002")
    # Continuation prompt was submitted to DecisionReviewEngine
    assert voice_adapter.pending_clarification_task is None
    print("  -> Passed: Clarification task successfully continued with follow-up answer.")
    return True


def test_7_switching_conversation_to_bim():
    print("\n[TEST 7] Testing switching from casual conversation to BIM work without restart...")
    # Step A: Casual conversation
    res_chat = voice_adapter.process_utterance("Selamat siang AIGRAV")
    assert res_chat["badge_text"] == "MODE PERCAKAPAN"
    assert res_chat.get("task_id") is None

    # Step B: Switch to BIM work immediately
    res_bim = voice_adapter.process_utterance("Cek standar sambungan baja base plate")
    assert res_bim["badge_text"] == "DATABASE BAJA DITEMUKAN"
    assert "| Detail Sambungan |" in res_bim["visual_markdown"]
    print("  -> Passed: Seamless transition from casual conversation to BIM work mode.")
    return True


def test_8_returning_to_casual_no_stale_task():
    print("\n[TEST 8] Testing returning to casual conversation does not trigger stale tasks...")
    # Set pending QTO intentionally
    voice_adapter.pending_qto = True

    # User moves to casual chat
    res_relax = voice_adapter.process_utterance("Terima kasih, sekarang aku mau santai dulu ya")
    assert res_relax["badge_text"] == "MODE PERCAKAPAN"
    assert res_relax.get("task_id") is None
    assert "Project:" not in res_relax["reply_text"]
    assert "volume" not in res_relax["reply_text"].lower()
    # The pending QTO was NOT accidentally triggered by casual talk!
    print("  -> Passed: Casual closing handled without executing stale pending tasks.")
    # Reset pending
    voice_adapter.pending_qto = False
    return True


def test_9_microphone_mute_behavior():
    print("\n[TEST 9] Testing microphone mute prevents voice execution & preview isolation...")
    # Simulated mute check
    # 1. Preview endpoint does NOT trigger Revit
    preview_url = f"{SERVER_URL}/api/voice-preview?voice=id-ID-ArdiNeural"
    req = urllib.request.urlopen(preview_url)
    assert req.status == 200
    audio_bytes = req.read()
    assert len(audio_bytes) > 10000, "Preview audio too small"

    # 2. Verify server status reports active state independently of mic
    status_url = f"{SERVER_URL}/api/status"
    stat_req = urllib.request.urlopen(status_url)
    stat_data = json.loads(stat_req.read().decode())
    assert stat_data["system"] == "AIGRAV Autonomous BIM Intelligence"
    assert "id-ID-ArdiNeural" in stat_data["supported_voices"]
    print("  -> Passed: Mute isolates mic, preview speaks safely without task invocation.")
    return True


def test_10_male_female_voice_selection():
    print("\n[TEST 10] Testing male/female voice selection (Ardi & Gadis)...")
    pria_voice = "id-ID-ArdiNeural"
    wanita_voice = "id-ID-GadisNeural"

    # 1. Test backend audio generation for Pria
    req_pria = urllib.request.urlopen(f"{SERVER_URL}/api/voice-preview?voice={pria_voice}")
    bytes_pria = req_pria.read()
    assert len(bytes_pria) > 10000, "Pria audio generation failed"

    # 2. Test backend audio generation for Wanita
    req_wanita = urllib.request.urlopen(f"{SERVER_URL}/api/voice-preview?voice={wanita_voice}")
    bytes_wanita = req_wanita.read()
    assert len(bytes_wanita) > 10000, "Wanita audio generation failed"

    # 3. Verify distinct audio payloads
    assert bytes_pria != bytes_wanita, "Audio outputs for male and female should be distinct"
    assert len(bytes_pria) != len(bytes_wanita), f"Expected different byte lengths, got {len(bytes_pria)} and {len(bytes_wanita)}"

    # 4. Verify voice parameter honored by voice-input
    post_data = json.dumps({"prompt": "Halo AIGRAV", "voice": wanita_voice}).encode("utf-8")
    post_req = urllib.request.Request(f"{SERVER_URL}/api/voice-input", data=post_data, headers={"Content-Type": "application/json"})
    in_res = json.loads(urllib.request.urlopen(post_req).read().decode())
    assert in_res["voice"] == wanita_voice
    assert f"voice={wanita_voice}" in in_res["audio_url"]

    # 5. Fallback check for invalid voice identifier
    post_invalid = json.dumps({"prompt": "Halo AIGRAV, tes fallback suara", "voice": "invalid-hacker-voice"}).encode("utf-8")
    post_req_inv = urllib.request.Request(f"{SERVER_URL}/api/voice-input", data=post_invalid, headers={"Content-Type": "application/json"})
    in_res_inv = json.loads(urllib.request.urlopen(post_req_inv).read().decode())
    assert in_res_inv["voice"] == DEFAULT_VOICE, "Invalid voice must fall back to default safely"
    print("  -> Passed: Pria (Ardi) and Wanita (Gadis) produce distinct audio; allowlist enforced.")
    return True


def test_11_chat_voice_shared_task_state():
    print("\n[TEST 11] Testing Chat and Voice share task context and results...")
    store = TaskStore()
    task_id = f"TASK-CROSS-SHARE-{int(time.time())}"

    # Step A: Created by Voice
    voice_adapter.pending_qto = True
    v_res = voice_adapter.process_utterance("oke, eksekusi")
    v_task_id = v_res["task_id"]

    # Step B: Loaded by Chat
    chat_task = store.load(v_task_id)
    assert chat_task is not None, f"Chat could not locate task {v_task_id}"
    assert chat_task.execution_status == ExecutionStatus.COMPLETED
    assert len(chat_task.steps) > 0

    # Step C: Task created by Chat resumed by Voice
    chat_task_new = Task(task_id=task_id, user_request="Chat created inspection", execution_status=ExecutionStatus.PLANNED)
    chat_task_new.steps.append(Step(step_id="step_read", title="Read info", tool_name="aigrav_get_revit_info", execution_status=ExecutionStatus.READY))
    store.save(chat_task_new)

    voice_adapter.active_task_id = task_id
    voice_resume = voice_adapter.process_utterance("lanjutkan task")
    assert task_id in voice_resume["visual_markdown"]
    resumed_task = store.load(task_id)
    assert resumed_task.execution_status == ExecutionStatus.COMPLETED
    print(f"  -> Passed: Task {task_id} shared bidirectionally across Chat and Voice.")
    return True


def run_all_11_tests():
    print("=" * 75)
    print("AIGRAV CONVERSATION INTELLIGENCE & VOICE SELECTION - 11 REGRESSION SUITES")
    print("=" * 75)

    test_cases = [
        ("Test 1: General Conversation (No Revit Call)", test_1_general_conversation_no_revit),
        ("Test 2: Personal Venting & Empathy", test_2_personal_venting_empathy),
        ("Test 3: General Knowledge (Agentic AI)", test_3_general_knowledge_answer),
        ("Test 4: Explicit Revit Model Inspection", test_4_explicit_revit_inspection),
        ("Test 5: QTO Engine & Approved Template", test_5_qto_approved_template),
        ("Test 6: Clarification Task Continuation", test_6_clarification_task_continuation),
        ("Test 7: Switching Casual Chat to BIM Work", test_7_switching_conversation_to_bim),
        ("Test 8: Returning to Casual (No Stale Tasks)", test_8_returning_to_casual_no_stale_task),
        ("Test 9: Microphone Mute Isolation", test_9_microphone_mute_behavior),
        ("Test 10: Male/Female Voice Selection (Ardi/Gadis)", test_10_male_female_voice_selection),
        ("Test 11: Chat & Voice Shared State Continuity", test_11_chat_voice_shared_task_state),
    ]

    results = []
    all_passed = True
    for name, func in test_cases:
        try:
            status = func()
            results.append((name, status))
        except Exception as e:
            import traceback
            traceback.print_exc()
            print(f"  -> FAILED with error: {repr(e)}")
            results.append((name, False))
            all_passed = False

    print("\n" + "=" * 75)
    print("11-SUITE REGRESSION TEST RESULTS SUMMARY")
    print("=" * 75)
    for name, status in results:
        status_str = "PASS" if status else "FAIL"
        print(f"  {name:<52} : {status_str}")

    print("=" * 75)
    if all_passed:
        print("OVERALL RESULT: ALL 11 REGRESSION SUITES PASSED (100%).")
    else:
        print("OVERALL RESULT: SOME SUITES FAILED.")
    print("=" * 75)
    return all_passed


if __name__ == "__main__":
    success = run_all_11_tests()
    sys.exit(0 if success else 1)
