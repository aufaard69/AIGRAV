using System.Collections.Concurrent;

namespace AIGRAV.RevitBridge;

public static class AIGRAVRequestManager
{
    private static readonly ConcurrentQueue<AIGRAVRequest> Queue = new();

    public static void Enqueue(AIGRAVRequest request)
    {
        Queue.Enqueue(request);
    }

    public static bool TryDequeue(out AIGRAVRequest? request)
    {
        return Queue.TryDequeue(out request);
    }
}