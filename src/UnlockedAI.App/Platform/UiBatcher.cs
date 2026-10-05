using System.Collections.Concurrent;
using Microsoft.UI.Dispatching;

namespace UnlockedAI.Platform;

/// <summary>
/// Collects items posted from any thread and applies them on the UI thread in batches, in order.
/// The UI catches up once per screen frame: often enough that each word appears as it arrives,
/// without doing more work than the screen can show.
/// </summary>
internal sealed class UiBatcher<T>
{
    // One frame at 60 Hz. At 50 ms, words visibly arrived in clumps.
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(16);

    private readonly ConcurrentQueue<T> _pending = new();
    private readonly DispatcherQueueTimer _timer;
    private readonly Action<T> _apply;
    private readonly Action _afterBatch;

    /// <param name="apply">Called on the UI thread for each item.</param>
    /// <param name="afterBatch">Called on the UI thread once after each non-empty batch.</param>
    public UiBatcher(DispatcherQueue dispatcher, Action<T> apply, Action afterBatch)
    {
        _apply = apply;
        _afterBatch = afterBatch;
        _timer = dispatcher.CreateTimer();
        _timer.Interval = Interval;
        _timer.Tick += (_, _) => Drain();
    }

    /// <summary>Safe to call from any thread.</summary>
    public void Post(T item) => _pending.Enqueue(item);

    public void Start() => _timer.Start();

    /// <summary>Stops the timer and applies whatever is still queued. Call on the UI thread.</summary>
    public void Stop()
    {
        _timer.Stop();
        Drain();
    }

    private void Drain()
    {
        try
        {
            var any = false;
            while (_pending.TryDequeue(out var item))
            {
                _apply(item);
                any = true;
            }

            if (any)
            {
                _afterBatch();
            }
        }
        catch (Exception exception)
        {
            // An exception escaping a timer callback ends the process without passing through
            // Application.UnhandledException, so this is the only chance to record it.
            CrashLog.Write(exception);
            throw;
        }
    }
}
