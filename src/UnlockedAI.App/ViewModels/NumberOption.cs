namespace UnlockedAI.ViewModels;

/// <summary>One choice in a dropdown that stands for a number, such as "15 minutes" for 15.</summary>
public sealed record NumberOption(int Value, string Label)
{
    /// <summary>Dropdowns and screen readers show this.</summary>
    public override string ToString() => Label;
}
