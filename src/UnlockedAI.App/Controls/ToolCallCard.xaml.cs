using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using UnlockedAI.ViewModels;

namespace UnlockedAI.Controls;

/// <summary>
/// One tool call in the chat: which tool, exactly what it was asked to do, how it went, and its result.
/// While a call is waiting for approval, the card carries the Allow and Don't allow buttons.
/// </summary>
public sealed partial class ToolCallCard : UserControl
{
    public static readonly DependencyProperty CallProperty =
        Dp.Register<ToolCallCard, ToolCallViewModel?>(nameof(Call), null);

    public ToolCallCard()
    {
        InitializeComponent();
    }

    public ToolCallViewModel? Call
    {
        get => (ToolCallViewModel?)GetValue(CallProperty);
        set => SetValue(CallProperty, value);
    }

    public static AppIcon ToolIcon(string? toolName) => toolName switch
    {
        "web_search" => AppIcon.Search,
        "web_fetch" => AppIcon.Globe,
        "http_request" => AppIcon.Link,
        "list_directory" => AppIcon.Folder,
        "read_file" => AppIcon.Document,
        "write_file" => AppIcon.Edit,
        "run_command" => AppIcon.Command,
        _ => AppIcon.Tool,
    };

    public static AppIcon StatusIcon(ToolCallState state) => state switch
    {
        ToolCallState.AwaitingApproval => AppIcon.Shield,
        ToolCallState.Succeeded => AppIcon.Check,
        ToolCallState.Failed => AppIcon.Error,
        ToolCallState.Denied or ToolCallState.Stopped => AppIcon.Close,
        _ => AppIcon.None,
    };

    public static Brush StatusBrush(ToolCallState state) => Tokens.Brush(state switch
    {
        ToolCallState.AwaitingApproval => "WarningBrush",
        ToolCallState.Succeeded => "SuccessBrush",
        ToolCallState.Failed => "ErrorBrush",
        _ => "TextTertiaryBrush",
    });

    public static AppIcon ToggleIcon(bool isOpen) => isOpen ? AppIcon.ChevronDown : AppIcon.ChevronRight;
}
