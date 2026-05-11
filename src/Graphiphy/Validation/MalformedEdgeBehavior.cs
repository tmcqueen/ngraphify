namespace Graphiphy.Validation;

/// <summary>
/// Controls how the pipeline responds when an extractor produces an edge whose source or
/// target node ID is not present in the same extraction's node list.
/// </summary>
public enum MalformedEdgeBehavior
{
    /// <summary>Throw an exception and halt the pipeline. (Default)</summary>
    Throw,

    /// <summary>Discard the entire file extraction and continue. The file contributes no nodes or edges.</summary>
    SkipFile,

    /// <summary>Remove only the dangling edges, keep the valid nodes, and continue.</summary>
    DropEdges,

    /// <summary>Log a warning and include the extraction as-is, dangling edges and all.</summary>
    Warn,
}
