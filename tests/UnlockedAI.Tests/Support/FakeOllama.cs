using UnlockedAI.Core.Ollama;

namespace UnlockedAI.Tests.Support;

/// <summary>An <see cref="IOllamaClient"/> whose replies are scripted by the test.</summary>
internal sealed class FakeOllama : IOllamaClient
{
    private readonly Queue<Func<CancellationToken, IAsyncEnumerable<ModelEvent>>> _replies = new();

    /// <summary>Every request made, in order.</summary>
    public List<(ModelOptions Options, IReadOnlyList<ChatTurn> Turns, IReadOnlyList<ToolDefinition> Tools)> Requests { get; } = [];

    public IReadOnlyList<ModelInfo> Models { get; set; } = [];

    /// <summary>Queues a reply made of plain events.</summary>
    public FakeOllama Reply(params ModelEvent[] events)
    {
        _replies.Enqueue(_ => events.ToAsyncEnumerable());
        return this;
    }

    /// <summary>Queues a reply produced by custom code, for delays and failures.</summary>
    public FakeOllama Reply(Func<CancellationToken, IAsyncEnumerable<ModelEvent>> reply)
    {
        _replies.Enqueue(reply);
        return this;
    }

    public Task<IReadOnlyList<ModelInfo>> ListModelsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Models);

    public Task PreloadAsync(ModelOptions options, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public IAsyncEnumerable<ModelEvent> StreamChatAsync(
        ModelOptions options,
        IReadOnlyList<ChatTurn> turns,
        IReadOnlyList<ToolDefinition> tools,
        CancellationToken cancellationToken = default)
    {
        // Copy the turns: the session keeps adding to its own list after this call.
        Requests.Add((options, turns.ToList(), tools.ToList()));
        return _replies.Dequeue()(cancellationToken);
    }
}
