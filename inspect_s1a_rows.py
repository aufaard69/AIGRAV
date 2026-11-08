import urllib.request, json

req = urllib.request.Request('http://127.0.0.1:8765/api/execute',
    data=json.dumps({'tool': 'aigrav_inspect_schedule_def', 'argument': '1546107'}).encode('utf-8'),
    headers={'Content-Type': 'application/json'})

try:
    with urllib.request.urlopen(req, timeout=10) as resp:
        data = json.loads(resp.read().decode('utf-8'))
        res = data.get('result', '')
        lines = res.splitlines()
        capturing = False
        for line in lines:
            if 'S1-A' in line:
                print(line)
except Exception as e:
    print('Error:', e)
