using System.Linq;
using TreeSitter;

namespace Graphiphy.Extraction.Extractors;

public sealed class JavaScriptExtractor : GenericTreeSitterExtractor
{
    public override IReadOnlySet<string> SupportedExtensions { get; } =
        new HashSet<string> { ".js", ".jsx", ".mjs", ".cjs" };

    public override string TreeSitterLanguage => "javascript";

    protected override IReadOnlySet<string> ClassNodeTypes { get; } =
        new HashSet<string> { "class_declaration", "class" };

    protected override IReadOnlySet<string> FunctionNodeTypes { get; } =
        new HashSet<string> { "function_declaration", "method_definition", "arrow_function" };

    protected override IReadOnlySet<string> ImportNodeTypes { get; } =
        new HashSet<string> { "import_statement", "call_expression" };

    protected override IReadOnlySet<string> CallNodeTypes { get; } =
        new HashSet<string> { "call_expression" };

    protected override string? ExtractName(Node node, string nodeType)
    {
        foreach (var child in node.Children)
        {
            if (child.Type == "identifier" || child.Type == "property_identifier")
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
                if (child.Type == "string" || child.Type == "string_fragment")
                {
                    var text = child.Text.Trim('"', '\'', '`');
                    return text.Replace("/", "::");
                }
                // string node may contain a string_fragment child
                if (child.Type == "string")
                {
                    foreach (var grandchild in child.Children)
                    {
                        if (grandchild.Type == "string_fragment")
                        {
                            var text = grandchild.Text.Trim('"', '\'', '`');
                            return text.Replace("/", "::");
                        }
                    }
                }
            }
        }
        else if (node.Type == "call_expression")
        {
            // require('...') — check if first child is identifier "require"
            var children = node.Children.ToList();
            if (children.Count == 0) return null;

            var first = children[0];
            if (first.Type != "identifier" || first.Text != "require")
                return null;

            // find argument string in arguments node
            foreach (var child in node.Children)
            {
                if (child.Type == "arguments")
                {
                    foreach (var arg in child.Children)
                    {
                        if (arg.Type == "string")
                        {
                            // get string_fragment inside or trim quotes from text
                            foreach (var sf in arg.Children)
                            {
                                if (sf.Type == "string_fragment")
                                    return sf.Text.Replace("/", "::");
                            }
                            var text = arg.Text.Trim('"', '\'', '`');
                            return text.Replace("/", "::");
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
        {
            if (first.Text == "require") return null;
            return first.Text;
        }

        if (first.Type == "member_expression")
        {
            // obj.method() — last child of member_expression is the property
            var memberChildren = first.Children.ToList();
            var last = memberChildren.LastOrDefault();
            if (last != null && last.Type == "property_identifier")
                return last.Text;
        }

        return null;
    }
}
