import sys
import os
import threading
import json

sys.path.insert(0, r"C:\ZWCAD-MCP")

VIEW_DEFINITIONS = [
    # Sheet 1: JL-048
    {
        "id": "JL048_V1_DetailPondasi",
        "sheet": "JL-048",
        "name": "DETAIL PONDASI - PETIR T1",
        "scale": 20,
        "center": (593276, 216092),
        "half_size": (1500, 2700),
        "paper_pos_mm": (84.9, 144.9)
    },
    {
        "id": "JL048_V2_DenahPondasi",
        "sheet": "JL-048",
        "name": "DENAH PONDASI - PETIR T1",
        "scale": 20,
        "center": (596103, 214650),
        "half_size": (1100, 950),
        "paper_pos_mm": (230.8, 192.1)
    },
    {
        "id": "JL048_V3_DetailAngkur",
        "sheet": "JL-048",
        "name": "DETAIL ANGKUR - PETIR T1",
        "scale": 10,
        "center": (597408, 214728),
        "half_size": (200, 500),
        "paper_pos_mm": (314.0, 196.9)
    },
    {
        "id": "JL048_V4_DenahSituasi",
        "sheet": "JL-048",
        "name": "DENAH PONDASI PENANGKAL PETIR (TIPE 1 - 20 M)",
        "scale": 50,
        "center": (132624, 82656),
        "half_size": (3900, 1750),
        "paper_pos_mm": (246.5, 90.6)
    },

    # Sheet 2: JL-049
    {
        "id": "JL049_V1_TampakPetir",
        "sheet": "JL-049",
        "name": "TAMPAK PENANGKAL PETIR T1 - 20M",
        "scale": 100,
        "center": (605062, 225440),
        "half_size": (5200, 11800),
        "paper_pos_mm": (79.2, 134.5)
    },
    {
        "id": "JL049_V2_Detail1",
        "sheet": "JL-049",
        "name": "DETAIL 1 SAMBUNGAN TIANG - T1",
        "scale": 20,
        "center": (611601, 216887),
        "half_size": (700, 1400),
        "paper_pos_mm": (169.8, 103.3)
    },

    # Sheet 3: JL-050
    {
        "id": "JL050_V1_DetailPondasi",
        "sheet": "JL-050",
        "name": "DETAIL PONDASI - PETIR T2",
        "scale": 20,
        "center": (627049, 216140),
        "half_size": (1500, 2700),
        "paper_pos_mm": (84.3, 147.4)
    },
    {
        "id": "JL050_V2_DenahPondasi",
        "sheet": "JL-050",
        "name": "DENAH PONDASI - PETIR T2",
        "scale": 20,
        "center": (629794, 214662),
        "half_size": (1100, 1000),
        "paper_pos_mm": (221.8, 191.4)
    },
    {
        "id": "JL050_V3_DetailAngkur",
        "sheet": "JL-050",
        "name": "DETAIL ANGKUR - PETIR T2",
        "scale": 10,
        "center": (631051, 214736),
        "half_size": (200, 500),
        "paper_pos_mm": (307.3, 186.3)
    },
    {
        "id": "JL050_V4_DenahSituasi",
        "sheet": "JL-050",
        "name": "DENAH PONDASI PENANGKAL PETIR (TIPE 2 - 25 M)",
        "scale": 50,
        "center": (384181, 60245),
        "half_size": (3900, 1750),
        "paper_pos_mm": (246.3, 90.9)
    },

    # Sheet 4: JL-051
    {
        "id": "JL051_V1_TampakPetir",
        "sheet": "JL-051",
        "name": "TAMPAK PENANGKAL PETIR T2 - 25M",
        "scale": 100,
        "center": (637772, 227545),
        "half_size": (5000, 14000),
        "paper_pos_mm": (79.8, 155.5)
    },
    {
        "id": "JL051_V2_Detail1",
        "sheet": "JL-051",
        "name": "DETAIL 1 SAMBUNGAN TIANG - T2",
        "scale": 20,
        "center": (611601, 216887),
        "half_size": (700, 1400),
        "paper_pos_mm": (179.7, 103.3)
    }
]

def worker():
    import ctypes
    user32 = ctypes.windll.user32
    h_default = user32.OpenDesktopW('Default', 0, False, 0x0100 | 0x0040 | 0x0001 | 0x0002 | 0x0004 | 0x0008 | 0x0010 | 0x0020)
    if h_default:
        user32.SetThreadDesktop(h_default)
        
    import pythoncom
    import win32com.client
    pythoncom.CoInitialize()
    
    app = win32com.client.GetActiveObject("ZWCAD.Application")
    orig_doc = app.ActiveDocument
    
    out_dir = r"C:\AIGRAV\Temp\DWG_Views"
    os.makedirs(out_dir, exist_ok=True)
    
    ms = orig_doc.ModelSpace
    print(f"Reading ModelSpace ({ms.Count} entities)...")
    
    # Cache entity bounding boxes to avoid repeating GetBoundingBox
    all_ents = []
    for i in range(ms.Count):
        try:
            e = ms.Item(i)
            min_pt, max_pt = e.GetBoundingBox()
            all_ents.append((e, min_pt, max_pt))
        except Exception:
            pass
            
    print(f"Cached {len(all_ents)} entities with bounding boxes.")
    
    exported_views = []
    
    for vdef in VIEW_DEFINITIONS:
        cx, cy = vdef["center"]
        hw, hh = vdef["half_size"]
        min_x, max_x = cx - hw, cx + hw
        min_y, max_y = cy - hh, cy + hh
        
        matching = []
        for e, min_pt, max_pt in all_ents:
            if not (max_pt[0] < min_x or min_pt[0] > max_x or max_pt[1] < min_y or min_pt[1] > max_y):
                matching.append(e)
                
        print(f"\nProcessing {vdef['id']} ({vdef['name']}): found {len(matching)} entities.")
        
        out_dwg = os.path.join(out_dir, f"{vdef['id']}.dwg")
        if os.path.exists(out_dwg):
            try: os.remove(out_dwg)
            except: pass
            
        if len(matching) == 0:
            continue
            
        new_doc = app.Documents.Add()
        new_ms = new_doc.ModelSpace
        
        obj_array = win32com.client.VARIANT(pythoncom.VT_ARRAY | pythoncom.VT_DISPATCH, matching)
        orig_doc.CopyObjects(obj_array, new_ms)
        
        # Center the entities at origin (0,0) in the new DWG
        pt_from = win32com.client.VARIANT(pythoncom.VT_ARRAY | pythoncom.VT_R8, (cx, cy, 0))
        pt_to = win32com.client.VARIANT(pythoncom.VT_ARRAY | pythoncom.VT_R8, (0, 0, 0))
        for i in range(new_ms.Count):
            try:
                new_ms.Item(i).Move(pt_from, pt_to)
            except Exception:
                pass
                
        new_doc.SaveAs(out_dwg)
        new_doc.Close(False)
        
        vdef_record = dict(vdef)
        vdef_record["file_path"] = out_dwg
        vdef_record["entity_count"] = len(matching)
        exported_views.append(vdef_record)
        print(f"  -> Exported to {out_dwg} ({len(matching)} entities)")
        
    with open(r"C:\AIGRAV\Temp\exported_views_manifest.json", "w", encoding="utf-8") as f:
        json.dump(exported_views, f, indent=2)
        
    print(f"\nALL {len(exported_views)} VIEWS EXPORTED SUCCESSFULLY!")

t = threading.Thread(target=worker)
t.start()
t.join()
