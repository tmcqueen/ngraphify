// src/Ngraphiphy.Llm/MafGraphAgent.cs
using Microsoft.Agents.AI;
using System.Runtime.CompilerServices;

namespace Ngraphiphy.Llm;

public sealed class MafGraphAgent : IGraphAgent
{
    private readonly AIAgent _agent;

    internal MafGraphAgent(AIAgent agent) => _agent = agent;

    public async Task<GraphSession> CreateSessionAsync(CancellationToken ct = default)
    {
        var agentSession = await _agent.CreateSessionAsync(ct);
        return new GraphSession(agentSession);
    }

    public async Task<string> AnswerAsync(string question, GraphSession session, CancellationToken ct = default)
    {
        var response = await _agent.RunAsync(question, session.AgentSession, cancellationToken: ct);
        return response.Text;
    }

    public async Task<string> SummarizeAsync(GraphSession session, CancellationToken ct = default)
        => await AnswerAsync(
            "Summarize the most interesting aspects of this codebase graph in 3-5 bullet points.",
            session, ct);

    public async IAsyncEnumerable<string> AnswerStreamingAsync(
        string question,
        GraphSession session,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var update in _agent.RunStreamingAsync(question, session.AgentSession, cancellationToken: ct))
        {
            if (!string.IsNullOrEmpty(update.Text))
                yield return update.Text;
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
