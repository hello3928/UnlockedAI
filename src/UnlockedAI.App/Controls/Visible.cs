using Microsoft.UI.Xaml;

namespace UnlockedAI.Controls;

/// <summary>Visibility helpers for x:Bind, e.g. <c>Visibility="{x:Bind ui:Visible.If(IsBusy), Mode=OneWay}"</c>.</summary>
public static class Visible
{
    public static Visibility If(bool condition) => condition ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility IfNot(bool condition) => condition ? Visibility.Collapsed : Visibility.Visible;

    public static Visibility IfText(string? text) => If(!string.IsNullOrEmpty(text));

    public static Visibility IfSet(object? value) => If(value is not null);
}
