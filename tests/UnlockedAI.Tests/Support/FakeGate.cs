using UnlockedAI.Core.Tools;

namespace UnlockedAI.Tests.Support;

/// <summary>Answers approval requests from a prepared list. With nothing prepared, it waits until cancelled.</summary>
internal sealed class FakeGate(params ApprovalDecision[] decisions) : IApprovalGate
{
    private readonly Queue<ApprovalDecision> _decisions = new(decisions);

    /// <summary>The call ids that were asked about, in order.</summary>
    public List<int> Asked { get; } = [];

    public async Task<ApprovalDecision> RequestAsync(int callId, CancellationToken cancellationToken)
    {
        Asked.Add(callId);
        if (_decisions.TryDequeue(out var decision))
        {
            return decision;
        }

        await Task.Delay(Timeout.Infinite, cancellationToken);
        throw new InvalidOperationException("Unreachable: the delay only ends by cancellation.");
    }
}
