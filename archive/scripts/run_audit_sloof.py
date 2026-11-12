import json
import urllib.request

req_body = {
    "tool": "aigrav_audit_sloof_floor_overlap",
    "argument": ""
}

req = urllib.request.Request(
    'http://127.0.0.1:8765/api/execute',
    data=json.dumps(req_body).encode('utf-8'),
    headers={'Content-Type': 'application/json'}
)

try:
    with urllib.request.urlopen(req, timeout=30) as resp:
        res = json.loads(resp.read().decode('utf-8'))
        print(res.get('result', '')[:2500])
except Exception as e:
    print("Error:", e)
