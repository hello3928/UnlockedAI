using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using UnlockedAI.Core.Errors;

namespace UnlockedAI;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        // The app is dark only, so the caption buttons must be light even when Windows is in light mode.
        AppWindow.TitleBar.PreferredTheme = TitleBarTheme.Dark;

        SampleError.Error = AppError.OllamaUnreachable("http://localhost:11434");
    }
}
