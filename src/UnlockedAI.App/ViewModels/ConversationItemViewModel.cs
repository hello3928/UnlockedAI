using CommunityToolkit.Mvvm.ComponentModel;

namespace UnlockedAI.ViewModels;

/// <summary>One saved chat in the sidebar.</summary>
public sealed partial class ConversationItemViewModel : ObservableObject
{
    public ConversationItemViewModel(long id, string title)
    {
        Id = id;
        Title = title;
    }

    public long Id { get; }

    [ObservableProperty]
    public partial string Title { get; set; }

    /// <summary>List rows are announced by screen readers using this text.</summary>
    public override string ToString() => Title;
}
