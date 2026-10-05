using System.Text.Json.Serialization;
using UnlockedAI.Core.Models;

namespace UnlockedAI.Core.Data;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(IReadOnlyList<ToolCall>))]
internal sealed partial class StorageJsonContext : JsonSerializerContext;
