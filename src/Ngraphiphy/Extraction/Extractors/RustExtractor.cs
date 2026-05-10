using TreeSitter;

namespace Ngraphiphy.Extraction.Extractors;

public sealed class RustExtractor : GenericTreeSitterExtractor
{
    public override IReadOnlySet<string> SupportedExtensions { get; } =
        new HashSet<string> { ".rs" };

    public override string TreeSitterLanguage => "rust";

    protected override IReadOnlySet<string> ClassNodeTypes { get; } =
        new HashSet<string> { "struct_item", "enum_item", "trait_item" };

    protected override IReadOnlySet<string> FunctionNodeTypes { get; } =
        new HashSet<string> { "function_item", "impl_item" };

    protected override IReadOnlySet<string> ImportNodeTypes { get; } =
        new HashSet<string> { "use_declaration" };

    protected override IReadOnlySet<string> CallNodeTypes { get; } =
        new HashSet<string> { "call_expression" };

    protected override string? ExtractName(Node node, string nodeType)
    {
        // impl blocks have no meaningful name; their children are walked by WalkNode
        if (nodeType == "impl_item")
            return null;

        foreach (var child in node.Children)
        {
            if (child.Type == "type_identifier" || child.Type == "identifier")
                return child.Text;
        }
        return null;
    }

    protected override string? ExtractImportTarget(Node node)
    {
        foreach (var child in node.Children)
        {
            if (child.Type == "scoped_identifier" ||
                child.Type == "identifier" ||
                child.Type == "use_wildcard" ||
                child.Type == "scoped_use_list" ||
                child.Type == "use_list")
            {
                return child.Text;
            }
        }
        return null;
    }

    protected override string? ExtractCallTarget(Node node)
    {
        var first = node.Children.FirstOrDefault();
        if (first == null) return null;

        if (first.Type == "identifier")
            return first.Text;

        if (first.Type == "scoped_identifier" || first.Type == "field_expression")
        {
            var last = first.Children.LastOrDefault();
            if (last != null && (last.Type == "identifier" || last.Type == "field_identifier"))
                return last.Text;
        }

        return null;
    }
}
