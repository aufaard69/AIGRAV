"""
Isolated unit test for AntigravityVoiceAdapter mechanics
"""

import sys
from pathlib import Path

# UTF-8 console output
if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')

from antigravity_voice_adapter import AntigravityVoiceAdapter, clean_text_for_speech

def test_clean_text():
    raw = "### Laporan Model Revit\n- Elemen **Balok**: `B1` (Panjang: 6000 mm)\n1. Cek parameter\n```json\n{\"id\": 123}\n```\nSelesai."
    cleaned = clean_text_for_speech(raw)
    print("RAW:\n", raw)
    print("CLEANED:\n", cleaned)
    assert "###" not in cleaned, "Markdown header not cleaned"
    assert "**" not in cleaned, "Bold marker not cleaned"
    assert "`" not in cleaned, "Backtick not cleaned"
    assert "json" not in cleaned, "Codeblock not cleaned"
    assert "Balok" in cleaned, "Content missing"
    print("[PASS] clean_text_for_speech")

def test_session_mechanics():
    adapter = AntigravityVoiceAdapter()
    assert adapter.active_conversation_id is None
    assert adapter._should_reset_session("tolong reset sesi sekarang") is True
    assert adapter._should_reset_session("halo selamat pagi") is False
    print("[PASS] session mechanics")

if __name__ == "__main__":
    test_clean_text()
    test_session_mechanics()
    print("All isolated unit checks passed!")
