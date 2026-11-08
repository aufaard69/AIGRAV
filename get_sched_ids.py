import urllib.request, json

req = urllib.request.Request('http://127.0.0.1:8765/api/execute',
    data=json.dumps({'tool': 'aigrav_list_all_schedules', 'argument': ''}).encode('utf-8'),
    headers={'Content-Type': 'application/json'})

try:
    with urllib.request.urlopen(req, timeout=10) as resp:
        data = json.loads(resp.read().decode('utf-8'))
        res = data.get('result', '')
        for line in res.splitlines():
            if any(k in line for k in ['C.3.1.3', 'C.3.3.1', 'B.1.3', 'A.1.5', 'A.1.8']):
                print(line)
except Exception as e:
    print('Error:', e)
