import urllib.request
import json

def get_matching_sheets():
    req = urllib.request.Request(
        'http://127.0.0.1:8765/',
        data=json.dumps({'tool': 'get_sheets'}).encode('utf-8'),
        headers={'Content-Type': 'application/json'},
        method='POST'
    )
    with urllib.request.urlopen(req) as res:
        sheet_lines = json.loads(res.read().decode('utf-8'))['Result'].splitlines()

    matching_sheets = []

    for line in sheet_lines[1:]:
        if line.startswith('- Id:'):
            parts = line.split('|')
            sid = parts[0].replace('- Id:', '').strip()
            name = parts[1].replace('Name:', '').strip()
            
            req_p = urllib.request.Request(
                'http://127.0.0.1:8765/',
                data=json.dumps({'tool': 'get_element_parameters', 'argument': sid}).encode('utf-8'),
                headers={'Content-Type': 'application/json'},
                method='POST'
            )
            with urllib.request.urlopen(req_p) as res_p:
                plines = json.loads(res_p.read().decode('utf-8'))['Result'].splitlines()
            
            snum, s1, scode, no = '', '', '', ''
            for pl in plines:
                if pl.startswith('Sheet Number = ') and pl.split('=', 1)[1].strip():
                    snum = pl.split('=', 1)[1].strip()
                elif pl.startswith('Sheet1 = ') and pl.split('=', 1)[1].strip():
                    s1 = pl.split('=', 1)[1].strip()
                elif pl.startswith('Sheet Code = ') and pl.split('=', 1)[1].strip():
                    scode = pl.split('=', 1)[1].strip()
                elif pl.startswith('NO = ') and pl.split('=', 1)[1].strip():
                    no = pl.split('=', 1)[1].strip()
                    
            if snum.startswith('GU') and 'SI-18' in s1:
                matching_sheets.append({
                    'id': sid,
                    'number': snum,
                    'name': name,
                    's1': s1,
                    'code': scode,
                    'no': no
                })

    matching_sheets.sort(key=lambda x: int(x['no']) if x['no'].isdigit() else 999)
    print(f'Total ditemukan: {len(matching_sheets)} sheet')
    for s in matching_sheets:
        print(f"[{s['number']}] (NO: {s['no']}) - {s['name']} | Sheet1: {s['s1']} | Code: {s['code']} (Element ID: {s['id']})")

if __name__ == '__main__':
    get_matching_sheets()
