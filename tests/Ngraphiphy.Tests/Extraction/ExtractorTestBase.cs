using Ngraphiphy.Extraction;

namespace Ngraphiphy.Tests.Extraction;

public abstract class ExtractorTestBase
{
    protected abstract ILanguageExtractor CreateExtractor();

    protected string FixturePath(string filename)
    {
        return Path.Combine(AppContext.BaseDirectory, "Fixtures", filename);
    }

    protected Ngraphiphy.Models.Extraction ExtractFixture(string filename)
    {
        var path = FixturePath(filename);
        var source = File.ReadAllText(path);
        return CreateExtractor().Extract(path, source);
    }

    protected static List<string> NodeLabels(Ngraphiphy.Models.Extraction extraction)
    {
        return extraction.Nodes.Select(n => n.Label).ToList();
    }

    protected static List<string> EdgeRelations(Ngraphiphy.Models.Extraction extraction, string sourceLabel, string targetLabel)
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
