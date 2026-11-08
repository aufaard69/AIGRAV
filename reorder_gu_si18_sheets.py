import urllib.request
import json
import time

def call_set_param(eid, param_name, value):
    payload = {
        'tool': 'set_parameter',
        'argument': json.dumps({
            'elementId': eid,
            'parameterName': param_name,
            'value': str(value)
        })
    }
    req = urllib.request.Request(
        'http://127.0.0.1:8765/',
        data=json.dumps(payload).encode('utf-8'),
        headers={'Content-Type': 'application/json'},
        method='POST'
    )
    with urllib.request.urlopen(req) as res:
        return json.loads(res.read().decode('utf-8'))

# Ordered list of sheets based on the table
ordered_sheets = [
    (4406526, 'LAYOUT PACKAGING POWDER'),
    (4415245, 'DENAH PILOT PLANT, WORKSOP & REPACK FG'),
    (4565324, 'POTONGAN A - B'),
    (4584254, 'POTONGAN C - D'),
    (4604080, 'DENAH FINISH LANTAI'),
    (4604086, 'DETAIL MEJA BETON'),
    (4584867, 'DETAIL RANGKA PLAFOND'),
    (4902797, 'DETAIL RANGKA PLAFOND (1)'),
    (4904179, 'DETAIL RANGKA PLAFOND (2)'),
    (3526932, 'DENAH DINDING & KUSEN BANGUNAN UTAMA'),
    (4906216, 'DENAH DINDING DAN KUSEN PILOT PLANT, WORKSOP & REPACK FG'),
    (3541963, 'DETAIL KUSEN #1'),
    (4642211, 'DETAIL KUSEN #2'),
    (4653057, 'DETAIL OPENING RD-M 1'),
    (4653064, 'DETAIL OPENING RD-M 2'),
    (4653071, 'DETAIL OPENING RD-M 3'),
    (4685463, 'DETAIL BAK PIT'),
    (4658886, 'DETAIL STANDAR DINDING'),
    (4908688, 'DETAIL WASTAFEL')
]

print("Step 1: Setting temporary Sheet Numbers...")
for i, (eid, name) in enumerate(ordered_sheets, 1):
    temp_num = f"TEMP-GU-{i:02d}"
    call_set_param(eid, 'Sheet Number', temp_num)

print("Step 2: Setting sequential Sheet Numbers (GU-01 to GU-19) and NO (1 to 19)...")
for i, (eid, name) in enumerate(ordered_sheets, 1):
    final_num = f"GU-{i:02d}"
    call_set_param(eid, 'Sheet Number', final_num)
    call_set_param(eid, 'NO', str(i))
    print(f"Updated: {final_num} | NO: {i} | {name} (ID: {eid})")

print("All 19 sheets updated successfully!")
