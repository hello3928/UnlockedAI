using CommunityToolkit.Mvvm.ComponentModel;
using UnlockedAI.Core.Tools;

namespace UnlockedAI.ViewModels;

/// <summary>One tool in Settings, with a checkbox for whether the model may use it.</summary>
public sealed partial class ToolToggleViewModel : ObservableObject
{
    public ToolToggleViewModel(ITool tool, bool isEnabled)
    {
        Name = tool.Name;
        Title = tool.Title;
        Summary = tool.Summary;
        IsEnabled = isEnabled;
    }

    public string Name { get; }

    public string Title { get; }

    public string Summary { get; }

    [ObservableProperty]
    public partial bool IsEnabled { get; set; }

    public override string ToString() => Title;
}
