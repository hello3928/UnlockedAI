using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace UnlockedAI.Controls;

/// <summary>
/// A square, icon-only button. <see cref="Label"/> is required: it becomes the tooltip and the
/// name screen readers announce, since the button shows no text of its own.
/// </summary>
public sealed partial class IconButton : AppButton
{
    public static readonly DependencyProperty LabelProperty =
        Dp.Register<IconButton, string>(nameof(Label), "", (button, label) => button.ApplyLabel(label));

    public static readonly DependencyProperty IsCompactProperty =
        Dp.Register<IconButton, bool>(nameof(IsCompact), false, (button, _) => button.ApplySize());

    public IconButton()
    {
        Variant = ButtonVariant.Ghost;
        Padding = new Thickness(0);
        ApplySize();
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Uses the smaller control height, for buttons nested inside another control.</summary>
    public bool IsCompact
    {
        get => (bool)GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
    }

    private void ApplyLabel(string label)
    {
        AutomationProperties.SetName(this, label);
        ToolTipService.SetToolTip(this, label);
    }

    private void ApplySize()
    {
        var size = Tokens.Number(IsCompact ? "ControlHeightCompact" : "ControlHeight");
        Width = size;
        Height = size;
        MinHeight = size;
    }
}
