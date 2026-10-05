using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using UnlockedAI.Controls;
using UnlockedAI.ViewModels;
using Windows.System;

namespace UnlockedAI.Views;

/// <summary>The left column: New chat, the saved chats, and Settings.</summary>
public sealed partial class SidebarView : UserControl
{
    private ShellViewModel? _viewModel;

    // The chat the context menu was opened for.
    private ConversationItemViewModel? _menuTarget;

    public SidebarView()
    {
        InitializeComponent();
    }

    public event EventHandler? SettingsRequested;

    public ShellViewModel? ViewModel
    {
        get => _viewModel;
        set
        {
            _viewModel = value;
            Bindings.Update();
        }
    }

    private MenuFlyout ItemMenu => (MenuFlyout)Resources["ItemMenu"];

    private void OnListContextRequested(UIElement sender, ContextRequestedEventArgs args)
    {
        // Right-click reports the element under the pointer; the keyboard menu key reports the focused row.
        var source = args.OriginalSource as FrameworkElement;
        _menuTarget = source?.DataContext as ConversationItemViewModel
            ?? (source as ListViewItem)?.Content as ConversationItemViewModel;
        if (_menuTarget is null || source is null)
        {
            return;
        }

        if (args.TryGetPosition(source, out var point))
        {
            ItemMenu.ShowAt(source, new FlyoutShowOptions { Position = point });
        }
        else
        {
            ItemMenu.ShowAt(source);
        }

        args.Handled = true;
    }

    private async void OnListKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (ViewModel?.Selected is not { } selected)
        {
            return;
        }

        switch (e.Key)
        {
            case VirtualKey.F2:
                e.Handled = true;
                await RenameAsync(selected);
                break;

            case VirtualKey.Delete:
                e.Handled = true;
                await DeleteAsync(selected);
                break;
        }
    }

    private async void OnRenameClick(object sender, RoutedEventArgs e)
    {
        if (_menuTarget is { } target)
        {
            await RenameAsync(target);
        }
    }

    private async void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (_menuTarget is { } target)
        {
            await DeleteAsync(target);
        }
    }

    private async Task RenameAsync(ConversationItemViewModel item)
    {
        var title = await PromptDialog.AskAsync(XamlRoot, "Rename chat", "Name", item.Title, "Rename");
        if (title is not null && ViewModel is { } viewModel)
        {
            await viewModel.RenameAsync(item, title);
        }
    }

    private async Task DeleteAsync(ConversationItemViewModel item)
    {
        var confirmed = await ConfirmDialog.AskAsync(
            XamlRoot,
            "Delete this chat?",
            $"\"{item.Title}\" and its messages will be deleted. This can't be undone.",
            "Delete",
            isDestructive: true);

        if (confirmed && ViewModel is { } viewModel)
        {
            await viewModel.DeleteAsync(item);
        }
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke(this, EventArgs.Empty);
}
