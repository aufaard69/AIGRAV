using System.Reflection;
using System.IO;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;

namespace AIGRAV.RevitBridge;

public class AIGRAVApp : IExternalApplication
{
    public static ExternalEvent? ExternalEvent { get; private set; }

    public static RevitExternalEventHandler? EventHandler
    {
        get;
        private set;
    }

    private static AIGRAVWelcomeWindow? _welcomeWindow;
    private static PushButton? _aigravButton;

    public Result OnStartup(
        UIControlledApplication application)
    {
        try
        {
            // ----------------------------------------------------
            // AIGRAV RUNTIME
            // ----------------------------------------------------
            // AIGRAV is initialized at Revit startup, but remains
            // OFF until the user activates it from the Ribbon.
            // ----------------------------------------------------

            AIGRAVRuntime.Initialize(
                developmentMode: true,
                enabled: true);

            AIGRAVHttpServer.Start();

            AIGRAVSkillRegistry.Initialize();
            AIGRAVSkillLoader.Initialize();


            // ----------------------------------------------------
            // EXTERNAL EVENT
            // ----------------------------------------------------

            EventHandler =
                new RevitExternalEventHandler();

            ExternalEvent =
                ExternalEvent.Create(EventHandler);


            // ----------------------------------------------------
            // RIBBON TAB
            // ----------------------------------------------------

            const string tabName = "AIGRAV";

            try
            {
                application.CreateRibbonTab(tabName);
            }
            catch
            {
                // Tab already exists.
            }


            // ----------------------------------------------------
            // RIBBON PANEL
            // ----------------------------------------------------

            RibbonPanel panel =
                application.CreateRibbonPanel(
                    tabName,
                    "AIGRAV AI");


            // ----------------------------------------------------
            // PUSH BUTTON
            // ----------------------------------------------------

            string assemblyPath =
                Assembly.GetExecutingAssembly().Location;

            PushButtonData buttonData =
                new PushButtonData(
                    "AIGRAVLaunch",
                    "AIGRAV",
                    assemblyPath,
                    "AIGRAV.RevitBridge.AIGRAVCommand");

            _aigravButton =
                panel.AddItem(buttonData) as PushButton;

            if (_aigravButton != null)
            {
                ConfigureRibbonButton(_aigravButton);
            }

            // IMPORTANT:
            // No AIGRAV window is opened during Revit startup.
            // AIGRAV remains OFF until the Ribbon button is clicked.

            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            TaskDialog.Show(
                "AIGRAV Startup Error",
                ex.ToString());

            return Result.Failed;
        }
    }


    // ============================================================
    // RIBBON BUTTON CONFIGURATION
    // ============================================================

    private static void ConfigureRibbonButton(
        PushButton button)
    {
        button.ToolTip =
            "AIGRAV is OFF. Click to turn AIGRAV ON.";

        button.LongDescription =
            "AIGRAV is initialized with Revit but currently OFF. " +
            "Click the button to start the AIGRAV AI Bridge.";

        try
        {
            string assemblyDirectory =
                Path.GetDirectoryName(
                    Assembly.GetExecutingAssembly().Location)
                ?? AppContext.BaseDirectory;

            string largeIconPath =
                Path.Combine(
                    assemblyDirectory,
                    "Assets",
                    "AIGRAV_Ribbon_32.png");

            string smallIconPath =
                Path.Combine(
                    assemblyDirectory,
                    "Assets",
                    "AIGRAV_Ribbon_16.png");

            if (File.Exists(largeIconPath))
            {
                button.LargeImage =
                    LoadRibbonImageFromFile(
                        largeIconPath);
            }

            if (File.Exists(smallIconPath))
            {
                button.Image =
                    LoadRibbonImageFromFile(
                        smallIconPath);
            }
        }
        catch
        {
            // Ribbon icon failure must not stop AIGRAV.
        }
    }

    private static BitmapImage LoadRibbonImageFromFile(
        string filePath)
    {
        BitmapImage image = new();

        image.BeginInit();
        image.UriSource =
            new Uri(filePath, UriKind.Absolute);
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
        image.EndInit();
        image.Freeze();

        return image;
    }

    // ============================================================
    // AIGRAV ON / OFF
    // ============================================================

    public static void ToggleAIGRAV()
    {
        if (!AIGRAVRuntime.IsInitialized)
        {
            AIGRAVRuntime.Initialize(
                developmentMode: true,
                enabled: false);
        }

        if (AIGRAVRuntime.IsEnabled)
        {
            DisableAIGRAV();
        }
        else
        {
            EnableAIGRAV();
        }
    }

    public static void EnableAIGRAV()
    {
        AIGRAVRuntime.Enable();

        // Start the local bridge only when AIGRAV is active.
        AIGRAVHttpServer.Start();

        ShowWelcome();

        UpdateRibbonState();
    }

    public static void DisableAIGRAV()
    {
        AIGRAVRuntime.Disable();

        // Stop external AI communication while AIGRAV is OFF.
        AIGRAVHttpServer.Stop();

        CloseWelcome();

        UpdateRibbonState();
    }


    // ============================================================
    // WELCOME / CONTROL WINDOW
    // ============================================================

    public static void ShowWelcome()
    {
        if (_welcomeWindow != null)
        {
            if (_welcomeWindow.IsVisible)
            {
                _welcomeWindow.Activate();
                return;
            }

            _welcomeWindow = null;
        }

        _welcomeWindow =
            new AIGRAVWelcomeWindow();

        _welcomeWindow.Closed +=
            (_, _) =>
            {
                _welcomeWindow = null;
            };

        _welcomeWindow.Show();
        _welcomeWindow.Activate();
    }

    public static void CloseWelcome()
    {
        try
        {
            if (_welcomeWindow != null)
            {
                _welcomeWindow.Close();
                _welcomeWindow = null;
            }
        }
        catch
        {
            // Ignore UI shutdown errors.
        }
    }


    // ============================================================
    // RIBBON STATUS
    // ============================================================

    private static void UpdateRibbonState()
    {
        if (_aigravButton == null)
        {
            return;
        }

        if (AIGRAVRuntime.IsEnabled)
        {
            _aigravButton.ToolTip =
                "AIGRAV is ACTIVE. Click to turn AIGRAV OFF.";

            _aigravButton.LongDescription =
                "AIGRAV is active and accepting MCP requests. " +
                "Click the button again to turn AIGRAV OFF.";
        }
        else
        {
            _aigravButton.ToolTip =
                "AIGRAV is OFF. Click to turn AIGRAV ON.";

            _aigravButton.LongDescription =
                "AIGRAV is initialized but currently OFF. " +
                "Click the button to start the AIGRAV AI Bridge.";
        }
    }


    // ============================================================
    // REVIT SHUTDOWN
    // ============================================================

    public Result OnShutdown(
        UIControlledApplication application)
    {
        try
        {
            CloseWelcome();
        }
        catch
        {
            // Ignore UI shutdown errors.
        }

        AIGRAVHttpServer.Stop();

        ExternalEvent?.Dispose();

        ExternalEvent = null;
        EventHandler = null;
        _aigravButton = null;

        AIGRAVRuntime.Shutdown();

        return Result.Succeeded;
    }
}
