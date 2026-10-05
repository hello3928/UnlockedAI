using UnlockedAI.Core.Secrets;

namespace UnlockedAI.Tests.Support;

internal sealed class MemorySecretStore : ISecretStore
{
    private readonly Dictionary<string, string> _values = [];

    public string? Read(string name) => _values.GetValueOrDefault(name);

    public void Write(string name, string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            _values.Remove(name);
        }
        else
        {
            _values[name] = value;
        }
    }
}
