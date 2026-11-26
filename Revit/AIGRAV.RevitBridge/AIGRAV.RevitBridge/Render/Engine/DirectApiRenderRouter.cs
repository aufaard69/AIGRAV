using System;
using System.Threading;
using System.Threading.Tasks;
using AIGRAV.RevitBridge.Render.Core;

namespace AIGRAV.RevitBridge.Render.Engine;

public class DirectApiRenderRouter : IAigravRenderDispatcher
{
    private readonly GeminiNanoBananaClient _geminiClient = new();
    private readonly OpenAiGptImageClient _openAiClient = new();
    private readonly XaiGrokImagineClient _grokClient = new();

    public Task<RenderResult> DispatchAsync(
        RenderJobContext context,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        string model = context.ModelName.ToLowerInvariant();

        if (model.Contains("gemini") || context.Provider == DirectApiProvider.Gemini)
        {
            return _geminiClient.DispatchAsync(context, progress, cancellationToken);
        }

        if (model.Contains("gpt") || model.Contains("openai") || context.Provider == DirectApiProvider.OpenAI)
        {
            return _openAiClient.DispatchAsync(context, progress, cancellationToken);
        }

        if (model.Contains("grok") || model.Contains("xai") || context.Provider == DirectApiProvider.Grok)
        {
            return _grokClient.DispatchAsync(context, progress, cancellationToken);
        }

        // Default fallback to Gemini Nano Banana
        return _geminiClient.DispatchAsync(context, progress, cancellationToken);
    }
}
