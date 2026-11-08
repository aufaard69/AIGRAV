import pythoncom
import win32com.client
from collections import Counter

pythoncom.CoInitialize()
try:
    app = win32com.client.GetActiveObject("ZWCAD.Application")
    doc = app.ActiveDocument
    print("Document:", doc.Name)
    print("Path:", doc.FullName)
    
    ms = doc.ModelSpace
    total = ms.Count
    print("Total ModelSpace entities:", total)
    
    types = Counter()
    layers = Counter()
    texts = []
    blocks = Counter()
    
    # Sample or iterate
    limit = min(total, 5000)
    for i in range(limit):
        try:
            ent = ms.Item(i)
            oname = ent.ObjectName
            layer = ent.Layer
            types[oname] += 1
            layers[layer] += 1
            if "Text" in oname:
                txt = getattr(ent, "TextString", "")
                if txt and txt not in texts:
                    texts.append(txt)
            elif oname == "AcDbBlockReference":
                bname = getattr(ent, "EffectiveName", getattr(ent, "Name", ""))
                blocks[bname] += 1
        except Exception:
            pass
            
    print("Types in first", limit, ":", dict(types))
    print("Layers in first", limit, ":", dict(layers))
    print("Block references:", dict(blocks))
    print("Texts found:", texts[:30])
finally:
    pythoncom.CoUninitialize()
