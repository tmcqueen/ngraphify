using TreeSitter;

namespace Ngraphiphy.Extraction.Extractors;

public sealed class CExtractor : GenericTreeSitterExtractor
{
    public override IReadOnlySet<string> SupportedExtensions { get; } =
        new HashSet<string> { ".c", ".h" };

    public override string TreeSitterLanguage => "c";

    protected override IReadOnlySet<string> ClassNodeTypes { get; } =
        new HashSet<string> { "struct_specifier", "enum_specifier", "type_definition" };

    protected override IReadOnlySet<string> FunctionNodeTypes { get; } =
        new HashSet<string> { "function_definition" };

    protected override IReadOnlySet<string> ImportNodeTypes { get; } =
        new HashSet<string> { "preproc_include" };

    protected override IReadOnlySet<string> CallNodeTypes { get; } =
        new HashSet<string> { "call_expression" };

    protected override string? ExtractName(Node node, string nodeType)
    {
        if (nodeType == "type_definition")
        {
            // typedef struct { ... } Name — the name is the last type_identifier child
            return node.Children
                .LastOrDefault(c => c.Type == "type_identifier")
                ?.Text;
        }

        if (nodeType == "struct_specifier" || nodeType == "enum_specifier")
        {
            foreach (var child in node.Children)
            {
                if (child.Type == "type_identifier" || child.Type == "identifier")
                    return child.Text;
            }
            return null;
        }

        if (nodeType == "function_definition")
        {
            // Find the function_declarator child (may be wrapped in pointer_declarator for Node* foo())
            return FindFunctionDeclaratorName(node);
        }

        return null;
    }

    private static string? FindFunctionDeclaratorName(Node node)
    {
        foreach (var child in node.Children)
        {
            if (child.Type == "function_declarator")
            {
                foreach (var inner in child.Children)
                {
                    if (inner.Type == "identifier")
                        return inner.Text;
                }
            }
            // Handle pointer_declarator: Node* foo() — recurse one level
            if (child.Type == "pointer_declarator")
            {
                var result = FindFunctionDeclaratorName(child);
                if (result is not null)
                    return result;
            }
        }
        return null;
    }

    protected override string? ExtractImportTarget(Node node)
    {
        foreach (var child in node.Children)
        {
            if (child.Type == "string_literal" || child.Type == "system_lib_string")
            {
                var text = child.Text;
                // Trim surrounding ", <, >
                return text.Trim('"', '<', '>');
            }
        }
        return null;
    }

    protected override string? ExtractCallTarget(Node node)
    {
        var first = node.Children.FirstOrDefault();
        if (first?.Type == "identifier")
            return first.Text;
        return null;
    }
}
