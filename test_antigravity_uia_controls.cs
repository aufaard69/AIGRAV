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
        IntPtr hDefault = OpenDesktop("Default", 0, false, 0x01FF);
        if (hDefault == IntPtr.Zero) return;

        Thread t = new Thread(() => {
            if (!SetThreadDesktop(hDefault)) return;

            AutomationElement root = AutomationElement.RootElement;
            Condition cond = new PropertyCondition(AutomationElement.ProcessIdProperty, 7664);
            AutomationElement antigravity = root.FindFirst(TreeScope.Children, cond);

            if (antigravity == null) {
                Console.WriteLine("[ERROR] Antigravity process 7664 window not found.");
                return;
            }

            Console.WriteLine("[TARGET] Antigravity Window: " + antigravity.Current.Name);

            // Find all Edit controls (input boxes)
            Condition editCond = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit);
            AutomationElementCollection edits = antigravity.FindAll(TreeScope.Descendants, editCond);
            Console.WriteLine("[RESULT] Total Edit (Input) controls found: " + edits.Count);
            foreach (AutomationElement e in edits) {
                try {
                    Console.WriteLine("  -> Edit: Name='" + e.Current.Name + "' Id='" + e.Current.AutomationId + "' HelpText='" + e.Current.HelpText + "'");
                } catch {}
            }

            // Find all Document controls
            Condition docCond = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Document);
            AutomationElementCollection docs = antigravity.FindAll(TreeScope.Descendants, docCond);
            Console.WriteLine("[RESULT] Total Document controls found: " + docs.Count);
            foreach (AutomationElement d in docs) {
                try {
                    Console.WriteLine("  -> Document: Name='" + d.Current.Name + "' Id='" + d.Current.AutomationId + "'");
                } catch {}
            }

            // Find all Buttons
            Condition btnCond = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button);
            AutomationElementCollection btns = antigravity.FindAll(TreeScope.Descendants, btnCond);
            Console.WriteLine("[RESULT] Total Button controls found: " + btns.Count);
            foreach (AutomationElement b in btns) {
                try {
                    string bName = b.Current.Name;
                    if (!string.IsNullOrEmpty(bName)) {
                        Console.WriteLine("  -> Button: Name='" + bName + "' Id='" + b.Current.AutomationId + "'");
                    }
                } catch {}
            }
        });

        t.Start();
        t.Join();

        CloseDesktop(hDefault);
    }
}
