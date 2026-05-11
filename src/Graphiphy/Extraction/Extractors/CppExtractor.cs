using TreeSitter;

namespace Graphiphy.Extraction.Extractors;

public sealed class CppExtractor : GenericTreeSitterExtractor
{
    public override IReadOnlySet<string> SupportedExtensions { get; } =
        new HashSet<string> { ".cpp", ".hpp", ".cc", ".cxx", ".hxx", ".h" };

    public override string TreeSitterLanguage => "cpp";

    protected override IReadOnlySet<string> ClassNodeTypes { get; } =
        new HashSet<string> { "class_specifier", "struct_specifier", "template_declaration" };

    protected override IReadOnlySet<string> FunctionNodeTypes { get; } =
        new HashSet<string> { "function_definition", "declaration" };

    protected override IReadOnlySet<string> ImportNodeTypes { get; } =
        new HashSet<string> { "preproc_include" };

    protected override IReadOnlySet<string> CallNodeTypes { get; } =
        new HashSet<string> { "call_expression" };

    protected override string? ExtractName(Node node, string nodeType)
    {
        if (nodeType == "template_declaration")
        {
            // Find the inner class_specifier or struct_specifier
            foreach (var child in node.Children)
            {
                if (child.Type == "class_specifier" || child.Type == "struct_specifier")
                    return ExtractName(child, child.Type);
            }
            return null;
        }

        if (nodeType == "function_definition")
        {
            // Find function_declarator child (it contains the qualified or simple name)
            foreach (var child in node.Children)
            {
                if (child.Type == "function_declarator")
                    return ExtractFunctionDeclaratorName(child);
            }
            // Fallback: look for identifier directly
            foreach (var child in node.Children)
            {
                if (child.Type == "identifier")
                    return child.Text;
            }
            return null;
        }

        if (nodeType == "declaration")
        {
            // Look for function_declarator inside declaration
            foreach (var child in node.Children)
            {
                if (child.Type == "function_declarator")
                    return ExtractFunctionDeclaratorName(child);
            }
            return null;
        }

        // class_specifier or struct_specifier: find type_identifier or identifier
        foreach (var child in node.Children)
        {
            if (child.Type == "type_identifier" || child.Type == "identifier")
                return child.Text;
        }
        return null;
    }

    private string? ExtractFunctionDeclaratorName(Node declarator)
    {
        foreach (var child in declarator.Children)
        {
            if (child.Type == "identifier")
                return child.Text;

            if (child.Type == "qualified_identifier" || child.Type == "scoped_identifier")
                return ExtractLastPart(child);

            if (child.Type == "destructor_name")
            {
                // ~ClassName — skip destructors or return name
                foreach (var dc in child.Children)
                {
                    if (dc.Type == "identifier")
                        return "~" + dc.Text;
                }
            }
        }
        return null;
    }

    private string? ExtractLastPart(Node node)
    {
        // For qualified/scoped identifiers, get the last identifier child
        string? last = null;
        foreach (var child in node.Children)
        {
            if (child.Type == "identifier" || child.Type == "type_identifier")
                last = child.Text;
            else if (child.Type == "qualified_identifier" || child.Type == "scoped_identifier")
                last = ExtractLastPart(child) ?? last;
        }
        return last;
    }

    protected override string? ExtractImportTarget(Node node)
    {
        foreach (var child in node.Children)
        {
            if (child.Type == "string_literal" || child.Type == "system_lib_string")
            {
                var text = child.Text;
                // Remove surrounding quotes or angle brackets
                if (text.Length >= 2)
                    text = text.Substring(1, text.Length - 2);
                // Strip .h / .hpp extension
                if (text.EndsWith(".hpp"))
                    text = text.Substring(0, text.Length - 4);
                else if (text.EndsWith(".h"))
                    text = text.Substring(0, text.Length - 2);
                // Replace path separators with ::
                text = text.Replace("/", "::");
                return text;
            }
        }
        return null;
    }

    protected override string? ExtractCallTarget(Node node)
    {
        var first = node.Children.FirstOrDefault();
        if (first is null) return null;

        if (first.Type == "identifier")
            return first.Text;

        if (first.Type == "field_expression" || first.Type == "member_expression")
        {
            var last = first.Children.LastOrDefault();
            if (last is not null && (last.Type == "field_identifier" || last.Type == "identifier"))
                return last.Text;
        }

        if (first.Type == "qualified_identifier" || first.Type == "scoped_identifier")
        {
            var last = first.Children.LastOrDefault();
            if (last is not null)
                return last.Text;
        }

        return null;
    }
}
