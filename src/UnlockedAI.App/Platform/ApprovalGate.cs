using UnlockedAI.Core.Tools;

namespace UnlockedAI.Platform;

/// <summary>
/// Connects the chat session, which waits for a decision on a worker thread, to the buttons on a
/// tool call's card, which the user clicks on the UI thread.
/// </summary>
public sealed class ApprovalGate : IApprovalGate
{
    private readonly Dictionary<int, TaskCompletionSource<ApprovalDecision>> _waiting = [];
    private readonly Lock _gate = new();

    public async Task<ApprovalDecision> RequestAsync(int callId, CancellationToken cancellationToken)
    {
        var pending = new TaskCompletionSource<ApprovalDecision>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            _waiting[callId] = pending;
        }

        try
        {
            // Stopping the reply ends the wait.
            using (cancellationToken.Register(() => pending.TrySetCanceled(cancellationToken)))
            {
                return await pending.Task.ConfigureAwait(false);
            }
        }
        finally
        {
            lock (_gate)
            {
                _waiting.Remove(callId);
            }
        }
    }

    /// <summary>Gives the user's answer for a waiting call. Does nothing if that call is no longer waiting.</summary>
    public void Resolve(int callId, ApprovalDecision decision)
    {
        lock (_gate)
        {
            if (_waiting.TryGetValue(callId, out var pending))
            {
                pending.TrySetResult(decision);
            }
        }
    }
}
