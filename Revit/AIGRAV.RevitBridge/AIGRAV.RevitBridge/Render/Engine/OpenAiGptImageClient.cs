using System;
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

public class OpenAiGptImageClient : IAigravRenderDispatcher
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromMinutes(5) };

    public async Task<RenderResult> DispatchAsync(
        RenderJobContext context,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        string apiKey = context.ApiKey?.Trim()
            ?? AigravKeyStorage.GetKey("openai")
            ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
            ?? string.Empty;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return RenderResult.Fail("API Key untuk OpenAI ChatGPT belum diisi. Masukkan API Key di panel kiri atau simpan di keys.dat.");
        }

        string model = string.IsNullOrWhiteSpace(context.ModelName) ? "gpt-image-2.6-sunburst" : context.ModelName;
        string outputPath = Path.Combine(context.OutputDirectory, "output.png");
        string promptText = RenderPromptBuilder.BuildPrompt(context);

        // Determine guide image for edits (Lighting pass is ideal for architectural realism)
        string? guideImagePath = File.Exists(context.LightingPassPath)
            ? context.LightingPassPath
            : (File.Exists(context.GeometryPassPath) ? context.GeometryPassPath : null);

        // Attempt 1: Images Edits endpoint with guide pass
        if (!string.IsNullOrEmpty(guideImagePath))
        {
            try
            {
                progress?.Report($"Mengirim render ke OpenAI Images Edits ({model})...");
                using var form = new MultipartFormDataContent();
                form.Add(new StringContent(model), "model");
                form.Add(new StringContent(promptText), "prompt");
                form.Add(new StringContent("b64_json"), "response_format");

                byte[] imageBytes = await File.ReadAllBytesAsync(guideImagePath, cancellationToken);
                var imageContent = new ByteArrayContent(imageBytes);
                imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
                form.Add(imageContent, "image", Path.GetFileName(guideImagePath));

                using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/images/edits")
                {
                    Content = form
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                var response = await HttpClient.SendAsync(request, cancellationToken);
                string responseStr = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    if (await TrySaveImageFromResponseAsync(responseStr, outputPath, cancellationToken))
                    {
                        sw.Stop();
                        return RenderResult.Ok(outputPath, sw.Elapsed);
                    }
                }
                else
                {
                    progress?.Report($"Images Edits dialihkan ke endpoint generasi ({response.StatusCode})...");
                }
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                progress?.Report($"Peringatan Images Edits ({ex.Message}), mencoba endpoint generations...");
            }
        }

        // Attempt 2: Images Generations endpoint
        try
        {
            progress?.Report($"Mengirim render ke OpenAI Images Generations ({model})...");

            var genBody = new
            {
                model = model,
                prompt = promptText,
                n = 1,
                response_format = "b64_json"
            };

            string jsonStr = JsonSerializer.Serialize(genBody);
            using var genRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/images/generations")
            {
                Content = new StringContent(jsonStr, Encoding.UTF8, "application/json")
            };
            genRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var genResponse = await HttpClient.SendAsync(genRequest, cancellationToken);
            string genResponseStr = await genResponse.Content.ReadAsStringAsync(cancellationToken);

            if (!genResponse.IsSuccessStatusCode)
            {
                return RenderResult.Fail($"OpenAI API error ({genResponse.StatusCode}): {genResponseStr}");
            }

            if (await TrySaveImageFromResponseAsync(genResponseStr, outputPath, cancellationToken))
            {
                sw.Stop();
                return RenderResult.Ok(outputPath, sw.Elapsed);
            }

            return RenderResult.Fail($"Gagal mengekstrak data gambar OpenAI: {genResponseStr[..Math.Min(300, genResponseStr.Length)]}");
        }
        catch (Exception ex)
        {
            return RenderResult.Fail($"Koneksi ke endpoint OpenAI gagal: {ex.Message}");
        }
    }

    private static async Task<bool> TrySaveImageFromResponseAsync(string jsonResponse, string outputPath, CancellationToken cancellationToken)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonResponse);
            if (doc.RootElement.TryGetProperty("data", out var dataArr) && dataArr.GetArrayLength() > 0)
            {
                var first = dataArr[0];
                if (first.TryGetProperty("b64_json", out var b64Elem))
                {
                    byte[] bytes = Convert.FromBase64String(b64Elem.GetString()!);
                    await File.WriteAllBytesAsync(outputPath, bytes, cancellationToken);
                    return true;
                }
                if (first.TryGetProperty("url", out var urlElem))
                {
                    byte[] bytes = await HttpClient.GetByteArrayAsync(urlElem.GetString()!, cancellationToken);
                    await File.WriteAllBytesAsync(outputPath, bytes, cancellationToken);
                    return true;
                }
            }
            return false;
        }
        catch
        {
            return false;
        }
    }
}
