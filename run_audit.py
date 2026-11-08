import urllib.request
import json
import sys

try:
    req = urllib.request.Request(
        'http://127.0.0.1:8765/',
        data=json.dumps({'tool': 'tag_wf400_endplate'}).encode('utf-8'),
        headers={'Content-Type': 'application/json'}
    )
    print("Sending tag_wf400_endplate audit request to Revit...")
    with urllib.request.urlopen(req, timeout=120) as resp:
        res_data = json.loads(resp.read().decode('utf-8'))
        result_text = res_data.get('Result', '')
        with open(r'C:\AIGRAV\audit_result.txt', 'w', encoding='utf-8') as out:
            out.write(result_text)
        print("Audit finished successfully. Length:", len(result_text))
        print("First 300 chars:")
        print(result_text[:300])
except Exception as ex:
    print("Error during audit:", ex)
    sys.exit(1)
