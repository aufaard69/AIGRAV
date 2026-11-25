using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using Autodesk.Revit.UI;

namespace AIGRAV.RevitBridge;

public static class AIGRAVHttpServer
{
    private static HttpListener? _listener;
    private static CancellationTokenSource? _cts;

    public const int BasePort = 8765;
    public const int MaxPort = 8775;

    public static int ActivePort { get; private set; } = 0;
    public static string ActiveUrl { get; private set; } = string.Empty;

    public static void Start()
    {
        if (_listener != null)
        {
            return;
        }

        Exception? lastEx = null;

        for (int port = BasePort; port <= MaxPort; port++)
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

                _ = Task.Run(
                    () => ListenLoopAsync(_cts.Token));

                return;
            }
            catch (HttpListenerException ex)
            {
                lastEx = ex;
                // Port occupied by another Revit instance, try next port
                continue;
            }
            catch (Exception ex)
            {
                lastEx = ex;
                break;
            }
        }

        _listener = null;
        _cts = null;
        ActivePort = 0;
        ActiveUrl = string.Empty;

        TaskDialog.Show(
            "AIGRAV HTTP Server Error",
            $"AIGRAV HTTP Server gagal start pada rentang port {BasePort}-{MaxPort}.\n\n" +
            (lastEx?.ToString() ?? "Seluruh port dalam rentang sedang digunakan.")
        );
    }

    public static void Stop()
    {
        try
        {
            _cts?.Cancel();

            _listener?.Stop();
            _listener?.Close();

            RemoveSessionFile();
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

    private static void WriteSessionFile(int port)
    {
        try
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "AIGRAV", "sessions");
            Directory.CreateDirectory(tempDir);

            int pid = Environment.ProcessId;
            string filePath = Path.Combine(tempDir, $"session_{pid}.json");

            var sessionInfo = new
            {
                pid = pid,
                port = port,
                url = $"http://127.0.0.1:{port}/",
                started_at = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
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