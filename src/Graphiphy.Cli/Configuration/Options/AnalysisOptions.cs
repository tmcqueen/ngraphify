using Graphiphy.Validation;

namespace Graphiphy.Cli.Configuration.Options;

public sealed class AnalysisOptions
{
    public MalformedEdgeBehavior MalformedEdgeBehavior { get; set; } = MalformedEdgeBehavior.Throw;
}
