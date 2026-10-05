using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace UnlockedAI.Controls;

/// <summary>
/// One labelled field in a form: label, the input itself as content, optional helper text and
/// validation text. The label is wired to the input for screen readers.
/// </summary>
public sealed partial class FormRow : ContentControl
{
    public static readonly DependencyProperty LabelProperty =
        Dp.Register<FormRow, string>(nameof(Label), "");

    public static readonly DependencyProperty DescriptionProperty =
        Dp.Register<FormRow, string>(nameof(Description), "", (row, _) => row.UpdateParts());

    public static readonly DependencyProperty ErrorProperty =
        Dp.Register<FormRow, string>(nameof(Error), "", (row, _) => row.UpdateParts());

    private TextBlock? _label;
    private TextBlock? _description;
    private TextBlock? _error;

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <summary>Validation message. Empty means the field is valid.</summary>
    public string Error
    {
        get => (string)GetValue(ErrorProperty);
        set => SetValue(ErrorProperty, value);
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _label = GetTemplateChild("PART_Label") as TextBlock;
        _description = GetTemplateChild("PART_Description") as TextBlock;
        _error = GetTemplateChild("PART_Error") as TextBlock;
        UpdateParts();
    }

    protected override void OnContentChanged(object oldContent, object newContent)
    {
        base.OnContentChanged(oldContent, newContent);
        UpdateParts();
    }

    private void UpdateParts()
    {
        if (_description is not null)
        {
            _description.Visibility = Visible.IfText(Description);
        }

        if (_error is not null)
        {
            _error.Visibility = Visible.IfText(Error);
        }

        if (_label is not null && Content is DependencyObject input)
        {
            AutomationProperties.SetLabeledBy(input, _label);
        }
    }
}
