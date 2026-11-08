using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AIGRAV.RevitBridge;

public static class RevitContext
{
    public static UIApplication? UIApp { get; private set; }

    public static UIDocument? UIDoc =>
        UIApp?.ActiveUIDocument;

    public static Document? Document =>
        UIDoc?.Document;

    public static void Initialize(UIApplication uiApp)
    {
        UIApp = uiApp;
    }
}