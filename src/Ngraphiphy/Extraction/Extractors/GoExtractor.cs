using TreeSitter;

namespace Ngraphiphy.Extraction.Extractors;

public sealed class GoExtractor : GenericTreeSitterExtractor
{
    public override IReadOnlySet<string> SupportedExtensions { get; } =
        new HashSet<string> { ".go" };

    public override string TreeSitterLanguage => "go";

    protected override IReadOnlySet<string> ClassNodeTypes { get; } =
        new HashSet<string> { "type_declaration" };

    protected override IReadOnlySet<string> FunctionNodeTypes { get; } =
        new HashSet<string> { "function_declaration", "method_declaration" };

    protected override IReadOnlySet<string> ImportNodeTypes { get; } =
        new HashSet<string> { "import_declaration", "import_spec" };

    protected override IReadOnlySet<string> CallNodeTypes { get; } =
        new HashSet<string> { "call_expression" };

    protected override string? ExtractName(Node node, string nodeType)
    {
        if (nodeType == "type_declaration")
        {
            // Find type_spec children, then look for type_identifier inside each
            foreach (var child in node.Children)
            {
                if (child.Type == "type_spec")
                {
                    foreach (var grandchild in child.Children)
                    {
                        if (grandchild.Type == "type_identifier")
                            return grandchild.Text;
                    }
                }
            }
        }
        else if (nodeType == "method_declaration")
        {
            // Methods have a field_identifier for the method name
            foreach (var child in node.Children)
            {
                if (child.Type == "field_identifier")
                    return child.Text;
            }
        }
        else if (nodeType == "function_declaration")
        {
            // Functions have an identifier for the function name
            foreach (var child in node.Children)
            {
                if (child.Type == "identifier")
                    return child.Text;
            }
        }
        return null;
    }

    protected override string? ExtractImportTarget(Node node)
    {
        if (node.Type == "import_spec")
        {
            // Find the interpreted_string_literal child and strip quotes
            foreach (var child in node.Children)
            {
                if (child.Type == "interpreted_string_literal")
                {
                    var raw = child.Text.Trim('"');
                    return raw.Replace("/", "::");
                }
            }
        }
        else if (node.Type == "import_declaration")
        {
            // Single import: direct import_spec child
            foreach (var child in node.Children)
            {
                if (child.Type == "import_spec")
                {
                    foreach (var grandchild in child.Children)
                    {
                        if (grandchild.Type == "interpreted_string_literal")
                        {
                            var raw = grandchild.Text.Trim('"');
                            return raw.Replace("/", "::");
                        }
                    }
                }
                // Grouped import: import_spec_list containing import_spec children
                if (child.Type == "import_spec_list")
                {
                    foreach (var spec in child.Children)
                    {
                        if (spec.Type == "import_spec")
                        {
                            foreach (var grandchild in spec.Children)
                            {
                                if (grandchild.Type == "interpreted_string_literal")
                                {
                                    var raw = grandchild.Text.Trim('"');
                                    return raw.Replace("/", "::");
                                }
                            }
                        }
                    }
                }
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

        if (first.Type == "selector_expression")
        {
            var last = first.Children.LastOrDefault();
            if (last != null && last.Type == "field_identifier")
                return last.Text;
        }

        return null;
    }
}
