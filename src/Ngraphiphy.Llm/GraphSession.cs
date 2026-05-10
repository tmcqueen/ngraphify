// src/Ngraphiphy.Llm/GraphSession.cs
using Microsoft.Agents.AI;

namespace Ngraphiphy.Llm;

/// <summary>
/// Represents a single conversation with an <see cref="IGraphAgent"/>.
/// Maintains message history across multiple AnswerAsync calls.
/// Dispose when the conversation is complete.
/// </summary>
public sealed class GraphSession : IAsyncDisposable
{
    internal AgentSession AgentSession { get; }

    internal GraphSession(AgentSession agentSession) => AgentSession = agentSession;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
