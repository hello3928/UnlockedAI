namespace UnlockedAI.Core.Tools;

public enum ApprovalDecision
{
    AllowOnce,

    /// <summary>Allow this tool for the rest of the chat without asking again.</summary>
    AllowForChat,

    Deny,
}

/// <summary>Asks the user whether a risky tool call may run, and waits for the answer.</summary>
public interface IApprovalGate
{
    /// <param name="callId">Identifies the call; the same id is in the <c>ToolStarted</c> event shown to the user.</param>
    Task<ApprovalDecision> RequestAsync(int callId, CancellationToken cancellationToken);
}
