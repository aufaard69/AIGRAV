using System;
class Program {
    static void Main() {
        foreach (var name in Enum.GetNames(typeof(Autodesk.Revit.DB.ScheduleFilterType))) {
            Console.WriteLine(name);
        }
    }
}
