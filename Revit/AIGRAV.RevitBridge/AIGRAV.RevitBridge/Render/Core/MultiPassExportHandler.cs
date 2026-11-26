using System;
using System.Threading.Tasks;
using Autodesk.Revit.UI;

namespace AIGRAV.RevitBridge.Render.Core;

public class MultiPassExportHandler : IExternalEventHandler
{
    private readonly object _lock = new();
    private Action<UIApplication>? _scheduledAction;

    public void Enqueue(Action<UIApplication> action)
    {
        lock (_lock)
        {
            _scheduledAction = action;
        }
    }

    public Task<T> EnqueueAsync<T>(Func<UIApplication, T> func, ExternalEvent revitEvent)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        Enqueue(app =>
        {
            try
            {
                T result = func(app);
                tcs.TrySetResult(result);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });

        revitEvent.Raise();
        return tcs.Task;
    }

    public void Execute(UIApplication app)
    {
        Action<UIApplication>? actionToRun;
        lock (_lock)
        {
            actionToRun = _scheduledAction;
            _scheduledAction = null;
        }

        try
        {
            actionToRun?.Invoke(app);
        }
        catch (Exception ex)
        {
            TaskDialog.Show("AIGRAV Render Handler Error", ex.Message);
        }
    }

    public string GetName() => "AIGRAV MultiPassExportHandler";
}
