using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using UnlockedAI.Controls;
using UnlockedAI.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace UnlockedAI.Views;

/// <summary>The chat column: error bar, message list and composer.</summary>
public sealed partial class ChatView : UserControl
{
    // How close to the end, in pixels, still counts as "reading the latest message".
    private const double BottomTolerance = 24;

    private ChatViewModel? _viewModel;
    private ScrollViewer? _scroller;
    private bool _followLatest = true;

    public ChatView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    public ChatViewModel? ViewModel
    {
        get => _viewModel;
        set
        {
            if (_viewModel is not null)
            {
                _viewModel.Opened -= OnChatOpened;
                _viewModel.Started -= OnChatStarted;
            }

            _viewModel = value;
            if (_viewModel is not null)
            {
                _viewModel.Opened += OnChatOpened;
                _viewModel.Started += OnChatStarted;
            }

            Bindings.Update();
        }
    }

    public void FocusComposer() => MessageComposer.FocusInput();

    // Focus stays where it is, so the sidebar can be walked with the arrow keys.
    private void OnChatOpened(object? sender, EventArgs e) => ScrollToLatest();

    // A new chat is for typing into, so the caret goes straight to the message box.
    private void OnChatStarted(object? sender, EventArgs e)
    {
        ScrollToLatest();
        FocusComposer();
    }

    /// <summary>Jumps to the newest message and keeps following new text as it arrives.</summary>
    public void ScrollToLatest()
    {
        _followLatest = true;
        ScrollToEnd();
    }

    private static T? FindDescendant<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match)
            {
                return match;
            }

            if (FindDescendant<T>(child) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        _scroller = FindDescendant<ScrollViewer>(MessageList);
        if (_scroller is not null)
        {
            _scroller.ViewChanged += (_, _) => _followLatest = IsAtEnd();
        }

        // The panel grows both when a message is added and when the reply in progress gets longer.
        if (MessageList.ItemsPanelRoot is { } panel)
        {
            panel.SizeChanged += (_, _) =>
            {
                if (_followLatest)
                {
                    ScrollToEnd();
                }
            };
        }

        FocusComposer();
    }

    private bool IsAtEnd() =>
        _scroller is null || _scroller.VerticalOffset >= _scroller.ScrollableHeight - BottomTolerance;

    private void ScrollToEnd() =>
        _scroller?.ChangeView(null, _scroller.ScrollableHeight, null, disableAnimation: true);

    // A row that scrolls out of view waits in a queue to be reused. Until then it would keep its
    // whole rendered message alive, so it is emptied here and filled again when it is next needed.
    private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.InRecycleQueue && args.ItemContainer.ContentTemplateRoot is MessageRow row)
        {
            row.ReleaseContent();
        }
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Attach";
        }
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (ViewModel is not { } viewModel || !e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        // The drop source waits for this handler; the deferral lets it wait for the file list too.
        List<string> paths;
        var deferral = e.GetDeferral();
        try
        {
            var items = await e.DataView.GetStorageItemsAsync();
            paths = items.OfType<StorageFile>().Select(file => file.Path).ToList();
        }
        catch (Exception exception)
        {
            viewModel.Report(exception);
            return;
        }
        finally
        {
            deferral.Complete();
        }

        await viewModel.AddFilesAsync(paths);
        FocusComposer();
    }

    private void OnEscapeInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (ViewModel is { IsGenerating: true } viewModel)
        {
            viewModel.StopCommand.Execute(null);
            args.Handled = true;
        }
    }
}
