using System.Collections.Concurrent;
using System.Text.Json;

namespace Filee.Web.Streaming;

public sealed record SseOptions(int BufferSize = 200, int ThrottleMs = 150, int HeartbeatSeconds = 15);

public sealed record SseEvent(long Id, string Event, string Data);

/// <summary>
/// In-memory per-job event ring. The Web layer never reads progress from SQLite; SQLite holds durable summaries only.
/// </summary>
public sealed class ProgressHub
{
    private sealed class JobEvents
    {
        public long NextId;
        public Queue<SseEvent> Buffer { get; } = new();
        public string? LastStatus;
        public double LastProgress;
        public DateTimeOffset LastPublishedAt;
    }

    private readonly ConcurrentDictionary<string, JobEvents> _jobs = new();
    private readonly SseOptions _options;

    public ProgressHub(SseOptions? options = null) => _options = options ?? new SseOptions();

    public SseEvent? Publish(string jobId, string status, double progress, bool force = false)
    {
        var state = _jobs.GetOrAdd(jobId, _ => new JobEvents());
        lock (state)
        {
            var now = DateTimeOffset.UtcNow;
            if (!force
                && state.LastStatus == status
                && Math.Abs(progress - state.LastProgress) < 0.0001
                && (now - state.LastPublishedAt).TotalMilliseconds < _options.ThrottleMs)
                return null;

            var sseEvent = new SseEvent(
                ++state.NextId,
                IsTerminal(status) ? "done" : "progress",
                JsonSerializer.Serialize(new { jobId, status, progress }));

            state.Buffer.Enqueue(sseEvent);
            while (state.Buffer.Count > _options.BufferSize)
                state.Buffer.Dequeue();

            state.LastStatus = status;
            state.LastProgress = progress;
            state.LastPublishedAt = now;
            return sseEvent;
        }
    }

    public IReadOnlyList<SseEvent> GetSince(string jobId, long? lastEventId)
    {
        if (!_jobs.TryGetValue(jobId, out var state))
            return [];

        lock (state)
        {
            var events = state.Buffer.ToArray();
            if (events.Length == 0 || lastEventId is null)
                return events;

            // A missing old id means the client missed the window; replay everything still buffered.
            if (lastEventId < events[0].Id - 1)
                return events;

            return events.Where(item => item.Id > lastEventId.Value).ToArray();
        }
    }

    private static bool IsTerminal(string status) =>
        status is "completed" or "completed_with_errors" or "failed" or "cancelled" or "interrupted";
}
