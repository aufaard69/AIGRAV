import urllib.request
import json

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

# Ordered list of 21 sheets under Sheet1: 01. SI-18
# Placing LAYOUT MEJA BETON right before DETAIL MEJA BETON
ordered_si18_sheets = [
    (4406526, 'LAYOUT PACKAGING POWDER'),
    (4415245, 'DENAH PILOT PLANT, WORKSOP & REPACK FG'),
    (4565324, 'POTONGAN A - B'),
    (4584254, 'POTONGAN C - D'),
    (4604080, 'DENAH FINISH LANTAI'),
    (4915827, 'LAYOUT MEJA BETON'),                         # Newly added sheet
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
    (4911393, 'DETAIL OPENING PASSBOX'),
    (4685463, 'DETAIL BAK PIT'),
    (4658886, 'DETAIL STANDAR DINDING'),
    (4908688, 'DETAIL WASTAFEL')
]

# Sheets under BANGUNAN UTAMA to shift after GU-21
bangunan_utama = [
    (4361806, 22), # GU-22
    (4300179, 23), # GU-23
    (4303715, 24), # GU-24
    (4622658, 25)  # GU-25
]

print("Step 1: Set temporary sheet numbers...")
for eid, _ in ordered_si18_sheets:
    call_set_param(eid, 'Sheet Number', f'TEMP-SI-{eid}')

for eid, _ in bangunan_utama:
    call_set_param(eid, 'Sheet Number', f'TEMP-BU-{eid}')

print("Step 2: Assign final Sheet Numbers (GU-01 to GU-21) and NO (1 to 21)...")
for idx, (eid, name) in enumerate(ordered_si18_sheets, 1):
    num_str = f"GU-{idx:02d}"
    call_set_param(eid, 'Sheet Number', num_str)
    call_set_param(eid, 'NO', str(idx))
    print(f"[{num_str}] (NO: {idx}) - {name} (ID: {eid})")

print("Step 3: Assign shifted Sheet Numbers (GU-22 to GU-25) and NO for BANGUNAN UTAMA...")
for eid, new_no in bangunan_utama:
    num_str = f"GU-{new_no:02d}"
    call_set_param(eid, 'Sheet Number', num_str)
    call_set_param(eid, 'NO', str(new_no))
    print(f"[{num_str}] (NO: {new_no}) - (ID: {eid})")

print("All sheets successfully reordered!")
