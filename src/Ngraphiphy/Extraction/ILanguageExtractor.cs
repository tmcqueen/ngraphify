using Ngraphiphy.Models;

namespace Ngraphiphy.Extraction;

public interface ILanguageExtractor
{
    /// <summary>
    /// File extensions this extractor handles (e.g. ".py", ".pyw").
    /// </summary>
    IReadOnlySet<string> SupportedExtensions { get; }

    /// <summary>
    /// The tree-sitter language name (e.g. "python", "javascript").
    /// </summary>
    string TreeSitterLanguage { get; }

    /// <summary>
    /// Extract nodes and edges from source code.
    /// </summary>
    Models.Extraction Extract(string filePath, string sourceCode);
}
