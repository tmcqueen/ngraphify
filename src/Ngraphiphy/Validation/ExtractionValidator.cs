using Ngraphiphy.Models;

namespace Ngraphiphy.Validation;

public static class ExtractionValidator
{
    private static readonly HashSet<string> ValidFileTypes =
        ["code", "document", "paper", "image", "rationale", "concept"];

    private static readonly HashSet<string> ValidConfidences =
        ["EXTRACTED", "INFERRED", "AMBIGUOUS"];

    public static List<string> Validate(Models.Extraction extraction)
    {
        var errors = new List<string>();
        var nodeIds = new HashSet<string>();

        for (int i = 0; i < extraction.Nodes.Count; i++)
        {
            var node = extraction.Nodes[i];

            if (string.IsNullOrWhiteSpace(node.Id))
                errors.Add($"Node[{i}]: missing required field 'id'");
            else
                nodeIds.Add(node.Id);

            if (string.IsNullOrWhiteSpace(node.Label))
                errors.Add($"Node[{i}]: missing required field 'label'");

            if (string.IsNullOrWhiteSpace(node.SourceFile))
                errors.Add($"Node[{i}]: missing required field 'source_file'");

            if (!ValidFileTypes.Contains(node.FileTypeString))
                errors.Add($"Node[{i}]: invalid file_type '{node.FileTypeString}' (must be one of: {string.Join(", ", ValidFileTypes)})");
        }

        for (int i = 0; i < extraction.Edges.Count; i++)
        {
            var edge = extraction.Edges[i];

            if (string.IsNullOrWhiteSpace(edge.Source))
                errors.Add($"Edge[{i}]: missing required field 'source'");

            if (string.IsNullOrWhiteSpace(edge.Target))
                errors.Add($"Edge[{i}]: missing required field 'target'");

            if (string.IsNullOrWhiteSpace(edge.Relation))
                errors.Add($"Edge[{i}]: missing required field 'relation'");

            if (string.IsNullOrWhiteSpace(edge.SourceFile))
                errors.Add($"Edge[{i}]: missing required field 'source_file'");

            if (!ValidConfidences.Contains(edge.ConfidenceString))
                errors.Add($"Edge[{i}]: invalid confidence '{edge.ConfidenceString}' (must be one of: {string.Join(", ", ValidConfidences)})");

            if (!string.IsNullOrWhiteSpace(edge.Source) && !nodeIds.Contains(edge.Source))
                errors.Add($"Edge[{i}]: dangling source '{edge.Source}' not found in nodes");

            if (!string.IsNullOrWhiteSpace(edge.Target) && !nodeIds.Contains(edge.Target))
                errors.Add($"Edge[{i}]: dangling target '{edge.Target}' not found in nodes");
        }

        return errors;
    }

    public static void AssertValid(Models.Extraction extraction)
    {
        var errors = Validate(extraction);
        if (errors.Count > 0)
            throw new InvalidOperationException(
                $"Invalid extraction ({errors.Count} errors):\n" + string.Join("\n", errors));
    }
}
