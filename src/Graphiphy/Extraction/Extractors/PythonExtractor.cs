using TreeSitter;

namespace Graphiphy.Extraction.Extractors;

public sealed class PythonExtractor : GenericTreeSitterExtractor
{
    public override IReadOnlySet<string> SupportedExtensions { get; } =
        new HashSet<string> { ".py", ".pyw", ".pyi" };

    public override string TreeSitterLanguage => "python";

    protected override IReadOnlySet<string> ClassNodeTypes { get; } =
        new HashSet<string> { "class_definition" };

    protected override IReadOnlySet<string> FunctionNodeTypes { get; } =
        new HashSet<string> { "function_definition" };

    protected override IReadOnlySet<string> ImportNodeTypes { get; } =
        new HashSet<string> { "import_statement", "import_from_statement" };

    protected override IReadOnlySet<string> CallNodeTypes { get; } =
        new HashSet<string> { "call" };

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
        if (node.Type == "import_statement")
        {
            foreach (var child in node.Children)
            {
                if (child.Type == "dotted_name")
                    return child.Text.Replace(".", "::");
            }
        }
        else if (node.Type == "import_from_statement")
        {
            foreach (var child in node.Children)
            {
                if (child.Type == "dotted_name" || child.Type == "relative_import")
                    return child.Text.Replace(".", "::");
            }
        }
        return null;
    }

    protected override string? ExtractCallTarget(Node node)
    {
        // call node: first child is the function being called
        var children = node.Children.ToList();
        if (children.Count == 0) return null;

        var func = children[0];
        if (func.Type == "identifier")
            return func.Text;

        if (func.Type == "attribute")
        {
            // obj.method() — last identifier child is the method name
            var attrChildren = func.Children.ToList();
            for (int i = attrChildren.Count - 1; i >= 0; i--)
            {
                if (attrChildren[i].Type == "identifier")
                    return attrChildren[i].Text;
            }
        }
        return null;
    }
}
