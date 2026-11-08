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

    private const string Url =
        "http://127.0.0.1:8765/";

    public static void Start()
    {
        if (_listener != null)
        {
            return;
        }

        try
        {
            _listener = new HttpListener();

            _listener.Prefixes.Add(Url);

            _listener.Start();

            _cts = new CancellationTokenSource();

            _ = Task.Run(
                () => ListenLoopAsync(_cts.Token));
        }
        catch (Exception ex)
        {
            _listener = null;
            _cts = null;

            TaskDialog.Show(
                "AIGRAV HTTP Server Error",
                "AIGRAV HTTP Server gagal start.\n\n" +
                ex.ToString()
            );
        }
    }

    public static void Stop()
    {
        try
        {
            _cts?.Cancel();

            _listener?.Stop();
            _listener?.Close();
        }
        catch
        {
            // Ignore shutdown errors
        }

        _listener = null;
        _cts = null;
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