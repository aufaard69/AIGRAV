import urllib.request
import json
import sys
from collections import defaultdict

# Force UTF-8 output
sys.stdout.reconfigure(encoding='utf-8')

def check_type_marks():
    req = urllib.request.Request(
        'http://127.0.0.1:8765/',
        data=json.dumps({'tool': 'get_family_types'}).encode('utf-8'),
        headers={'Content-Type': 'application/json'},
        method='POST'
    )
    with urllib.request.urlopen(req) as res:
        type_lines = [l for l in json.loads(res.read().decode('utf-8'))['Result'].splitlines() if l.strip().startswith('- Id:')]

    duplicate_marks = defaultdict(list)
    type_details = []

    for line in type_lines:
        parts = line.split('|')
        tid = parts[0].replace('- Id:', '').strip()
        fam_and_type = parts[1].strip()

        req_p = urllib.request.Request(
            'http://127.0.0.1:8765/',
            data=json.dumps({'tool': 'get_element_parameters', 'argument': tid}).encode('utf-8'),
            headers={'Content-Type': 'application/json'},
            method='POST'
        )
        with urllib.request.urlopen(req_p) as res_p:
            ptxt = json.loads(res_p.read().decode('utf-8'))['Result']

        tmark = ''
        for pl in ptxt.splitlines():
            if pl.startswith('Type Mark = ') and pl.split('=', 1)[1].strip():
                tmark = pl.split('=', 1)[1].strip()
                break

        if tmark:
            duplicate_marks[tmark].append((tid, fam_and_type))
            type_details.append((tid, fam_and_type, tmark))

    print("=== DAFTAR NOTASI TYPE MARK TERDAFTAR ===")
    for tid, name, tm in type_details:
        print(f"ID {tid:8} | Type Mark: {tm:15} | {name}")

    print("\n=== TEMUAN INKONSISTENSI & DUPLICATE TYPE MARK ===")
    for tm, items in duplicate_marks.items():
        if len(items) > 1:
            print(f"Type Mark '{tm}' DUPLIKAT pada {len(items)} tipe:")
            for tid, name in items:
                print(f"  - ID: {tid} | {name}")

if __name__ == '__main__':
    check_type_marks()
