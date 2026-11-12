import ctypes
from ctypes import wintypes
import time

# Let's inspect Antigravity window with UIAutomation
user32 = ctypes.windll.user32
ole32 = ctypes.windll.ole32

ole32.CoInitialize(None)

# Find Antigravity HWND on Default desktop
h_default = user32.OpenDesktopW('Default', 0, False, 0x0040 | 0x0001 | 0x0002)

antigravity_hwnds = []
def proc(hwnd, lparam):
    if user32.IsWindowVisible(hwnd):
        pid = wintypes.DWORD()
        user32.GetWindowThreadProcessId(hwnd, ctypes.byref(pid))
        if pid.value == 7664:
            antigravity_hwnds.append(hwnd)
    return True

cb = ctypes.WINFUNCTYPE(ctypes.c_bool, ctypes.c_void_p, ctypes.c_void_p)(proc)
user32.EnumDesktopWindows(h_default, cb, 0)

print(f"Found {len(antigravity_hwnds)} HWNDs for Antigravity (PID 7664): {antigravity_hwnds}")

# Check titles and classes of all HWNDs
for h in antigravity_hwnds:
    length = user32.GetWindowTextLengthW(h)
    buff = ctypes.create_unicode_buffer(length + 1)
    user32.GetWindowTextW(h, buff, length + 1)
    class_buff = ctypes.create_unicode_buffer(256)
    user32.GetClassNameW(h, class_buff, 256)
    print(f"HWND {h}: Class='{class_buff.value}', Title='{buff.value}'")
