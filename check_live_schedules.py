import urllib.request, json

schedules_to_check = [
    ("C.3.1.3", "Waterline WF 400"),
    ("C.3.1.1", "Waterline WF 350"),
    ("C.3.3.1", "Carton & Cups H 250"),
    ("B.1.3", "Kolom WF 400 Bangunan Utama"),
    ("A.1.5", "Balok Sloof")
]

for tag, desc in schedules_to_check:
    req = urllib.request.Request('http://127.0.0.1:8765/api/execute',
        data=json.dumps({'tool': 'aigrav_inspect_schedule_def', 'argument': tag}).encode('utf-8'),
        headers={'Content-Type': 'application/json'})
    try:
        with urllib.request.urlopen(req, timeout=15) as resp:
            data = json.loads(resp.read().decode('utf-8'))
            res = data.get('result', '')
            lines = res.splitlines()
            print(f"=== {tag}: {desc} ===")
            for l in lines[:15]:
                print(" ", l)
    except Exception as e:
        print(f"Error {tag}:", e)
