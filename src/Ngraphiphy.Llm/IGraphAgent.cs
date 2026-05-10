// src/Ngraphiphy.Llm/IGraphAgent.cs
using Ngraphiphy.Models;
using QuikGraph;

namespace Ngraphiphy.Llm;

/// <summary>
/// Session-aware LLM agent that answers questions about a repository knowledge graph.
/// Create one agent per repository (graph is fixed at construction), then create
/// sessions per conversation to maintain independent message history.
/// </summary>
public interface IGraphAgent : IAsyncDisposable
{
    /// <summary>Creates a new independent conversation session.</summary>
    Task<GraphSession> CreateSessionAsync(CancellationToken ct = default);

    /// <summary>Ask a question, maintaining history within the given session.</summary>
    Task<string> AnswerAsync(string question, GraphSession session, CancellationToken ct = default);

    /// <summary>Produce a 3-5 bullet-point summary using the given session.</summary>
    Task<string> SummarizeAsync(GraphSession session, CancellationToken ct = default);

    /// <summary>Stream the answer token by token.</summary>
    IAsyncEnumerable<string> AnswerStreamingAsync(
        string question, GraphSession session, CancellationToken ct = default);
}
