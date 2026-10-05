using Microsoft.UI.Xaml;

namespace UnlockedAI.Controls;

/// <summary>Cuts the boilerplate of registering a dependency property.</summary>
internal static class Dp
{
    public static DependencyProperty Register<TOwner, TValue>(
        string name,
        TValue defaultValue,
        Action<TOwner, TValue>? changed = null)
        where TOwner : DependencyObject
    {
        var metadata = changed is null
            ? new PropertyMetadata(defaultValue)
            : new PropertyMetadata(defaultValue, (sender, args) => changed((TOwner)sender, (TValue)args.NewValue));

        return DependencyProperty.Register(name, typeof(TValue), typeof(TOwner), metadata);
    }
}
