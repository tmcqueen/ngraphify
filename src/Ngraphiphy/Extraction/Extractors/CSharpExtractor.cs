using TreeSitter;

namespace Ngraphiphy.Extraction.Extractors;

public sealed class CSharpExtractor : GenericTreeSitterExtractor
{
    public override IReadOnlySet<string> SupportedExtensions { get; } =
        new HashSet<string> { ".cs" };

    public override string TreeSitterLanguage => "c_sharp";

    protected override Language CreateLanguage() =>
        new Language("tree-sitter-c-sharp", "tree_sitter_c_sharp");

    protected override IReadOnlySet<string> ClassNodeTypes { get; } =
        new HashSet<string>
        {
            "class_declaration",
            "interface_declaration",
            "struct_declaration",
            "record_declaration",
            "enum_declaration",
        };

    protected override IReadOnlySet<string> FunctionNodeTypes { get; } =
        new HashSet<string>
        {
            "method_declaration",
            "constructor_declaration",
            "property_declaration",
        };

    protected override IReadOnlySet<string> ImportNodeTypes { get; } =
        new HashSet<string> { "using_directive" };

    protected override IReadOnlySet<string> CallNodeTypes { get; } =
        new HashSet<string> { "invocation_expression", "object_creation_expression" };

    protected override string? ExtractName(Node node, string nodeType)
    {
        // For method declarations, the name identifier immediately precedes the parameter_list.
        if (nodeType == "method_declaration")
        {
            var children = node.Children.ToList();
            for (int i = 1; i < children.Count; i++)
            {
                if (children[i].Type == "parameter_list" && i > 0)
                {
                    var prev = children[i - 1];
                    if (prev.Type == "identifier")
                        return prev.Text;
                    if (prev.Type == "generic_name")
                    {
                        foreach (var gc in prev.Children)
                        {
                            if (gc.Type == "identifier")
                                return gc.Text;
                        }
                    }
                }
            }
            return null;
        }

        // For constructor_declaration, class/interface/record declarations:
        // first identifier (or generic_name identifier) is the name.
        foreach (var child in node.Children)
        {
            if (child.Type == "identifier")
                return child.Text;

            // Handle generic names like IRepository<T>
            if (child.Type == "generic_name")
            {
                foreach (var grandchild in child.Children)
                {
                    if (grandchild.Type == "identifier")
                        return grandchild.Text;
                }
            }
        }
        return null;
    }

    protected override string? ExtractImportTarget(Node node)
    {
        foreach (var child in node.Children)
        {
            if (child.Type == "qualified_name" || child.Type == "identifier_name" || child.Type == "identifier")
                return child.Text.Replace(".", "::");
        }
        return null;
    }

    protected override string? ExtractCallTarget(Node node)
    {
        var first = node.Children.FirstOrDefault();
        if (first is null) return null;

        // object_creation_expression: find first identifier child
        if (node.Type == "object_creation_expression")
        {
            foreach (var child in node.Children)
            {
                if (child.Type == "identifier")
                    return child.Text;
            }
            return null;
        }

        // invocation_expression: first child is the function reference
        if (first.Type == "identifier")
            return first.Text;

        if (first.Type == "member_access_expression")
        {
            var last = first.Children.LastOrDefault();
            if (last is null) return null;
            if (last.Type == "identifier" || last.Type == "generic_name")
                return last.Text;
        }

        return null;
    }
}
