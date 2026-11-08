import urllib.request, json

req = urllib.request.Request('http://127.0.0.1:8765/api/execute',
    data=json.dumps({'tool': 'aigrav_audit_sloof_floor_overlap', 'argument': ''}).encode('utf-8'),
    headers={'Content-Type': 'application/json'})

try:
    with urllib.request.urlopen(req, timeout=10) as resp:
        data = json.loads(resp.read().decode('utf-8'))
        res = data.get('result', '')
        print(res[:2500])
except Exception as e:
    print('Error:', e)
