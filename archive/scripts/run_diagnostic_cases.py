import sys
import os
import json

sys.path.insert(0, r"C:\AIGRAV")
sys.path.insert(0, r"C:\AIGRAV\MCP\TestServer")

from voice_adapter import voice_adapter

cases = [
    ("Case A", "Hitung volume pondasi saja."),
    ("Case B", "Buatkan 10 sheet baru dengan standar Detail Engineering Design."),
    ("Case C", "Tolong jelaskan parameter yang tadi kamu tanyakan."),
    ("Case D", "Apa kabar hari ini?")
]

results = []
for name, text in cases:
    print(f"\n=======================================================")
    print(f"RUNNING {name}: '{text}'")
    print(f"=======================================================")
    
    # State before
    prev_task = voice_adapter.active_task_id
    prev_pending_qto = getattr(voice_adapter, "pending_qto", False)
    
    res = voice_adapter.process_utterance(text)
    
    # State after
    curr_task = voice_adapter.active_task_id
    curr_pending_qto = getattr(voice_adapter, "pending_qto", False)
    
    print(f"Raw Input: '{text}'")
    print(f"Active Task Before: {prev_task}")
    print(f"Active Task After : {curr_task}")
    print(f"Pending QTO State : {curr_pending_qto}")
    print(f"Badge Text        : {res.get('badge_text')}")
    print(f"Mode              : {res.get('mode')}")
    print(f"Reply Text        : {res.get('reply_text')}")
    print(f"Visual Markdown   : {res.get('visual_markdown')[:100]}...")
    print(f"Artifacts         : {res.get('artifacts')}")
    print(f"Excel Ready Flag  : {res.get('excel_ready')}")
    print(f"Auto-Opened Flag  : {res.get('auto_opened')}")
