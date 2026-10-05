namespace UnlockedAI.Controls;

/// <summary>Every icon the app uses. Views name an icon; the glyph code lives only here.</summary>
public enum AppIcon
{
    None,
    Add,
    Attach,
    Chat,
    Check,
    ChevronDown,
    ChevronRight,
    Clock,
    Close,
    Command,
    Copy,
    Delete,
    Document,
    Edit,
    Error,
    Folder,
    Globe,
    Info,
    Link,
    More,
    Retry,
    Search,
    Send,
    Settings,
    Shield,
    Stop,
    Success,
    Tool,
    Warning,
}

public static class AppIconExtensions
{
    /// <summary>The Segoe Fluent Icons code point for an icon, or an empty string for <see cref="AppIcon.None"/>.</summary>
    public static string ToGlyph(this AppIcon icon) => icon switch
    {
        AppIcon.Add => "",
        AppIcon.Attach => "",
        AppIcon.Chat => "",
        AppIcon.Check => "",
        AppIcon.ChevronDown => "",
        AppIcon.ChevronRight => "",
        AppIcon.Clock => "",
        AppIcon.Close => "",
        AppIcon.Command => "",
        AppIcon.Copy => "",
        AppIcon.Delete => "",
        AppIcon.Document => "",
        AppIcon.Edit => "",
        AppIcon.Error => "",
        AppIcon.Folder => "",
        AppIcon.Globe => "",
        AppIcon.Info => "",
        AppIcon.Link => "",
        AppIcon.More => "",
        AppIcon.Retry => "",
        AppIcon.Search => "",
        AppIcon.Send => "",
        AppIcon.Settings => "",
        AppIcon.Shield => "",
        AppIcon.Stop => "",
        AppIcon.Success => "",
        AppIcon.Tool => "",
        AppIcon.Warning => "",
        _ => "",
    };
}
