using UnlockedAI.Core.Data;
using UnlockedAI.Core.Files;
using UnlockedAI.Core.Search;
using UnlockedAI.Core.Web;

namespace UnlockedAI.Core.Tools.Builtin;

public static class BuiltinTools
{
    /// <summary>
    /// Every tool the app ships with, in the order Settings lists them: looking things up first,
    /// then the ones that can change the PC. A new tool is added here.
    /// </summary>
    public static ToolRegistry CreateRegistry(
        SettingsService settings,
        WebReader web,
        SearchService search,
        AttachmentService files) =>
        new(
        [
            new WebSearchTool(search),
            new WebFetchTool(web),
            new HttpRequestTool(web),
            new DateTimeInfoTool(settings),
            new ListDirectoryTool(settings),
            new ReadFileTool(settings, files),
            new WriteFileTool(settings),
            new RunCommandTool(settings),
        ]);
}
