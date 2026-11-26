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

    public static AIGRAV.RevitBridge.Render.Core.MultiPassExportHandler? RenderExportHandler { get; private set; }
    public static ExternalEvent? RenderExternalEvent { get; private set; }

    private static AIGRAVWelcomeWindow? _welcomeWindow;
    private static PushButton? _aigravButton;
    private static PushButton? _renderButton;

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

            try
            {
                AIGRAVHttpServer.Start();
            }
            catch
            {
                // Never halt Revit startup due to HTTP listener conflict.
                // The user can connect or change port interactively via the Bridge Controller window.
            }

            AIGRAVSkillRegistry.Initialize();
            AIGRAVSkillLoader.Initialize();


            // ----------------------------------------------------
            // EXTERNAL EVENTS
            // ----------------------------------------------------

            EventHandler =
                new RevitExternalEventHandler();

            ExternalEvent =
                ExternalEvent.Create(EventHandler);

            // Dedicated isolated handler for AIGRAV Render module
            RenderExportHandler =
                new AIGRAV.RevitBridge.Render.Core.MultiPassExportHandler();

            RenderExternalEvent =
                ExternalEvent.Create(RenderExportHandler);


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
            // PUSH BUTTON 1: AIGRAV Bridge
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

            // ----------------------------------------------------
            // PUSH BUTTON 2: AIGRAV Render
            // ----------------------------------------------------

            PushButtonData renderButtonData =
                new PushButtonData(
                    "AIGRAVRenderLaunch",
                    "AIGRAV Render",
                    assemblyPath,
                    "AIGRAV.RevitBridge.Render.Commands.LaunchAigravRenderCommand");

            _renderButton =
                panel.AddItem(renderButtonData) as PushButton;

            if (_renderButton != null)
            {
                ConfigureRenderRibbonButton(_renderButton);
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

    private static void ConfigureRenderRibbonButton(
        PushButton button)
    {
        button.ToolTip =
            "AIGRAV Render Studio — 4K Precision Architectural Visualizer";

        button.LongDescription =
            "Launch the AIGRAV Render Studio to extract 3-pass 4K views (Geometry, Lighting, Material ID) " +
            "and generate photorealistic architectural renders via Antigravity Bridge or Direct AI APIs.";

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
                    "AIGRAV_Gemini_Icon.png");

            string fallbackIconPath =
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
                button.LargeImage = LoadRibbonImageFromFile(largeIconPath);
            }
            else if (File.Exists(fallbackIconPath))
            {
                button.LargeImage = LoadRibbonImageFromFile(fallbackIconPath);
            }

            if (File.Exists(smallIconPath))
            {
                button.Image = LoadRibbonImageFromFile(smallIconPath);
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
            int port = AIGRAVHttpServer.ActivePort;
            string portText = port > 0 ? $" on Port {port}" : "";

            _aigravButton.ToolTip =
                $"AIGRAV is ACTIVE{portText}. Click to turn AIGRAV OFF.";

            _aigravButton.LongDescription =
                $"AIGRAV is active{portText} and accepting MCP requests. " +
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
        RenderExternalEvent?.Dispose();

        ExternalEvent = null;
        EventHandler = null;
        RenderExternalEvent = null;
        RenderExportHandler = null;
        _aigravButton = null;
        _renderButton = null;

        AIGRAVRuntime.Shutdown();

        return Result.Succeeded;
    }
}