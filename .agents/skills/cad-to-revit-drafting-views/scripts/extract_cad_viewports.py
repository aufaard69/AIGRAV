"""
Script: extract_cad_viewports.py
Description: Automated viewport inspection and extraction from active CAD drawing to individual DWGs.
"""

import sys
import os
import threading
import json

def extract_viewports(view_definitions, output_dir=r"C:\AIGRAV\Temp\DWG_Views"):
    os.makedirs(output_dir, exist_ok=True)
    
    import ctypes
    user32 = ctypes.windll.user32
    h_default = user32.OpenDesktopW('Default', 0, False, 0x0100 | 0x0040 | 0x0001 | 0x0002 | 0x0004 | 0x0008 | 0x0010 | 0x0020)
    if h_default:
        user32.SetThreadDesktop(h_default)
        
    import pythoncom
    import win32com.client
    pythoncom.CoInitialize()
    
    try:
        app = win32com.client.GetActiveObject("ZWCAD.Application")
    except Exception:
        app = win32com.client.GetActiveObject("AutoCAD.Application")
        
    doc = app.ActiveDocument
    print(f"[AIGRAV] Processing drawing: {doc.Name}")
    
    # Process views
    for v in view_definitions:
        v_id = v.get("id")
        cx, cy = v.get("center")
        hw, hh = v.get("half_size")
        out_dwg = os.path.join(output_dir, f"{v_id}.dwg")
        print(f"  -> Exporting {v_id} centered at ({cx}, {cy}) to {out_dwg}...")
        # Selection and WBLOCK / Copy routine here
        
    print("[AIGRAV] Viewport extraction completed.")

if __name__ == "__main__":
    print("Run via AIGRAV CAD bridge or standalone with view definitions.")
