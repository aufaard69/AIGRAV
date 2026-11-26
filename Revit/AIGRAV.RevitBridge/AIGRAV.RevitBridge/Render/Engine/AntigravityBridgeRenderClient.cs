using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AIGRAV.RevitBridge.Render.Core;

namespace AIGRAV.RevitBridge.Render.Engine;

public class AntigravityBridgeRenderClient : IAigravRenderDispatcher
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(5) };

    public async Task<RenderResult> DispatchAsync(
        RenderJobContext context,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        progress?.Report("Menyiapkan paket render job di .aigrav/render_jobs...");

        Directory.CreateDirectory(context.OutputDirectory);

        // Copy style reference if specified
        string? localStyleRef = null;
        if (!string.IsNullOrWhiteSpace(context.StyleReferencePath) && File.Exists(context.StyleReferencePath))
        {
            localStyleRef = Path.Combine(context.OutputDirectory, "style_reference.png");
            if (!string.Equals(context.StyleReferencePath, localStyleRef, StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(context.StyleReferencePath, localStyleRef, overwrite: true);
            }
        }

        // Prepare job descriptor
        string expectedOutputFile = Path.Combine(context.OutputDirectory, "output.png");
        string jobJsonPath = Path.Combine(context.OutputDirectory, "job.json");

        var jobDescriptor = new
        {
            job_id = context.JobId,
            status = "PENDING_AGENT_RENDER",
            view_name = context.ViewName,
            view_id = context.ViewId,
            preset = context.SelectedPreset.DisplayName,
            prompt_file = Path.GetFileName(context.RenderPromptMdPath),
            geometry_pass = Path.GetFileName(context.GeometryPassPath),
            lighting_pass = Path.GetFileName(context.LightingPassPath),
            material_id_pass = Path.GetFileName(context.MaterialIdCleanPath),
            style_reference = localStyleRef != null ? Path.GetFileName(localStyleRef) : null,
            expected_output = "output.png",
            created_at = DateTime.UtcNow.ToString("o")
        };

        File.WriteAllText(jobJsonPath, JsonSerializer.Serialize(jobDescriptor, new JsonSerializerOptions { WriteIndented = true }));

        progress?.Report("Mengirim sinyal render ke AIGRAV Bridge...");

        // Notify local bridge HTTP endpoint if active
        try
        {
            var bridgePayload = new
            {
                tool = "aigrav_render_job_dispatched",
                argument = JsonSerializer.Serialize(new
                {
                    job_id = context.JobId,
                    job_directory = context.OutputDirectory,
                    prompt_file = context.RenderPromptMdPath,
                    view_name = context.ViewName
                })
            };

            var content = new StringContent(
                JsonSerializer.Serialize(bridgePayload),
                Encoding.UTF8,
                "application/json");

            await HttpClient.PostAsync("http://127.0.0.1:8765/", content, cancellationToken);
        }
        catch
        {
            // Bridge HTTP might be running or agent monitors filesystem directly
        }

        // If output.png already exists (e.g. pre-rendered), return immediately
        if (File.Exists(expectedOutputFile) && IsFileReady(expectedOutputFile))
        {
            sw.Stop();
            progress?.Report("Render output ditemukan!");
            return RenderResult.Ok(expectedOutputFile, sw.Elapsed);
        }

        progress?.Report("Menghubungkan ke Antigravity Agent (memantau output.png)...");

        // Watch directory using FileSystemWatcher + polling
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var watcher = new FileSystemWatcher(context.OutputDirectory, "output*.png")
        {
            EnableRaisingEvents = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
        };

        void OnFileDetected(string path)
        {
            if (IsFileReady(path))
            {
                tcs.TrySetResult(path);
            }
        }

        watcher.Created += (_, e) => OnFileDetected(e.FullPath);
        watcher.Changed += (_, e) => OnFileDetected(e.FullPath);

        using var pollCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var pollTask = Task.Run(async () =>
        {
            while (!pollCts.Token.IsCancellationRequested && !tcs.Task.IsCompleted)
            {
                if (File.Exists(expectedOutputFile) && IsFileReady(expectedOutputFile))
                {
                    tcs.TrySetResult(expectedOutputFile);
                    break;
                }

                string jpgFallback = Path.Combine(context.OutputDirectory, "output.jpg");
                if (File.Exists(jpgFallback) && IsFileReady(jpgFallback))
                {
                    tcs.TrySetResult(jpgFallback);
                    break;
                }

                await Task.Delay(500, pollCts.Token);
            }
        }, pollCts.Token);

        try
        {
            // Check for 3 seconds if an external agent picks up
            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(3), cancellationToken));

            if (completedTask == tcs.Task)
            {
                string renderedFile = await tcs.Task;
                sw.Stop();
                progress?.Report("Render selesai dari Antigravity Agent!");
                return RenderResult.Ok(renderedFile, sw.Elapsed);
            }

            // Fallback: If no agent created output within 3s, check if an API key is available to synthesize automatically
            string? availableKey = context.ApiKey
                ?? AigravKeyStorage.GetKey("openai")
                ?? AigravKeyStorage.GetKey("gemini")
                ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");

            if (!string.IsNullOrWhiteSpace(availableKey))
            {
                progress?.Report("Agent Bridge aktif. Mensintesis render 4K via AI Engine...");
                var router = new DirectApiRenderRouter();
                if (AigravKeyStorage.GetKey("openai") != null && string.IsNullOrWhiteSpace(context.ApiKey))
                {
                    context.ModelName = "gpt-image-2.6-sunburst";
                    context.Provider = DirectApiProvider.OpenAI;
                }

                return await router.DispatchAsync(context, progress, cancellationToken);
            }

            // If no API key configured, wait for external agent up to 10 minutes
            progress?.Report("Menunggu output.png dari Antigravity Agent... (Atau masukkan API Key di BYOK Mode)");
            var agentWaitTask = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromMinutes(10), cancellationToken));

            if (agentWaitTask == tcs.Task)
            {
                string renderedFile = await tcs.Task;
                sw.Stop();
                progress?.Report("Render completed successfully!");
                return RenderResult.Ok(renderedFile, sw.Elapsed);
            }

            return RenderResult.Fail("Rendering timeout menunggu Antigravity Agent. Gunakan mode Direct API Key (BYOK) untuk render langsung.");
        }
        catch (OperationCanceledException)
        {
            return RenderResult.Fail("Render operation was cancelled by user.");
        }
        finally
        {
            pollCts.Cancel();
        }
    }

    private static bool IsFileReady(string filename)
    {
        try
        {
            if (!File.Exists(filename)) return false;
            var fi = new FileInfo(filename);
            if (fi.Length == 0) return false;

            using var stream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return stream.Length > 1024;
        }
        catch
        {
            return false;
        }
    }
}
