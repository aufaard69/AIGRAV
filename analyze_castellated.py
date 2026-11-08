types = [
    {
        'name': 'RCWF.300',
        'd1': 300, 'bf1': 150, 'tf1': 9, 'tw1': 7,
        'd': 450, 'ho': 300, 'e1': 75, 'e2': 75,
        'h1': 150, 'dt1': 75, 'wo': 248, 'b': 87, 'Spacing': 323, 'phi1': 60.03
    },
    {
        'name': 'RCWF.350',
        'd1': 350, 'bf1': 175, 'tf1': 11, 'tw1': 7,
        'd': 525, 'ho': 350, 'e1': 88, 'e2': 88,
        'h1': 175, 'dt1': 88, 'wo': 290, 'b': 101, 'Spacing': 378, 'phi1': 60.01
    },
    {
        'name': 'RCWF.400',
        'd1': 400, 'bf1': 200, 'tf1': 13, 'tw1': 8,
        'd': 600, 'ho': 400, 'e1': 100, 'e2': 100,
        'h1': 200, 'dt1': 100, 'wo': 331, 'b': 116, 'Spacing': 431, 'phi1': 59.99
    }
]

for t in types:
    d1 = t['d1']
    d = t['d']
    ho = t['ho']
    e1 = t['e1']
    h1 = t['h1']
    dt1 = t['dt1']
    wo = t['wo']
    b = t['b']
    spacing = t['Spacing']
    phi = t['phi1']
    
    calc_d = d1 * 1.5
    calc_e = (d - ho) / 2
    calc_h = d1 / 2
    r_wo = wo / d1
    r_b = b / d1
    r_sp = spacing / d1
    print(f"{t['name']}: d={d} (d1*1.5={calc_d}), ho={ho}, e1={e1}, h1={h1}")
    print(f"  wo={wo} (wo/d1={r_wo:.4f}), b={b} (b/d1={r_b:.4f}), Spacing={spacing} (sp/d1={r_sp:.4f})")
    
    # Check opening slope height:
    # slope height = ho - 2 * (e - tf)? Or ho - e?
    # In standard castellated: the cut depth c = (d - d1) = 0.5 * d1 = 150 for 300!
    # slope horizontal run = c / tan(60) = 150 / 1.73205 = 86.6 mm!
    # wo = b + 2 * (c / tan(60)) = 87 + 2 * 86.6 = 260.2 (approx 248)
    # Spacing = wo + b = 248 + 87 = 335 (or Spacing parameter in family)
