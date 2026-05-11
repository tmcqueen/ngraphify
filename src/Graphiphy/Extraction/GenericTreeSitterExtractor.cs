using Graphiphy.Models;
using TreeSitter;

namespace Graphiphy.Extraction;

/// <summary>
/// Base class providing tree-sitter parsing and common AST walking patterns.
/// Subclasses override to define language-specific node type mappings.
/// </summary>
public abstract class GenericTreeSitterExtractor : ILanguageExtractor
{
    public abstract IReadOnlySet<string> SupportedExtensions { get; }
    public abstract string TreeSitterLanguage { get; }

    /// <summary>Node types that represent class/struct/interface declarations.</summary>
    protected abstract IReadOnlySet<string> ClassNodeTypes { get; }

    /// <summary>Node types that represent function/method declarations.</summary>
    protected abstract IReadOnlySet<string> FunctionNodeTypes { get; }

    /// <summary>Node types that represent import/using statements.</summary>
    protected abstract IReadOnlySet<string> ImportNodeTypes { get; }

    /// <summary>Node types that represent function/method calls.</summary>
    protected abstract IReadOnlySet<string> CallNodeTypes { get; }

    /// <summary>
    /// Extract the name from a class/function/import AST node.
    /// Returns null if the node should be skipped.
    /// </summary>
    protected abstract string? ExtractName(TreeSitter.Node node, string nodeType);

    /// <summary>
    /// Extract import target from an import node.
    /// Returns the module/symbol being imported.
    /// </summary>
    protected abstract string? ExtractImportTarget(TreeSitter.Node node);

    /// <summary>
    /// Extract the callee name from a call expression node.
    /// </summary>
    protected abstract string? ExtractCallTarget(TreeSitter.Node node);

    /// <summary>
    /// Creates the TreeSitter Language instance for parsing.
    /// Override to use a different library or function name.
    /// </summary>
    protected virtual Language CreateLanguage() => new Language(TreeSitterLanguage);

    /// <summary>
    /// Build the node ID prefix from the file path (e.g. "module::").
    /// </summary>
    protected virtual string BuildIdPrefix(string filePath)
    {
        var name = Path.GetFileNameWithoutExtension(filePath);
        // Strip special chars, keep alphanumeric and underscore
        var clean = new string(name.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
        return clean + "::";
    }

    public Models.Extraction Extract(string filePath, string sourceCode)
    {
        var extraction = new Models.Extraction { SourceFile = filePath };
        var prefix = BuildIdPrefix(filePath);

        using var language = CreateLanguage();
        using var parser = new Parser(language);
        using var tree = parser.Parse(sourceCode);

        if (tree is null)
            return extraction;

        var root = tree.RootNode;

        var classNames = new HashSet<string>();
        var functionNames = new HashSet<string>();
        var nodeIds = new HashSet<string>();

        // Walk the tree
        WalkNode(root, extraction, prefix, filePath, classNames, functionNames, nodeIds, parent: null);

        return extraction;
    }

    private void WalkNode(
        TreeSitter.Node node,
        Models.Extraction extraction,
        string prefix,
        string filePath,
        HashSet<string> classNames,
        HashSet<string> functionNames,
        HashSet<string> nodeIds,
        string? parent)
    {
        var nodeType = node.Type;
        var location = $"L{node.StartPosition.Row + 1}";

        if (ClassNodeTypes.Contains(nodeType))
        {
            var name = ExtractName(node, nodeType);
            if (name is not null)
            {
                var id = prefix + name;
                if (nodeIds.Add(id))
                {
                    extraction.Nodes.Add(new Models.Node
                    {
                        Id = id,
                        Label = name,
                        FileTypeString = "code",
                        SourceFile = filePath,
                        SourceLocation = location,
                    });
                    classNames.Add(name);

                    if (parent is not null)
                    {
                        extraction.Edges.Add(new Models.Edge
                        {
                            Source = parent,
                            Target = id,
                            Relation = "contains",
                            ConfidenceString = "EXTRACTED",
                            SourceFile = filePath,
                            SourceLocation = location,
                        });
                    }
                }

                // Walk children with this class as parent
                foreach (var child in node.Children)
                    WalkNode(child, extraction, prefix, filePath, classNames, functionNames, nodeIds, id);
                return;
            }
        }

        if (FunctionNodeTypes.Contains(nodeType))
        {
            var name = ExtractName(node, nodeType);
            if (name is not null)
            {
                var id = prefix + name;
                if (nodeIds.Add(id))
                {
                    extraction.Nodes.Add(new Models.Node
                    {
                        Id = id,
                        Label = name,
                        FileTypeString = "code",
                        SourceFile = filePath,
                        SourceLocation = location,
                    });
                    functionNames.Add(name);

                    if (parent is not null)
                    {
                        extraction.Edges.Add(new Models.Edge
                        {
                            Source = parent,
                            Target = id,
                            Relation = "contains",
                            ConfidenceString = "EXTRACTED",
                            SourceFile = filePath,
                            SourceLocation = location,
                        });
                    }
                }

                // Walk children for calls within this function
                foreach (var child in node.Children)
                    WalkNode(child, extraction, prefix, filePath, classNames, functionNames, nodeIds, id);
                return;
            }
        }

        if (ImportNodeTypes.Contains(nodeType))
        {
            var target = ExtractImportTarget(node);
            if (target is not null)
            {
                var targetId = target;
                if (!nodeIds.Contains(targetId))
                {
                    nodeIds.Add(targetId);
                    extraction.Nodes.Add(new Models.Node
                    {
                        Id = targetId,
                        Label = target.Split("::").Last(),
                        FileTypeString = "code",
                        SourceFile = filePath,
                        SourceLocation = location,
                    });
                }

                var sourceId = parent ?? prefix.TrimEnd(':');
                extraction.Edges.Add(new Models.Edge
                {
                    Source = sourceId,
                    Target = targetId,
                    Relation = "imports",
                    ConfidenceString = "EXTRACTED",
                    SourceFile = filePath,
                    SourceLocation = location,
                });
            }
        }

        if (CallNodeTypes.Contains(nodeType))
        {
            var target = ExtractCallTarget(node);
            if (target is not null && parent is not null)
            {
                var targetId = prefix + target;
                extraction.Edges.Add(new Models.Edge
                {
                    Source = parent,
                    Target = targetId,
                    Relation = "calls",
                    ConfidenceString = "EXTRACTED",
                    SourceFile = filePath,
                    SourceLocation = location,
                });
            }
        }

        // Recurse into children
        foreach (var child in node.Children)
            WalkNode(child, extraction, prefix, filePath, classNames, functionNames, nodeIds, parent);
    }
}
