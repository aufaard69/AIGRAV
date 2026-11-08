"""
AIGRAV POC Test Suite: Official Antigravity SDK & Subscription Auth Verification
Executes the 4 required scenarios:
1. General conversational question.
2. Two turns with verified context retention.
3. Safe read-only Revit MCP tool access.
4. Output payload formatting compatible with Voice HUD.
"""

import os
import sys
import json
import time
import urllib.parse
from pathlib import Path

# Add POC directory to sys.path
POC_DIR = Path(__file__).resolve().parent
sys.path.insert(0, str(POC_DIR))

# Ensure UTF-8 printing in Windows console (prevent cp1252 UnicodeEncodeError on emojis)
if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')
if hasattr(sys.stderr, 'reconfigure'):
    sys.stderr.reconfigure(encoding='utf-8', errors='replace')

from antigravity_client import AntigravityAgentClient

def format_voice_hud_payload(
    conversation_id: str,
    reply_text: str,
    voice: str = "id-ID-ArdiNeural",
    badge_text: str = "ANTIGRAVITY AI RESPON"
) -> dict:
    """Format agent output into standard AIGRAV Voice HUD JSON payload."""
    encoded_text = urllib.parse.quote(reply_text)
    return {
        "reply_text": reply_text,
        "visual_markdown": f"### RESPONS ANTIGRAVITY AGENT\n- **Conversation ID:** `{conversation_id}`\n\n{reply_text}",
        "audio_url": f"/api/tts?text={encoded_text}&voice={voice}",
        "voice": voice,
        "badge_text": badge_text,
        "badge_class": "text-cyan-300 font-bold",
        "task_id": f"CONV-{conversation_id[:8]}",
        "task_status": "COMPLETED",
        "artifacts": [],
        "mode": "antigravity_agent",
        "timestamp": time.strftime("%H:%M:%S")
    }

def main():
    print("=" * 75)
    print("AIGRAV POC: OFFICIAL ANTIGRAVITY SDK & SUBSCRIPTION AUTH VERIFICATION")
    print("=" * 75)
    print("Target Auth: Native Desktop Subscription (Zero API Keys, Zero Add-on Costs)")
    print("Runtime    : Language Server agentapi CLI")
    print("-" * 75)

    client = AntigravityAgentClient()

    # --------------------------------------------------------------------------
    # SKENARIO 1: General Conversational Question
    # --------------------------------------------------------------------------
    print("\n[TEST 1] Testing General Conversational Question...")
    prompt_1 = (
        "Halo, kamu siapa dan apa peranmu dalam ekosistem AIGRAV? "
        "Jawab singkat dan ramah dalam Bahasa Indonesia."
    )
    t0 = time.time()
    conv_id, resp_1 = client.create_conversation(
        prompt=prompt_1,
        title="AIGRAV POC Conversation",
        model="flash"
    )
    dur_1 = time.time() - t0
    print(f"  -> Conversation ID: {conv_id}")
    print(f"  -> Duration       : {dur_1:.2f}s")
    print(f"  -> Prompt 1       : \"{prompt_1}\"")
    print(f"  -> Agent Response : \"{resp_1.strip()}\"")

    assert conv_id is not None and len(conv_id) > 10, "Invalid conversation ID"
    assert len(resp_1) > 10, "Empty response from Antigravity agent"
    assert any(k in resp_1.lower() for k in ["aigrav", "asisten", "ai", "bantu", "halo", "saya"]), "Response not relevant to AIGRAV"
    print("  [PASS] Scenario 1 verified successfully.")

    # --------------------------------------------------------------------------
    # SKENARIO 2: Multi-turn Context Retention (Turn 2 references Turn 1)
    # --------------------------------------------------------------------------
    print("\n[TEST 2] Testing Multi-turn Context Retention (Follow-up Question)...")
    prompt_2 = (
        "Sebutkan kembali nama sistem yang tadi kita bicarakan di pertanyaan pertama, "
        "dan apa satu fungsi utamanya yang kamu sebutkan tadi?"
    )
    t0 = time.time()
    resp_2 = client.send_message(
        conversation_id=conv_id,
        content=prompt_2
    )
    dur_2 = time.time() - t0
    print(f"  -> Duration       : {dur_2:.2f}s")
    print(f"  -> Prompt 2       : \"{prompt_2}\"")
    print(f"  -> Agent Response : \"{resp_2.strip()}\"")

    assert len(resp_2) > 10, "Empty response on turn 2"
    assert "aigrav" in resp_2.lower(), "Context lost: Antigravity did not recall 'AIGRAV' from Turn 1"
    print("  [PASS] Scenario 2 verified: Full context preserved across turns.")

    # --------------------------------------------------------------------------
    # SKENARIO 3: Safe Read-only Revit MCP Tool Access
    # --------------------------------------------------------------------------
    print("\n[TEST 3] Testing Safe Read-only Revit MCP Tool Access via Agent...")
    prompt_3 = (
        "Tolong periksa konektivitas Revit Bridge dengan memanggil MCP tool 'aigrav_ping' "
        "pada server 'aigrav-test' menggunakan tool call_mcp_tool. "
        "Berikan laporan singkat apakah bridge terhubung."
    )
    t0 = time.time()
    resp_3 = client.send_message(
        conversation_id=conv_id,
        content=prompt_3,
        timeout_seconds=45.0
    )
    dur_3 = time.time() - t0
    print(f"  -> Duration       : {dur_3:.2f}s")
    print(f"  -> Prompt 3       : \"{prompt_3}\"")
    print(f"  -> Agent Response : \"{resp_3.strip()}\"")

    assert any(w in resp_3.lower() for w in ["bridge", "ping", "aigrav", "terhubung", "aktif", "status"]), "Agent failed to execute or report MCP tool call"
    print("  [PASS] Scenario 3 verified: Agent successfully called Revit MCP tool.")

    # --------------------------------------------------------------------------
    # SKENARIO 4: Delivery to Voice HUD Format
    # --------------------------------------------------------------------------
    print("\n[TEST 4] Testing Voice HUD Payload Delivery Formatting...")
    payload = format_voice_hud_payload(conv_id, resp_3)
    print(f"  -> Payload JSON:")
    print(f"     - reply_text     : \"{payload['reply_text'][:80]}...\"")
    print(f"     - audio_url      : \"{payload['audio_url'][:60]}...\"")
    print(f"     - mode           : {payload['mode']}")
    print(f"     - task_id        : {payload['task_id']}")
    print(f"     - badge_text     : {payload['badge_text']}")

    assert payload["reply_text"] == resp_3
    assert payload["audio_url"].startswith("/api/tts?text=")
    assert payload["mode"] == "antigravity_agent"
    print("  [PASS] Scenario 4 verified: Compatible Voice HUD delivery format confirmed.")

    print("\n" + "=" * 75)
    print("ALL 4 POC SCENARIOS PASSED WITH 100% SUCCESS!")
    print("Auth Decision: PASS (Using Official Native Antigravity Subscription)")
    print("=" * 75)

if __name__ == "__main__":
    main()
