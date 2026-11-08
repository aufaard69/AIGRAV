namespace AIGRAV.RevitBridge;

public static class AIGRAVRuntime
{
    private static readonly object Sync = new();

    public static bool IsInitialized { get; private set; }

    public static bool IsEnabled { get; private set; }

    public static bool IsDevelopmentMode { get; private set; }

    public static DateTime InitializedAt { get; private set; }

    public static void Initialize(
        bool developmentMode = true,
        bool enabled = false)
    {
        lock (Sync)
        {
            if (IsInitialized)
            {
                return;
            }

            IsDevelopmentMode = developmentMode;
            IsEnabled = enabled;
            IsInitialized = true;
            InitializedAt = DateTime.Now;
        }
    }

    public static void Enable()
    {
        lock (Sync)
        {
            if (!IsInitialized)
            {
                IsDevelopmentMode = true;
                IsInitialized = true;
                InitializedAt = DateTime.Now;
            }

            IsEnabled = true;
        }
    }

    public static void Disable()
    {
        lock (Sync)
        {
            IsEnabled = false;
        }
    }

    public static void SetDevelopmentMode(bool enabled)
    {
        lock (Sync)
        {
            IsDevelopmentMode = enabled;
        }
    }

    public static string GetStatus()
    {
        lock (Sync)
        {
            return
                $"AIGRAV Runtime: {(IsInitialized ? "INITIALIZED" : "NOT INITIALIZED")}\n" +
                $"AIGRAV Engine: {(IsEnabled ? "ACTIVE" : "OFF")}\n" +
                $"Development Mode: {(IsDevelopmentMode ? "ON" : "OFF")}\n" +
                $"Initialized At: {(IsInitialized ? InitializedAt.ToString("yyyy-MM-dd HH:mm:ss") : "-")}";
        }
    }

    public static void Shutdown()
    {
        lock (Sync)
        {
            IsEnabled = false;
            IsInitialized = false;
            IsDevelopmentMode = false;
        }
    }
}
