using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using UnlockedAI.Platform;

namespace UnlockedAI.Controls;

/// <summary>
/// Copies <see cref="Text"/> to the clipboard when clicked, then shows a tick for a moment so the
/// user can see it worked. Set <see cref="IconButton.Label"/> to say what gets copied.
/// </summary>
public sealed partial class CopyButton : IconButton
{
    public static readonly DependencyProperty TextProperty =
        Dp.Register<CopyButton, string>(nameof(Text), "");

    private static readonly TimeSpan ConfirmationTime = TimeSpan.FromSeconds(1.5);

    private DispatcherQueueTimer? _revert;
    private string _restingLabel = "";

    public CopyButton()
    {
        Icon = AppIcon.Copy;
        IsCompact = true;
        Click += OnClick;
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    private void OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(Text))
        {
            return;
        }

        ClipboardService.CopyText(Text);

        if (_revert is null)
        {
            _revert = DispatcherQueue.CreateTimer();
            _revert.Interval = ConfirmationTime;
            _revert.IsRepeating = false;
            _revert.Tick += (_, _) => ShowResting();
        }

        if (!_revert.IsRunning)
        {
            _restingLabel = Label;
        }

        Icon = AppIcon.Check;
        Label = "Copied";
        _revert.Stop();
        _revert.Start();
    }

    private void ShowResting()
    {
        Icon = AppIcon.Copy;
        Label = _restingLabel;
    }
}
