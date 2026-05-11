using TreeSitter;

namespace Graphiphy.Extraction.Extractors;

public sealed class JavaExtractor : GenericTreeSitterExtractor
{
    public override IReadOnlySet<string> SupportedExtensions { get; } =
        new HashSet<string> { ".java" };

    public override string TreeSitterLanguage => "java";

    protected override IReadOnlySet<string> ClassNodeTypes { get; } =
        new HashSet<string> { "class_declaration", "interface_declaration", "enum_declaration", "record_declaration" };

    protected override IReadOnlySet<string> FunctionNodeTypes { get; } =
        new HashSet<string> { "method_declaration", "constructor_declaration" };

    protected override IReadOnlySet<string> ImportNodeTypes { get; } =
        new HashSet<string> { "import_declaration" };

    protected override IReadOnlySet<string> CallNodeTypes { get; } =
        new HashSet<string> { "method_invocation", "object_creation_expression" };

    protected override string? ExtractName(Node node, string nodeType)
    {
        foreach (var child in node.Children)
        {
            if (child.Type == "identifier")
                return child.Text;
        }
        return null;
    }

    protected override string? ExtractImportTarget(Node node)
    {
        foreach (var child in node.Children)
        {
            if (child.Type == "scoped_identifier" || child.Type == "identifier")
                return child.Text.Replace(".", "::");
        }
        return null;
    }

    protected override string? ExtractCallTarget(Node node)
    {
        if (node.Type == "object_creation_expression")
        {
            foreach (var child in node.Children)
            {
                if (child.Type == "type_identifier" || child.Type == "identifier")
                    return child.Text;

                if (child.Type == "generic_type")
                {
                    var first = child.Children.FirstOrDefault();
                    return first?.Text;
                }
            }
            return null;
        }

        // method_invocation: look for the method name
        // Pattern: [object.]method(args)
        // Children may be: identifier, argument_list  OR  object, ".", identifier, argument_list
        var children = node.Children.ToList();
        if (children.Count == 0) return null;

        // Walk children: if we find an identifier before any field_access/method_invocation, return it
        // Otherwise, look at the last identifier across nested member accesses
        string? lastIdentifier = null;
        bool foundAccessor = false;

        foreach (var child in children)
        {
            if (child.Type == "identifier")
            {
                if (!foundAccessor)
                    return child.Text;
                lastIdentifier = child.Text;
            }
            else if (child.Type == "field_access" || child.Type == "method_invocation")
            {
                foundAccessor = true;
                // dig into nested access for last identifier
                foreach (var nested in child.Children)
                {
                    if (nested.Type == "identifier")
                        lastIdentifier = nested.Text;
                }
            }
        }

        return lastIdentifier;
    }
}
