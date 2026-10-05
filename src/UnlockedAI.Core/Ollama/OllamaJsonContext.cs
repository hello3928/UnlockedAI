using System.Text.Json.Serialization;

namespace UnlockedAI.Core.Ollama;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ChatRequestDto))]
[JsonSerializable(typeof(ChatChunkDto))]
[JsonSerializable(typeof(TagsDto))]
[JsonSerializable(typeof(ErrorDto))]
internal sealed partial class OllamaJsonContext : JsonSerializerContext;
