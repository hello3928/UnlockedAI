using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace UnlockedAI.Controls;

/// <summary>Reads design tokens from Theme/*.xaml for the few places that need them in code.</summary>
internal static class Tokens
{
    public static double Number(string key) => (double)Application.Current.Resources[key];

    public static Brush Brush(string key) => (Brush)Application.Current.Resources[key];

    public static Style Style(string key) => (Style)Application.Current.Resources[key];
}
