import ctypes
from ctypes import wintypes
import time

user32 = ctypes.windll.user32
kernel32 = ctypes.windll.kernel32

WNDENUMPROC = ctypes.WINFUNCTYPE(ctypes.c_bool, wintypes.HWND, wintypes.LPARAM)
DESKTOPENUMPROC = ctypes.WINFUNCTYPE(ctypes.c_bool, ctypes.c_wchar_p, ctypes.c_void_p)

h_winsta = user32.OpenWindowStationW('WinSta0', False, 0x037F)
if h_winsta:
    user32.SetProcessWindowStation(h_winsta)

desktops = []
def desk_cb(name, _):
    desktops.append(name)
    return True

if h_winsta:
    user32.EnumDesktopsW(h_winsta, DESKTOPENUMPROC(desk_cb), None)

if not desktops:
    desktops = ['Default']

print("Desktops:", desktops)

BM_CLICK = 0x00F5
WM_COMMAND = 0x0111

def get_text(hwnd):
    length = user32.GetWindowTextLengthW(hwnd)
    if length > 0:
        buf = ctypes.create_unicode_buffer(length + 1)
        user32.GetWindowTextW(hwnd, buf, length + 1)
        return buf.value
    return ""

def get_class(hwnd):
    buf = ctypes.create_unicode_buffer(256)
    user32.GetClassNameW(hwnd, buf, 256)
    return buf.value

for dname in desktops:
    hd = user32.OpenDesktopW(dname, 0, False, 0x01FF)
    if not hd:
        continue
    user32.SetThreadDesktop(hd)

    def cb(hwnd, _):
        cls = get_class(hwnd)
        txt = get_text(hwnd)
        pid = wintypes.DWORD()
        user32.GetWindowThreadProcessId(hwnd, ctypes.byref(pid))

        if cls == "#32770" and user32.IsWindowVisible(hwnd):
            print(f"Dialog on {dname} (PID {pid.value}): '{txt}'")
            def child_cb(c_hwnd, _):
                c_txt = get_text(c_hwnd)
                cid = user32.GetDlgCtrlID(c_hwnd)
                if cid == 1001 or "Always Load" in c_txt or "Load Once" in c_txt or "OK" in c_txt or "Cancel" in c_txt:
                    print(f"  Clicking control {cid} ('{c_txt}')")
                    user32.SendMessageW(c_hwnd, BM_CLICK, 0, 0)
                return True
            user32.EnumChildWindows(hwnd, WNDENUMPROC(child_cb), 0)

        if "Autodesk Revit" in txt:
            print(f"Found Revit Window (PID {pid.value}): '{txt}' on desktop '{dname}'")
            user32.ShowWindow(hwnd, 9)
            user32.SetForegroundWindow(hwnd)

        return True

    user32.EnumDesktopWindows(hd, WNDENUMPROC(cb), 0)
    user32.CloseDesktop(hd)
