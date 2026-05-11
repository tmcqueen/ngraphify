using Graphiphy.Extraction;

namespace Graphiphy.Tests.Extraction;

public abstract class ExtractorTestBase
{
    protected abstract ILanguageExtractor CreateExtractor();

    protected string FixturePath(string filename)
    {
        return Path.Combine(AppContext.BaseDirectory, "Fixtures", filename);
    }

    protected Graphiphy.Models.Extraction ExtractFixture(string filename)
    {
        var path = FixturePath(filename);
        var source = File.ReadAllText(path);
        return CreateExtractor().Extract(path, source);
    }

    protected static List<string> NodeLabels(Graphiphy.Models.Extraction extraction)
    {
        return extraction.Nodes.Select(n => n.Label).ToList();
    }

    protected static List<string> EdgeRelations(Graphiphy.Models.Extraction extraction, string sourceLabel, string targetLabel)
    {
        return extraction.Edges
            .Where(e =>
                extraction.Nodes.Any(n => n.Id == e.Source && n.Label == sourceLabel) &&
                (extraction.Nodes.Any(n => n.Id == e.Target && n.Label == targetLabel) ||
                 e.Target.EndsWith("::" + targetLabel)))
            .Select(e => e.Relation)
            .ToList();
    }
}
