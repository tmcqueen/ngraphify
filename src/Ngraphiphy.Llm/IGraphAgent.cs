using Ngraphiphy.Models;
using QuikGraph;

namespace Ngraphiphy.Llm;

/// <summary>
/// Answers natural-language questions about an analyzed knowledge graph.
/// </summary>
public interface IGraphAgent
{
    Task<string> AnswerAsync(
        string question,
        BidirectionalGraph<Node, TaggedEdge<Node, Edge>> graph,
        CancellationToken ct = default);

    Task<string> SummarizeAsync(
        BidirectionalGraph<Node, TaggedEdge<Node, Edge>> graph,
        CancellationToken ct = default);
}
