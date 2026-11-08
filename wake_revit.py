import ctypes
import win32gui
import win32process
import win32con

user32 = ctypes.windll.user32
h_default = user32.OpenDesktopW('Default', 0, False, 0x01ff)
if h_default:
    user32.SetThreadDesktop(h_default)
    def cb(hwnd, _):
        cls = win32gui.GetClassName(hwnd)
        if cls == "Ghost":
            return True
        _, pid = win32process.GetWindowThreadProcessId(hwnd)
        txt = win32gui.GetWindowText(hwnd)
        if "Autodesk Revit" in txt or ("Revit" in cls and "tooltips" not in cls):
            if "Worker" not in txt:
                user32.ShowWindow(hwnd, 9)
                user32.SetForegroundWindow(hwnd)
                user32.PostMessageW(hwnd, win32con.WM_NULL, 0, 0)
        return True
    win32gui.EnumDesktopWindows(h_default, cb, None)
    user32.CloseDesktop(h_default)
