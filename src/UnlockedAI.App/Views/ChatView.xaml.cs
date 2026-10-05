using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using UnlockedAI.ViewModels;

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
            _viewModel = value;
            Bindings.Update();
        }
    }

    public void FocusComposer() => MessageComposer.FocusInput();

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

    private void OnEscapeInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (ViewModel is { IsGenerating: true } viewModel)
        {
            viewModel.StopCommand.Execute(null);
            args.Handled = true;
        }
    }
}
