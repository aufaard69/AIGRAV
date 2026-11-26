using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AIGRAV.RevitBridge.Render.Core;

namespace AIGRAV.RevitBridge.Render.Engine;

public class XaiGrokImagineClient : IAigravRenderDispatcher
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromMinutes(5) };

    public async Task<RenderResult> DispatchAsync(
        RenderJobContext context,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        string apiKey = context.ApiKey?.Trim()
            ?? AigravKeyStorage.GetKey("grok")
            ?? Environment.GetEnvironmentVariable("XAI_API_KEY")
            ?? string.Empty;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return RenderResult.Fail("API Key untuk xAI Grok belum diisi. Masukkan API Key di panel kiri atau simpan di keys.dat.");
        }

        string model = string.IsNullOrWhiteSpace(context.ModelName) ? "grok-imagine-image-2.0" : context.ModelName;
        progress?.Report($"Mengemas prompt dan panduan visual untuk xAI Grok ({model})...");

        string promptText = RenderPromptBuilder.BuildPrompt(context);

        var contentList = new List<object>
        {
            new { type = "text", text = promptText }
        };

        if (File.Exists(context.GeometryPassPath))
        {
            byte[] geomBytes = await File.ReadAllBytesAsync(context.GeometryPassPath, cancellationToken);
            contentList.Add(new
            {
                type = "image_url",
                image_url = new { url = $"data:image/png;base64,{Convert.ToBase64String(geomBytes)}" }
            });
        }

        if (File.Exists(context.LightingPassPath))
        {
            byte[] lightBytes = await File.ReadAllBytesAsync(context.LightingPassPath, cancellationToken);
            contentList.Add(new
            {
                type = "image_url",
                image_url = new { url = $"data:image/png;base64,{Convert.ToBase64String(lightBytes)}" }
            });
        }

        if (File.Exists(context.MaterialIdCleanPath))
        {
            byte[] matBytes = await File.ReadAllBytesAsync(context.MaterialIdCleanPath, cancellationToken);
            contentList.Add(new
            {
                type = "image_url",
                image_url = new { url = $"data:image/png;base64,{Convert.ToBase64String(matBytes)}" }
            });
        }

        var requestBody = new
        {
            model = model,
            prompt = promptText,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = contentList.ToArray()
                }
            },
            size = "3840x2160",
            quality = "hd",
            response_format = "b64_json"
        };

        progress?.Report($"Mengirim permintaan render ke xAI Grok ({model})...");

        string requestJson = JsonSerializer.Serialize(requestBody);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.x.ai/v1/images/generations")
        {
            Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        HttpResponseMessage response;
        try
        {
            response = await HttpClient.SendAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            return RenderResult.Fail($"Koneksi ke endpoint xAI Grok gagal: {ex.Message}");
        }

        string responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return RenderResult.Fail($"xAI Grok error (Status {(int)response.StatusCode}): {responseContent}");
        }

        progress?.Report("Menyimpan hasil render xAI Grok...");
        string outputPath = Path.Combine(context.OutputDirectory, "output.png");

        try
        {
            using var doc = JsonDocument.Parse(responseContent);
            var root = doc.RootElement;

            if (root.TryGetProperty("data", out var dataArr) && dataArr.GetArrayLength() > 0)
            {
                var item = dataArr[0];
                if (item.TryGetProperty("b64_json", out var b64Elem))
                {
                    byte[] bytes = Convert.FromBase64String(b64Elem.GetString()!);
                    await File.WriteAllBytesAsync(outputPath, bytes, cancellationToken);
                    sw.Stop();
                    return RenderResult.Ok(outputPath, sw.Elapsed);
                }
                if (item.TryGetProperty("url", out var urlElem))
                {
                    byte[] bytes = await HttpClient.GetByteArrayAsync(urlElem.GetString()!, cancellationToken);
                    await File.WriteAllBytesAsync(outputPath, bytes, cancellationToken);
                    sw.Stop();
                    return RenderResult.Ok(outputPath, sw.Elapsed);
                }
            }

            return RenderResult.Fail($"Respon xAI Grok tidak menyertakan gambar yang valid: {responseContent[..Math.Min(400, responseContent.Length)]}");
        }
        catch (Exception ex)
        {
            return RenderResult.Fail($"Gagal memproses data gambar xAI Grok: {ex.Message}");
        }
    }
}
