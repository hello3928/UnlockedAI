using UnlockedAI.Core.Models;
using UnlockedAI.Core.Ollama;

namespace UnlockedAI.Core.Tools;

/// <summary>Every tool the app has, in the order they are listed in Settings.</summary>
public sealed class ToolRegistry(IEnumerable<ITool> tools)
{
    private readonly List<ITool> _tools = [.. tools];

    public IReadOnlyList<ITool> All => _tools;

    public ITool? Find(string name) => _tools.Find(tool => tool.Name == name);

    /// <summary>The tools the user has left switched on, in the form sent to the model.</summary>
    public List<ToolDefinition> DefinitionsFor(AppSettings settings)
    {
        var definitions = new List<ToolDefinition>(_tools.Count);
        foreach (var tool in _tools)
        {
            if (!settings.DisabledTools.Contains(tool.Name))
            {
                definitions.Add(new ToolDefinition(tool.Name, tool.Description, tool.ParametersJson));
            }
        }

        return definitions;
    }
}
