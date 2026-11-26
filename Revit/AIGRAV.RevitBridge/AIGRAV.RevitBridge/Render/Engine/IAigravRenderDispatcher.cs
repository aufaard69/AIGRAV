using System;
using System.Threading;
using System.Threading.Tasks;
using AIGRAV.RevitBridge.Render.Core;

namespace AIGRAV.RevitBridge.Render.Engine;

public interface IAigravRenderDispatcher
{
    Task<RenderResult> DispatchAsync(
        RenderJobContext context,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);
}
