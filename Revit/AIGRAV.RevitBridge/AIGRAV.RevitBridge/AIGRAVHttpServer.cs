using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Autodesk.Revit.UI;

namespace AIGRAV.RevitBridge;

public static class AIGRAVHttpServer
{
    private static readonly object StateLock = new();
    private static HttpListener? _listener;
    private static CancellationTokenSource? _cts;
    private static long _requestsServed;

    public const int BasePort = 8765;
    public const int MaxPort = 8775;

    private static readonly string TempPortConfigPath = Path.Combine(Path.GetTempPath(), "aigrav_port.txt");

    public static int ActivePort { get; private set; } = 0;
    public static string ActiveUrl { get; private set; } = string.Empty;

    public static bool IsRunning
    {
        get
        {
            lock (StateLock)
            {
                return _listener != null && _listener.IsListening && ActivePort > 0;
            }
        }
    }

    public static long RequestsServed => Interlocked.Read(ref _requestsServed);

    public static event Action? StateChanged;

    private static void NotifyStateChanged()
    {
        try
        {
            StateChanged?.Invoke();
        }
        catch
        {
            // UI subscribers must never disrupt HTTP server lifecycle
        }
    }

    public static void Start()
    {
        var (ok, _, errorMessage) = StartAuto(showErrorDialog: true);
        if (!ok && !string.IsNullOrWhiteSpace(errorMessage))
        {
            // Error dialog is already handled inside StartAuto when showErrorDialog == true
        }
    }

    public static (bool ok, int port, string message) StartAuto(bool showErrorDialog = false)
    {
        lock (StateLock)
        {
            if (_listener != null && _listener.IsListening && ActivePort > 0)
            {
                return (true, ActivePort, $"Bridge sudah berjalan di {ActiveUrl}");
            }
        }

        Exception? lastEx = null;

        for (int port = BasePort; port <= MaxPort; port++)
        {
            var attempt = TryBindPortInternal(port);
            if (attempt.ok)
            {
                return (attempt.ok, attempt.port, attempt.message);
            }
            lastEx = attempt.exception;
            if (attempt.fatalNonSocketError)
            {
                break;
            }
        }

        if (showErrorDialog)
        {
            TaskDialog.Show(
                "AIGRAV HTTP Server Error",
                $"AIGRAV HTTP Server gagal start pada rentang port {BasePort}-{MaxPort}.\n\n" +
                (lastEx?.ToString() ?? "Seluruh port dalam rentang sedang digunakan."));
        }

        return (false, 0, lastEx?.Message ?? $"Seluruh port pada rentang {BasePort}-{MaxPort} sedang digunakan.");
    }

    public static (bool ok, int port, string message) Restart(int? preferredPort = null)
    {
        Stop();

        if (preferredPort.HasValue)
        {
            int target = preferredPort.Value;
            if (target < 1024 || target > 65535)
            {
                return (false, 0, "Port harus berada di rentang 1024-65535.");
            }
            var attempt = TryBindPortInternal(target);
            if (attempt.ok)
            {
                return (true, attempt.port, attempt.message);
            }
            return (false, 0, attempt.message);
        }

        return StartAuto(showErrorDialog: false);
    }

    public static (bool ok, int port, string message) SwitchToNextFreePort()
    {
        int current = ActivePort > 0 ? ActivePort : BasePort - 1;
        var candidates = new List<int>();

        for (int p = Math.Max(BasePort, current + 1); p <= MaxPort; p++)
        {
            candidates.Add(p);
        }
        for (int p = BasePort; p <= Math.Min(MaxPort, current - 1); p++)
        {
            candidates.Add(p);
        }
        if (candidates.Count == 0 && ActivePort > 0)
        {
            candidates.Add(ActivePort);
        }

        foreach (int candidate in candidates)
        {
            if (IsTcpPortAvailable(candidate))
            {
                Stop();
                var res = TryBindPortInternal(candidate);
                if (res.ok)
                {
                    return (true, res.port, $"Berpindah ke port bebas :{res.port}");
                }
            }
        }

        // Fallback if candidate outside BasePort..MaxPort is needed
        for (int p = MaxPort + 1; p <= MaxPort + 20; p++)
        {
            if (IsTcpPortAvailable(p))
            {
                Stop();
                var res = TryBindPortInternal(p);
                if (res.ok)
                {
                    return (true, res.port, $"Berpindah ke port bebas :{res.port}");
                }
            }
        }

        return (false, ActivePort, "Tidak ditemukan port bebas di rentang 8765-8795.");
    }

    private static bool IsTcpPortAvailable(int port)
    {
        try
        {
            using var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static (bool ok, int port, string message, Exception? exception, bool fatalNonSocketError) TryBindPortInternal(int port)
    {
        lock (StateLock)
        {
            try
            {
                var listener = new HttpListener();
                string url = $"http://127.0.0.1:{port}/";
                listener.Prefixes.Add(url);
                listener.Start();

                _listener = listener;
                ActivePort = port;
                ActiveUrl = url;
                _cts = new CancellationTokenSource();

                WriteSessionFile(port);
                SaveActivePortHint(port);

                var token = _cts.Token;
                _ = Task.Run(() => ListenLoopAsync(listener, token));

                NotifyStateChanged();
                return (true, port, $"Bridge Connected di {url}", null, false);
            }
            catch (HttpListenerException ex)
            {
                int code = ex.NativeErrorCode != 0 ? ex.NativeErrorCode : ex.ErrorCode;
                string detail = code == 183
                    ? $"Port {port} sedang digunakan atau terdapat konflik registrasi URL (ERROR_ALREADY_EXISTS)."
                    : $"Port {port} tidak dapat dipakai (Error {code}: {ex.Message}).";
                return (false, 0, detail, ex, false);
            }
            catch (Exception ex)
            {
                return (false, 0, ex.Message, ex, true);
            }
        }
    }

    public static void Stop()
    {
        lock (StateLock)
        {
            int previousPort = ActivePort;
            try
            {
                _cts?.Cancel();
                _listener?.Stop();
                _listener?.Close();
                RemoveSessionFile();
                RemoveActivePortHintIfOwned(previousPort);
            }
            catch
            {
                // Ignore shutdown errors
            }

            _listener = null;
            _cts = null;
            ActivePort = 0;
            ActiveUrl = string.Empty;
        }

        NotifyStateChanged();
    }

    public static async Task<(bool ok, string message)> TestSelfConnectionAsync(int? portOverride = null)
    {
        int targetPort = portOverride ?? ActivePort;
        if (targetPort <= 0)
        {
            return (false, "HTTP Server Bridge belum aktif. Tekan Restart Bridge terlebih dahulu.");
        }

        string url = $"http://127.0.0.1:{targetPort}/";
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            using var content = new StringContent("{\"tool\":\"__bridge_health__\",\"argument\":\"\"}", Encoding.UTF8, "application/json");
            using var resp = await client.PostAsync(url, content).ConfigureAwait(false);
            string body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!resp.IsSuccessStatusCode)
            {
                return (false, $"HTTP {(int)resp.StatusCode} pada {url}");
            }

            return (true, $"Bridge Connected di {url} • Siap melayani instruksi AI Revit.");
        }
        catch (Exception ex)
        {
            return (false, $"Koneksi ke {url} gagal: {ex.Message}");
        }
    }

    private static void SaveActivePortHint(int port)
    {
        try
        {
            File.WriteAllText(TempPortConfigPath, port.ToString(), Encoding.UTF8);
        }
        catch { }

        try
        {
            string? rootPortPath = AIGRAVVersionChecker.ResolveRootFile("aigrav_port.txt");
            if (!string.IsNullOrWhiteSpace(rootPortPath))
            {
                File.WriteAllText(rootPortPath, port.ToString(), Encoding.UTF8);
            }
        }
        catch { }
    }

    private static void RemoveActivePortHintIfOwned(int port)
    {
        if (port <= 0) return;

        try
        {
            if (File.Exists(TempPortConfigPath) &&
                File.ReadAllText(TempPortConfigPath).Trim() == port.ToString())
            {
                File.Delete(TempPortConfigPath);
            }
        }
        catch { }

        try
        {
            string? rootPortPath = AIGRAVVersionChecker.ResolveRootFile("aigrav_port.txt");
            if (!string.IsNullOrWhiteSpace(rootPortPath) &&
                File.Exists(rootPortPath) &&
                File.ReadAllText(rootPortPath).Trim() == port.ToString())
            {
                File.Delete(rootPortPath);
            }
        }
        catch { }
    }

    private static void WriteSessionFile(int port)
    {
        try
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "AIGRAV", "sessions");
            Directory.CreateDirectory(tempDir);

            int pid = Environment.ProcessId;
            string filePath = Path.Combine(tempDir, $"session_{pid}.json");
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            var sessionInfo = new
            {
                pid = pid,
                port = port,
                url = $"http://127.0.0.1:{port}/",
                started_at = timestamp,
                updated_at = timestamp
            };

            string json = JsonSerializer.Serialize(sessionInfo, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json, Encoding.UTF8);
        }
        catch
        {
            // Session file writing must never crash the server
        }
    }

    private static void RemoveSessionFile()
    {
        try
        {
            int pid = Environment.ProcessId;
            string filePath = Path.Combine(Path.GetTempPath(), "AIGRAV", "sessions", $"session_{pid}.json");
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch
        {
            // Ignore session cleanup errors
        }
    }

    private static async Task ListenLoopAsync(
        HttpListener listener,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && listener.IsListening)
        {
            try
            {
                HttpListenerContext context = await listener.GetContextAsync();

                _ = Task.Run(
                    () => HandleRequestAsync(context),
                    cancellationToken);
            }
            catch
            {
                if (cancellationToken.IsCancellationRequested || !listener.IsListening)
                {
                    break;
                }
            }
        }
    }

    private static async Task HandleRequestAsync(
        HttpListenerContext context)
    {
        try
        {
            if (context.Request.HttpMethod != "POST")
            {
                context.Response.StatusCode = 405;
                context.Response.Close();
                return;
            }

            using StreamReader reader = new(
                context.Request.InputStream,
                context.Request.ContentEncoding ?? Encoding.UTF8);
            string body = await reader.ReadToEndAsync();

            AIGRAVHttpRequest? request = JsonSerializer.Deserialize<AIGRAVHttpRequest>(
                body,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (request == null || string.IsNullOrWhiteSpace(request.Tool))
            {
                await WriteResponseAsync(
                    context,
                    new AIGRAVHttpResponse
                    {
                        Success = false,
                        Result = "Invalid request."
                    });

                return;
            }

            if (request.Tool.Equals("__bridge_health__", StringComparison.OrdinalIgnoreCase))
            {
                string payload = JsonSerializer.Serialize(new
                {
                    status = "Connected",
                    port = ActivePort,
                    url = ActiveUrl,
                    pid = Environment.ProcessId,
                    requests_served = RequestsServed
                });

                await WriteResponseAsync(
                    context,
                    new AIGRAVHttpResponse
                    {
                        Success = true,
                        Result = payload
                    });
                return;
            }

            Interlocked.Increment(ref _requestsServed);
            NotifyStateChanged();

            string result = await AIGRAVBridge.ExecuteToolAsync(
                request.Tool,
                request.Argument);

            await WriteResponseAsync(
                context,
                new AIGRAVHttpResponse
                {
                    Success = true,
                    Result = result
                });
        }
        catch (Exception ex)
        {
            await WriteResponseAsync(
                context,
                new AIGRAVHttpResponse
                {
                    Success = false,
                    Result = ex.ToString()
                });
        }
    }

    private static async Task WriteResponseAsync(
        HttpListenerContext context,
        AIGRAVHttpResponse response)
    {
        string json = JsonSerializer.Serialize(response);
        byte[] bytes = Encoding.UTF8.GetBytes(json);

        context.Response.ContentType = "application/json";
        context.Response.ContentLength64 = bytes.Length;

        await context.Response.OutputStream.WriteAsync(bytes);
        context.Response.Close();
    }
}

public class AIGRAVHttpRequest
{
    public string Tool { get; set; } = string.Empty;
    public string? Argument { get; set; }
}

public class AIGRAVHttpResponse
{
    public bool Success { get; set; }
    public string Result { get; set; } = string.Empty;
}
