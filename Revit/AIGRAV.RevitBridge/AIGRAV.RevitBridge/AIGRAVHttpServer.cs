using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Autodesk.Revit.UI;

namespace AIGRAV.RevitBridge;

public static class AIGRAVHttpServer
{
    private static HttpListener? _listener;
    private static CancellationTokenSource? _cts;

    public const int DefaultPort = 8765;
    public static int CurrentPort { get; private set; } = DefaultPort;
    public static string CurrentUrl => $"http://127.0.0.1:{CurrentPort}/";
    public static bool IsRunning => _listener != null && _listener.IsListening;
    public static string? LastError { get; private set; }
    public static DateTime? StartedAt { get; private set; }
    public static int TotalRequestsServed { get; internal set; } = 0;

    public static event Action? StateChanged;

    private static readonly string PortConfigPath = Path.Combine(@"C:\AIGRAV", "aigrav_port.txt");
    private static readonly string TempPortConfigPath = Path.Combine(Path.GetTempPath(), "aigrav_port.txt");

    static AIGRAVHttpServer()
    {
        // Try reading saved port if exists
        try
        {
            if (File.Exists(PortConfigPath))
            {
                string txt = File.ReadAllText(PortConfigPath).Trim();
                if (int.TryParse(txt, out int savedPort) && savedPort >= 1024 && savedPort <= 65535)
                {
                    CurrentPort = savedPort;
                }
            }
        }
        catch { }
    }

    public static bool Start(int port, out string? errorMessage)
    {
        errorMessage = null;

        if (port < 1024 || port > 65535)
        {
            errorMessage = "Port harus berada dalam rentang 1024 - 65535.";
            LastError = errorMessage;
            StateChanged?.Invoke();
            return false;
        }

        // If already listening on requested port, return true
        if (_listener != null && _listener.IsListening && CurrentPort == port)
        {
            return true;
        }

        // Stop existing listener if on a different port or active
        Stop();

        try
        {
            _listener = new HttpListener();
            string prefix = $"http://127.0.0.1:{port}/";
            _listener.Prefixes.Add(prefix);
            _listener.Start();

            CurrentPort = port;
            LastError = null;
            StartedAt = DateTime.Now;

            SaveActivePort(port);

            _cts = new CancellationTokenSource();
            _ = Task.Run(() => ListenLoopAsync(_cts.Token));

            StateChanged?.Invoke();
            return true;
        }
        catch (HttpListenerException hlex) when (hlex.ErrorCode == 183)
        {
            _listener = null;
            _cts = null;
            LastError = $"Port {port} sedang digunakan oleh proses lain (ERROR_ALREADY_EXISTS / Konflik). Silakan gunakan port lain (misal: {port + 1}).";
            errorMessage = LastError;
            StateChanged?.Invoke();
            return false;
        }
        catch (Exception ex)
        {
            _listener = null;
            _cts = null;
            LastError = $"Gagal memulai server pada port {port}: {ex.Message}";
            errorMessage = LastError;
            StateChanged?.Invoke();
            return false;
        }
    }

    public static void Start()
    {
        Start(CurrentPort, out _);
    }

    public static void Stop()
    {
        try
        {
            _cts?.Cancel();
            if (_listener != null)
            {
                if (_listener.IsListening)
                {
                    _listener.Stop();
                }
                _listener.Close();
            }
        }
        catch
        {
            // Ignore shutdown errors
        }

        _listener = null;
        _cts = null;
        StateChanged?.Invoke();
    }

    public static (bool isAvailable, string message) TestPortAvailability(int port)
    {
        if (port < 1024 || port > 65535)
        {
            return (false, "Port tidak valid (harus antara 1024 - 65535).");
        }

        // If current server is already running on this port, it is valid and working
        if (IsRunning && CurrentPort == port)
        {
            return (true, $"Server saat ini sedang aktif mendengarkan di port {port}.");
        }

        // Test with TcpListener first for immediate port in use check
        TcpListener? tcpTest = null;
        try
        {
            tcpTest = new TcpListener(IPAddress.Loopback, port);
            tcpTest.Start();
            tcpTest.Stop();
        }
        catch (SocketException sex) when (sex.SocketErrorCode == SocketError.AddressAlreadyInUse)
        {
            return (false, $"Port {port} sudah terpakai oleh aplikasi/proses lain.");
        }
        catch (Exception ex)
        {
            return (false, $"Port {port} tidak dapat diakses: {ex.Message}");
        }

        // Test HttpListener prefix binding
        HttpListener? testListener = null;
        try
        {
            testListener = new HttpListener();
            testListener.Prefixes.Add($"http://127.0.0.1:{port}/");
            testListener.Start();
            testListener.Stop();
            testListener.Close();
            return (true, $"Port {port} tersedia dan siap digunakan!");
        }
        catch (HttpListenerException hlex) when (hlex.ErrorCode == 183)
        {
            return (false, $"Port {port} memiliki konflik registrasi URL di sistem (ERROR_ALREADY_EXISTS).");
        }
        catch (Exception ex)
        {
            return (false, $"Gagal menguji port {port}: {ex.Message}");
        }
        finally
        {
            try { testListener?.Close(); } catch { }
        }
    }

    public static int FindNextAvailablePort(int startingPort = 8765)
    {
        for (int p = startingPort; p <= startingPort + 50; p++)
        {
            var (isAvail, _) = TestPortAvailability(p);
            if (isAvail)
            {
                return p;
            }
        }
        return startingPort;
    }

    private static void SaveActivePort(int port)
    {
        try
        {
            File.WriteAllText(PortConfigPath, port.ToString());
        }
        catch { }

        try
        {
            File.WriteAllText(TempPortConfigPath, port.ToString());
        }
        catch { }
    }

    private static async Task ListenLoopAsync(
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext? context = null;

            try
            {
                context =
                    await _listener!.GetContextAsync();

                _ = Task.Run(
                    () => HandleRequestAsync(context),
                    cancellationToken);
            }
            catch
            {
                if (cancellationToken.IsCancellationRequested)
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

            using StreamReader reader =
                new(context.Request.InputStream);

            string body =
                await reader.ReadToEndAsync();

            AIGRAVHttpRequest? request =
                JsonSerializer.Deserialize<AIGRAVHttpRequest>(
                    body,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

            if (request == null ||
                string.IsNullOrWhiteSpace(request.Tool))
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

            TotalRequestsServed++;
            StateChanged?.Invoke();

            string result =
                await AIGRAVBridge.ExecuteToolAsync(
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
        string json =
            JsonSerializer.Serialize(response);

        byte[] bytes =
            Encoding.UTF8.GetBytes(json);

        context.Response.ContentType =
            "application/json";

        context.Response.ContentLength64 =
            bytes.Length;

        await context.Response.OutputStream.WriteAsync(
            bytes);

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
