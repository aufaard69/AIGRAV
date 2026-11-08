using System;
using System.Text;
using System.Threading;
using System.Runtime.InteropServices;
using System.Windows.Automation;

public class Program {
    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr OpenDesktop(string lpszDesktop, uint dwFlags, bool fInherit, uint dwDesiredAccess);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetThreadDesktop(IntPtr hDesktop);

    [DllImport("user32.dll")]
    public static extern bool CloseDesktop(IntPtr hDesktop);

    public static void Main() {
        Console.WriteLine("[UIA DIAGNOSTIC]: Starting Antigravity UI Automation Test...");
        IntPtr hDefault = OpenDesktop("Default", 0, false, 0x01FF);
        if (hDefault == IntPtr.Zero) {
            Console.WriteLine("[ERROR] Could not open Default desktop. LastError: " + Marshal.GetLastWin32Error());
            return;
        }

        Thread t = new Thread(() => {
            bool ok = SetThreadDesktop(hDefault);
            if (!ok) {
                Console.WriteLine("[ERROR] SetThreadDesktop failed. LastError: " + Marshal.GetLastWin32Error());
                return;
            }
            Console.WriteLine("[SUCCESS] SetThreadDesktop succeeded on worker thread.");

            try {
                AutomationElement root = AutomationElement.RootElement;
                Console.WriteLine("[INFO] RootElement Name: " + root.Current.Name);

                Condition cond = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Window);
                AutomationElementCollection windows = root.FindAll(TreeScope.Children, cond);
                Console.WriteLine("[INFO] Total top-level windows found: " + windows.Count);

                AutomationElement antigravityWindow = null;
                foreach (AutomationElement w in windows) {
                    string title = w.Current.Name;
                    string className = w.Current.ClassName;
                    int pid = w.Current.ProcessId;
                    if (title.IndexOf("Antigravity", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        title.IndexOf("AIGRAV", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        title.IndexOf("Automation Ideas", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        className.IndexOf("Chrome_WidgetWin_1", StringComparison.OrdinalIgnoreCase) >= 0) {
                        Console.WriteLine("  -> Window: PID=" + pid + ", Class='" + className + "', Name='" + title + "'");
                        if (antigravityWindow == null && pid == 7664) {
                            antigravityWindow = w;
                        }
                    }
                }

                if (antigravityWindow != null) {
                    Console.WriteLine("\n[TARGET IDENTIFIED] Inspecting Antigravity Window: " + antigravityWindow.Current.Name);
                    InspectElement(antigravityWindow, 0, 3);
                } else {
                    Console.WriteLine("\n[WARNING] Antigravity window with PID 7664 not found in windows list.");
                }

            } catch (Exception ex) {
                Console.WriteLine("[EXCEPTION in UIA]: " + ex.ToString());
            }
        });

        // Do not call SetApartmentState(STA) before start to avoid pre-binding desktop!
        t.Start();
        t.Join();

        CloseDesktop(hDefault);
        Console.WriteLine("[UIA DIAGNOSTIC]: Completed.");
    }

    static void InspectElement(AutomationElement element, int depth, int maxDepth) {
        if (depth > maxDepth) return;
        string indent = new string(' ', depth * 2);
        try {
            Console.WriteLine(indent + "- [" + element.Current.ControlType.ProgrammaticName + "] Name='" + element.Current.Name + "' Class='" + element.Current.ClassName + "' Id='" + element.Current.AutomationId + "'");
            AutomationElementCollection children = element.FindAll(TreeScope.Children, Condition.TrueCondition);
            Console.WriteLine(indent + "  (Children count: " + children.Count + ")");
            foreach (AutomationElement child in children) {
                InspectElement(child, depth + 1, maxDepth);
            }
        } catch (Exception ex) {
            Console.WriteLine(indent + "  Error reading element: " + ex.Message);
        }
    }
}
