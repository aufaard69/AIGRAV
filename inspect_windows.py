import ctypes
from ctypes import wintypes
import os

user32 = ctypes.windll.user32

WNDENUMPROC = ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)

windows = []

def enum_windows_proc(hwnd, lparam):
    if user32.IsWindowVisible(hwnd):
        length = user32.GetWindowTextLengthW(hwnd)
        if length > 0:
            buff = ctypes.create_unicode_buffer(length + 1)
            user32.GetWindowTextW(hwnd, buff, length + 1)
            
            pid = wintypes.DWORD()
            user32.GetWindowThreadProcessId(hwnd, ctypes.byref(pid))
            
            class_buff = ctypes.create_unicode_buffer(256)
            user32.GetClassNameW(hwnd, class_buff, 256)
            
            title = buff.value
            class_name = class_buff.value
            
            windows.append({
                "hwnd": hwnd,
                "pid": pid.value,
                "title": title,
                "class_name": class_name
            })
    return True

cb = WNDENUMPROC(enum_windows_proc)
user32.EnumWindows(cb, 0)

print(f"Total visible windows with title: {len(windows)}")
for w in windows:
    # Filter interesting ones
    t_lower = w['title'].lower()
    c_lower = w['class_name'].lower()
    if any(k in t_lower or k in c_lower for k in ["antigravity", "gemini", "code", "aigrav", "revit", "chrome", "edge", "electron"]):
        print(f"PID: {w['pid']}, HWND: {w['hwnd']}, Class: '{w['class_name']}', Title: '{w['title']}'")
