using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace UnlockedAI.Controls;

public enum ButtonVariant
{
    /// <summary>Neutral fill. The default for most actions.</summary>
    Secondary,

    /// <summary>Accent fill. At most one per view: the main action.</summary>
    Primary,

    /// <summary>No fill until hovered. For low-emphasis and toolbar actions.</summary>
    Ghost,

    /// <summary>For actions that destroy something.</summary>
    Danger,
}

/// <summary>
/// The app's button. Set <see cref="Variant"/> for the look and <see cref="Icon"/> for a leading icon.
/// Its appearance comes from Theme/Buttons.xaml; views never restyle it.
/// </summary>
public partial class AppButton : Button
{
    public static readonly DependencyProperty VariantProperty =
        Dp.Register<AppButton, ButtonVariant>(nameof(Variant), ButtonVariant.Secondary, (button, _) => button.ApplyVariant());

    public static readonly DependencyProperty IconProperty =
        Dp.Register<AppButton, AppIcon>(nameof(Icon), AppIcon.None, (button, _) => button.UpdateParts());

    public static readonly DependencyProperty IsBusyProperty =
        Dp.Register<AppButton, bool>(nameof(IsBusy), false, (button, _) => button.UpdateParts());

    public static readonly DependencyProperty HoverBackgroundProperty =
        Dp.Register<AppButton, Brush?>(nameof(HoverBackground), null);

    public static readonly DependencyProperty PressedBackgroundProperty =
        Dp.Register<AppButton, Brush?>(nameof(PressedBackground), null);

    public static readonly DependencyProperty DisabledBackgroundProperty =
        Dp.Register<AppButton, Brush?>(nameof(DisabledBackground), null);

    private FontIcon? _icon;
    private ProgressRing? _busy;
    private ContentPresenter? _label;

    public AppButton()
    {
        ApplyVariant();
    }

    public ButtonVariant Variant
    {
        get => (ButtonVariant)GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    public AppIcon Icon
    {
        get => (AppIcon)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>Shows a progress ring in place of the icon and ignores clicks while true.</summary>
    public bool IsBusy
    {
        get => (bool)GetValue(IsBusyProperty);
        set => SetValue(IsBusyProperty, value);
    }

    public Brush? HoverBackground
    {
        get => (Brush?)GetValue(HoverBackgroundProperty);
        set => SetValue(HoverBackgroundProperty, value);
    }

    public Brush? PressedBackground
    {
        get => (Brush?)GetValue(PressedBackgroundProperty);
        set => SetValue(PressedBackgroundProperty, value);
    }

    public Brush? DisabledBackground
    {
        get => (Brush?)GetValue(DisabledBackgroundProperty);
        set => SetValue(DisabledBackgroundProperty, value);
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _icon = GetTemplateChild("PART_Icon") as FontIcon;
        _busy = GetTemplateChild("PART_Busy") as ProgressRing;
        _label = GetTemplateChild("PART_Label") as ContentPresenter;
        UpdateParts();
    }

    protected override void OnContentChanged(object oldContent, object newContent)
    {
        base.OnContentChanged(oldContent, newContent);
        UpdateParts();
    }

    private void ApplyVariant() => Style = Tokens.Style($"AppButton{Variant}Style");

    private void UpdateParts()
    {
        var busy = IsBusy;
        IsHitTestVisible = !busy;

        if (_icon is not null)
        {
            _icon.Glyph = Icon.ToGlyph();
            _icon.Visibility = Visible.If(Icon != AppIcon.None && !busy);
        }

        if (_busy is not null)
        {
            _busy.IsActive = busy;
            _busy.Visibility = Visible.If(busy);
        }

        if (_label is not null)
        {
            _label.Visibility = Visible.IfSet(Content);
        }
    }
}
