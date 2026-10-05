using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UnlockedAI.Core.Ollama;
using UnlockedAI.ViewModels;

namespace UnlockedAI.Controls;

/// <summary>Dropdown of installed models. Each row shows the model's size and whether it can use tools.</summary>
public sealed partial class ModelPicker : UserControl
{
    public static readonly DependencyProperty ModelsProperty =
        Dp.Register<ModelPicker, IReadOnlyList<ModelInfo>?>(nameof(Models), null);

    public static readonly DependencyProperty SelectedModelProperty =
        Dp.Register<ModelPicker, ModelInfo?>(nameof(SelectedModel), null);

    public ModelPicker()
    {
        InitializeComponent();
    }

    public IReadOnlyList<ModelInfo>? Models
    {
        get => (IReadOnlyList<ModelInfo>?)GetValue(ModelsProperty);
        set => SetValue(ModelsProperty, value);
    }

    public ModelInfo? SelectedModel
    {
        get => (ModelInfo?)GetValue(SelectedModelProperty);
        set => SetValue(SelectedModelProperty, value);
    }

    public static string DisplayName(string modelName) => ChatViewModel.ShortName(modelName);

    public static string Detail(string? parameterSize, bool supportsTools) => (parameterSize, supportsTools) switch
    {
        (null or "", true) => "tools",
        (null or "", false) => "",
        (_, true) => $"{parameterSize} · tools",
        (_, false) => parameterSize,
    };
}
