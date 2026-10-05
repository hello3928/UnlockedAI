using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UnlockedAI.Core.Models;
using UnlockedAI.Core.Tools;
using UnlockedAI.Platform;

namespace UnlockedAI.ViewModels;

public enum ToolCallState
{
    /// <summary>Waiting for the user to allow or deny it.</summary>
    AwaitingApproval,
    Running,
    Succeeded,
    Failed,

    /// <summary>The user chose not to let it run.</summary>
    Denied,

    /// <summary>The reply was stopped before this call finished.</summary>
    Stopped,
}

/// <summary>One use of a tool by the model, as a card in the chat.</summary>
public sealed partial class ToolCallViewModel : ChatItemViewModel
{
    private readonly ApprovalGate? _gate;

    /// <param name="gate">Receives the user's decision. Null for a call loaded from a saved chat, which is already over.</param>
    public ToolCallViewModel(int callId, string toolName, string title, string summary, ToolCallState state, ApprovalGate? gate = null)
    {
        CallId = callId;
        ToolName = toolName;
        Title = title;
        Summary = summary;
        State = state;
        Output = "";
        _gate = gate;
    }

    public int CallId { get; }

    public string ToolName { get; }

    /// <summary>The tool's name as the user knows it, such as "Run command".</summary>
    public string Title { get; }

    /// <summary>What this call does: the command, the address, the path.</summary>
    public string Summary { get; }

    public bool IsAwaitingApproval => State == ToolCallState.AwaitingApproval;

    public bool IsRunning => State == ToolCallState.Running;

    public bool HasOutput => Output.Length > 0;

    public string StatusText => State switch
    {
        ToolCallState.AwaitingApproval => "Waiting for you",
        ToolCallState.Running => "Running",
        ToolCallState.Succeeded => "Done",
        ToolCallState.Failed => "Failed",
        ToolCallState.Denied => "Not allowed",
        _ => "Stopped",
    };

    public string OutputToggleText => IsOutputVisible ? "Hide result" : "Show result";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAwaitingApproval))]
    [NotifyPropertyChangedFor(nameof(IsRunning))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    public partial ToolCallState State { get; set; }

    /// <summary>What the tool gave back to the model.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOutput))]
    public partial string Output { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OutputToggleText))]
    public partial bool IsOutputVisible { get; set; }

    public static ToolCallState StateFor(ToolOutcome outcome) => outcome switch
    {
        ToolOutcome.Succeeded => ToolCallState.Succeeded,
        ToolOutcome.Denied => ToolCallState.Denied,
        _ => ToolCallState.Failed,
    };

    /// <summary>List rows are announced by screen readers using this text.</summary>
    public override string ToString() => $"{Title}: {Summary}. {StatusText}.";

    [RelayCommand]
    private void AllowOnce() => _gate?.Resolve(CallId, ApprovalDecision.AllowOnce);

    [RelayCommand]
    private void AllowForChat() => _gate?.Resolve(CallId, ApprovalDecision.AllowForChat);

    [RelayCommand]
    private void Deny() => _gate?.Resolve(CallId, ApprovalDecision.Deny);

    [RelayCommand]
    private void ToggleOutput() => IsOutputVisible = !IsOutputVisible;
}
