using Windows.ApplicationModel.DataTransfer;

namespace UnlockedAI.Platform;

internal static class ClipboardService
{
    public static void CopyText(string text)
    {
        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);
    }
}
