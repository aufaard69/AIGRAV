using System;
using Autodesk.Revit.DB;

namespace AIGRAV.Contracts;

public static class EnumDump
{
    public static string DumpScheduleFilterType()
    {
        return string.Join(", ", Enum.GetNames(typeof(ScheduleFilterType)));
    }
}
