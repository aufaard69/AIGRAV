import ctypes
from ctypes import wintypes

user32 = ctypes.windll.user32
kernel32 = ctypes.windll.kernel32

h_default = user32.OpenDesktopW('Default', 0, False, 0x0040 | 0x0001 | 0x0002)

windows = []
def proc(hwnd, lparam):
    if user32.IsWindowVisible(hwnd):
        length = user32.GetWindowTextLengthW(hwnd)
        if length > 0:
            buff = ctypes.create_unicode_buffer(length + 1)
            user32.GetWindowTextW(hwnd, buff, length + 1)
            
            pid = wintypes.DWORD()
            user32.GetWindowThreadProcessId(hwnd, ctypes.byref(pid))
            
            class_buff = ctypes.create_unicode_buffer(256)
            user32.GetClassNameW(hwnd, class_buff, 256)
            
            windows.append({
                'hwnd': hwnd,
                'pid': pid.value,
                'title': buff.value,
                'class_name': class_buff.value
            })
    return True

cb = ctypes.WINFUNCTYPE(ctypes.c_bool, ctypes.c_void_p, ctypes.c_void_p)(proc)
user32.EnumDesktopWindows(h_default, cb, 0)

print(f"Total visible windows on Default desktop: {len(windows)}")
for w in windows:
    t = w['title'].lower()
    c = w['class_name'].lower()
    if any(k in t or k in c for k in ['antigravity', 'revit', 'code', 'electron', 'chrome', 'edge', 'gemini']):
        print(f"PID: {w['pid']} | HWND: {w['hwnd']} | Class: '{w['class_name']}' | Title: '{w['title']}'")
