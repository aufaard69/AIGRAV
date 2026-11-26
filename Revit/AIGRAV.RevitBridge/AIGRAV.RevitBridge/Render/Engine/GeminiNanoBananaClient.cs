using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AIGRAV.RevitBridge.Render.Core;

namespace AIGRAV.RevitBridge.Render.Engine;

public class GeminiNanoBananaClient : IAigravRenderDispatcher
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromMinutes(5) };

    public async Task<RenderResult> DispatchAsync(
        RenderJobContext context,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        string apiKey = context.ApiKey?.Trim()
            ?? AigravKeyStorage.GetKey("gemini")
            ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY")
            ?? string.Empty;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return RenderResult.Fail("API Key untuk Google Gemini belum diisi. Masukkan API Key di panel kiri atau simpan di keys.dat.");
        }

        string model = string.IsNullOrWhiteSpace(context.ModelName) ? "gemini-3-pro-image" : context.ModelName;
        progress?.Report($"Mengemas payload multimodal 4K untuk Google Gemini ({model})...");

        string promptText = RenderPromptBuilder.BuildPrompt(context);

        var parts = new List<object>
        {
            new { text = promptText }
        };

        // Attach Pass 1 (Geometry)
        if (File.Exists(context.GeometryPassPath))
        {
            progress?.Report("Encoding Geometry Pass (4K)...");
            byte[] geomBytes = await File.ReadAllBytesAsync(context.GeometryPassPath, cancellationToken);
            parts.Add(new
            {
                inline_data = new
                {
                    mime_type = "image/png",
                    data = Convert.ToBase64String(geomBytes)
                }
            });
        }

        // Attach Pass 2 (Lighting)
        if (File.Exists(context.LightingPassPath))
        {
            progress?.Report("Encoding Base Lighting Pass (4K)...");
            byte[] lightBytes = await File.ReadAllBytesAsync(context.LightingPassPath, cancellationToken);
            parts.Add(new
            {
                inline_data = new
                {
                    mime_type = "image/png",
                    data = Convert.ToBase64String(lightBytes)
                }
            });
        }

        // Attach Pass 3 (Quantized Material ID)
        if (File.Exists(context.MaterialIdCleanPath))
        {
            progress?.Report("Encoding Quantized Material ID Mask (4K)...");
            byte[] matBytes = await File.ReadAllBytesAsync(context.MaterialIdCleanPath, cancellationToken);
            parts.Add(new
            {
                inline_data = new
                {
                    mime_type = "image/png",
                    data = Convert.ToBase64String(matBytes)
                }
            });
        }

        // Optional Style Reference
        if (!string.IsNullOrWhiteSpace(context.StyleReferencePath) && File.Exists(context.StyleReferencePath))
        {
            progress?.Report("Encoding Style Reference Image...");
            byte[] styleBytes = await File.ReadAllBytesAsync(context.StyleReferencePath, cancellationToken);
            string mime = context.StyleReferencePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg";
            parts.Add(new
            {
                inline_data = new
                {
                    mime_type = mime,
                    data = Convert.ToBase64String(styleBytes)
                }
            });
        }

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = parts.ToArray()
                }
            },
            generationConfig = new
            {
                responseModalities = new[] { "IMAGE", "TEXT" },
                imageConfig = new
                {
                    imageSize = "4K"
                }
            }
        };

        string url = $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(model)}:generateContent?key={apiKey}";

        progress?.Report($"Mengirim permintaan ke Google GenAI Endpoint ({model})...");

        string requestJson = JsonSerializer.Serialize(requestBody);
        using var requestContent = new StringContent(requestJson, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await HttpClient.PostAsync(url, requestContent, cancellationToken);
        }
        catch (Exception ex)
        {
            return RenderResult.Fail($"Koneksi ke endpoint Gemini gagal: {ex.Message}");
        }

        string responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return RenderResult.Fail($"Gemini API error (Status {(int)response.StatusCode}): {responseContent}");
        }

        progress?.Report("Mendekode hasil render 4K dari respon Gemini...");

        string outputPath = Path.Combine(context.OutputDirectory, "output.png");

        try
        {
            using var doc = JsonDocument.Parse(responseContent);
            var root = doc.RootElement;

            if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            {
                return RenderResult.Fail("Respon Gemini tidak memiliki candidate.");
            }

            var firstCandidate = candidates[0];
            if (!firstCandidate.TryGetProperty("content", out var contentElem) ||
                !contentElem.TryGetProperty("parts", out var resParts))
            {
                return RenderResult.Fail("Respon Gemini tidak memiliki parts gambar.");
            }

            bool imageFound = false;

            foreach (var part in resParts.EnumerateArray())
            {
                if (part.TryGetProperty("inlineData", out var inlineData) ||
                    part.TryGetProperty("inline_data", out inlineData))
                {
                    if (inlineData.TryGetProperty("data", out var dataElem))
                    {
                        string base64 = dataElem.GetString() ?? string.Empty;
                        if (!string.IsNullOrEmpty(base64))
                        {
                            byte[] imgBytes = Convert.FromBase64String(base64);
                            await File.WriteAllBytesAsync(outputPath, imgBytes, cancellationToken);
                            imageFound = true;
                            break;
                        }
                    }
                }
            }

            if (!imageFound)
            {
                return RenderResult.Fail($"Respon Gemini tidak menyertakan data gambar inline: {responseContent[..Math.Min(500, responseContent.Length)]}");
            }

            sw.Stop();
            progress?.Report("Render Gemini 4K selesai dengan sukses!");
            return RenderResult.Ok(outputPath, sw.Elapsed);
        }
        catch (Exception ex)
        {
            return RenderResult.Fail($"Gagal memproses data gambar dari Gemini: {ex.Message}");
        }
    }
}
