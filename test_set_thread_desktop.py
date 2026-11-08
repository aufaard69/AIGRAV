import ctypes
from ctypes import wintypes
import threading

user32 = ctypes.windll.user32
kernel32 = ctypes.windll.kernel32

h_default = user32.OpenDesktopW('Default', 0, False, 0x0100 | 0x0040 | 0x0001 | 0x0002 | 0x0004 | 0x0008 | 0x0010 | 0x0020)
print("h_default handle:", h_default)

result = {}

def thread_func():
    try:
        ok = user32.SetThreadDesktop(h_default)
        result["set_desktop"] = ok
        result["error"] = ctypes.GetLastError() if not ok else 0
        if ok:
            # Try to find HWND 67210
            is_wnd = user32.IsWindow(67210)
            result["is_window"] = is_wnd
    except Exception as e:
        result["exception"] = str(e)

t = threading.Thread(target=thread_func)
t.start()
t.join()

print("Thread execution result:", result)
