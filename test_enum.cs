    using System;
    using System.Linq;
    using System.Reflection;
    class P {
        static void Main() {
            var asm = Assembly.LoadFrom(@"C:\Program Files\Autodesk\Revit 2026\RevitAPI.dll");
            var t = asm.GetType("Autodesk.Revit.DB.ScheduleFilterType");
            foreach (var n in Enum.GetNames(t)) Console.WriteLine(n);
        }
    }
