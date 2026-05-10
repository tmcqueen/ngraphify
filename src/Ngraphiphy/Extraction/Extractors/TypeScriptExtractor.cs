using TreeSitter;

namespace Ngraphiphy.Extraction.Extractors;

public sealed class TypeScriptExtractor : GenericTreeSitterExtractor
{
    public override IReadOnlySet<string> SupportedExtensions { get; } =
        new HashSet<string> { ".ts", ".tsx", ".mts", ".cts" };

    public override string TreeSitterLanguage => "typescript";

    protected override IReadOnlySet<string> ClassNodeTypes { get; } =
        new HashSet<string> { "class_declaration", "interface_declaration", "type_alias_declaration" };

    protected override IReadOnlySet<string> FunctionNodeTypes { get; } =
        new HashSet<string> { "function_declaration", "method_definition", "arrow_function" };

    protected override IReadOnlySet<string> ImportNodeTypes { get; } =
        new HashSet<string> { "import_statement" };

    protected override IReadOnlySet<string> CallNodeTypes { get; } =
        new HashSet<string> { "call_expression", "new_expression" };

    protected override string? ExtractName(Node node, string nodeType)
    {
        foreach (var child in node.Children)
        {
            if (child.Type == "type_identifier" || child.Type == "identifier" || child.Type == "property_identifier")
                return child.Text;
        }
        return null;
    }

    protected override string? ExtractImportTarget(Node node)
    {
        foreach (var child in node.Children)
        {
            if (child.Type == "string" || child.Type == "string_fragment")
            {
                var text = child.Text.Trim('"', '\'', '`');
                return text.Replace("/", "::");
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

        if (first.Type == "member_expression")
        {
            var memberChildren = first.Children.ToList();
            for (int i = memberChildren.Count - 1; i >= 0; i--)
            {
                if (memberChildren[i].Type == "property_identifier")
                    return memberChildren[i].Text;
            }
        }
        return null;
    }
}
