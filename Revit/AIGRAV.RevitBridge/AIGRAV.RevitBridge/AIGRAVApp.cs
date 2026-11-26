using System.Reflection;
using System.IO;
using System.Windows.Media.Imaging;
using AIGRAV.RevitBridge.UI.RebarSuite;
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
                UpdateRibbonState();
            }

            // ----------------------------------------------------
            // OFFLINE REBAR SUITE (100% DETERMINISTIC, NO AI)
            // ----------------------------------------------------
            AIGRAVSkillRegistry.Register(new AIGRAV.Skills.RebarExtension.RebarExtensionSkill());
            RebarSuiteController.Initialize(application);
            BuildRebarSuiteRibbonPanel(application, tabName, assemblyPath);

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
    // REBAR SUITE RIBBON PANEL (100% OFFLINE)
    // ============================================================

    private static void BuildRebarSuiteRibbonPanel(
        UIControlledApplication application,
        string tabName,
        string assemblyPath)
    {
        RibbonPanel rebarPanel = application.CreateRibbonPanel(tabName, "Rebar Suite");

        // 1. Primary Large Button: Rebar Studio (3D Preview + Selection Generator)
        var studioData = new PushButtonData(
            "AIGRAV_RebarStudio",
            "Rebar\nStudio",
            assemblyPath,
            "AIGRAV.RevitBridge.UI.RebarSuite.OpenRebarStudioCommand")
        {
            ToolTip = "Buka AIGRAV Rebar Studio (100% Offline, Selection-Driven 3D & 2D Preview).",
            LongDescription = "Pemodelan tulangan deterministik (Balok, Kolom dari pondasi, Pondasi murni, Pelat void) dengan live Viewport3D, splice ≤ 12m, dan BBS.",
            LargeImage = RebarIconFactory.LoadIcon("rebar_studio", "RB", "#38BDF8", 32),
            Image = RebarIconFactory.LoadIcon("rebar_studio", "RB", "#38BDF8", 16)
        };
        rebarPanel.AddItem(studioData);

        // 2. Large Button: Reinforcement Browser Dockable Pane
        var browserData = new PushButtonData(
            "AIGRAV_RebarBrowser",
            "Rebar\nBrowser",
            assemblyPath,
            "AIGRAV.RevitBridge.UI.RebarSuite.ToggleRebarBrowserCommand")
        {
            ToolTip = "Buka/Tutup Dockable Reinforcement Browser (Level -> Host -> Marks).",
            LargeImage = RebarIconFactory.LoadIcon("rebar_browser", "BR", "#34D399", 32),
            Image = RebarIconFactory.LoadIcon("rebar_browser", "BR", "#34D399", 16)
        };
        rebarPanel.AddItem(browserData);

        rebarPanel.AddSeparator();

        // 3. Stacked Column 1: Split / Propagate / Smart Mark
        var splitData = new PushButtonData(
            "AIGRAV_RebarSplit",
            "Split / Stagger",
            assemblyPath,
            "AIGRAV.RevitBridge.UI.RebarSuite.SplitRebarCommand")
        {
            ToolTip = "Potong & overlap rebar lurus terpilih ≤ 12m secara staggered.",
            LargeImage = RebarIconFactory.LoadIcon("split_stagger", "SP", "#FBBF24", 32),
            Image = RebarIconFactory.LoadIcon("split_stagger", "SP", "#FBBF24", 16)
        };
        var propData = new PushButtonData(
            "AIGRAV_RebarPropagate",
            "Propagate",
            assemblyPath,
            "AIGRAV.RevitBridge.UI.RebarSuite.PropagateRebarCommand")
        {
            ToolTip = "Salin pola penulangan dari 1 host sumber ke beberapa host target.",
            LargeImage = RebarIconFactory.LoadIcon("propagate", "PR", "#60A5FA", 32),
            Image = RebarIconFactory.LoadIcon("propagate", "PR", "#60A5FA", 16)
        };
        var markData = new PushButtonData(
            "AIGRAV_RebarMark",
            "Smart Mark",
            assemblyPath,
            "AIGRAV.RevitBridge.UI.RebarSuite.SmartMarkRebarCommand")
        {
            ToolTip = "Penomoran AIGRAV_Mark & Schedule Mark berdasarkan diameter, shape & panjang.",
            LargeImage = RebarIconFactory.LoadIcon("smart_mark", "MK", "#A855F7", 32),
            Image = RebarIconFactory.LoadIcon("smart_mark", "MK", "#A855F7", 16)
        };
        rebarPanel.AddStackedItems(splitData, propData, markData);

        // 4. Stacked Column 2: Auto-Tag / BBS Schedule / Export CSV+BVBS
        var tagData = new PushButtonData(
            "AIGRAV_RebarAutoTag",
            "Auto-Tag",
            assemblyPath,
            "AIGRAV.RevitBridge.UI.RebarSuite.AutoTagRebarCommand")
        {
            ToolTip = "Pasang Structural Rebar Tag otomatis pada view aktif.",
            LargeImage = RebarIconFactory.LoadIcon("auto_tag", "TG", "#2DD4BF", 32),
            Image = RebarIconFactory.LoadIcon("auto_tag", "TG", "#2DD4BF", 16)
        };
        var bbsData = new PushButtonData(
            "AIGRAV_RebarBbsSched",
            "BBS Schedule",
            assemblyPath,
            "AIGRAV.RevitBridge.UI.RebarSuite.CreateBbsScheduleCommand")
        {
            ToolTip = "Buat/buka native Revit Rebar Bending Schedule (Lokasi + Mark + Total).",
            LargeImage = RebarIconFactory.LoadIcon("bbs_schedule", "BB", "#F97316", 32),
            Image = RebarIconFactory.LoadIcon("bbs_schedule", "BB", "#F97316", 16)
        };
        var expData = new PushButtonData(
            "AIGRAV_RebarExportBbs",
            "Export CSV/BVBS",
            assemblyPath,
            "AIGRAV.RevitBridge.UI.RebarSuite.ExportBbsCommand")
        {
            ToolTip = "Ekspor daftar potong besi ke CSV (Excel) dan format mesin BVBS (.abs).",
            LargeImage = RebarIconFactory.LoadIcon("export_bbs", "EX", "#32D583", 32),
            Image = RebarIconFactory.LoadIcon("export_bbs", "EX", "#32D583", 16)
        };
        rebarPanel.AddStackedItems(tagData, bbsData, expData);
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
                enabled: true);
        }

        if (!AIGRAVRuntime.IsEnabled || !AIGRAVHttpServer.IsRunning)
        {
            EnableAIGRAV();
            return;
        }

        ShowWelcome();
        UpdateRibbonState();
    }

    public static void RefreshRibbonStatus()
    {
        UpdateRibbonState();
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
        RebarSuiteController.Shutdown(application);
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
