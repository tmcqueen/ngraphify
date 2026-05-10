namespace Ngraphiphy.Extraction;

public sealed class LanguageRegistry
{
    private readonly Dictionary<string, ILanguageExtractor> _byExtension = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ILanguageExtractor> _extractors = [];

    public void Register(ILanguageExtractor extractor)
    {
        _extractors.Add(extractor);
        foreach (var ext in extractor.SupportedExtensions)
            _byExtension[ext] = extractor;
    }

    public ILanguageExtractor? GetExtractor(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return _byExtension.GetValueOrDefault(ext);
    }

    public IReadOnlyList<ILanguageExtractor> All => _extractors;

    public static LanguageRegistry CreateDefault()
    {
        var registry = new LanguageRegistry();
        registry.Register(new Extractors.PythonExtractor());
        registry.Register(new Extractors.JavaScriptExtractor());
        registry.Register(new Extractors.TypeScriptExtractor());
        registry.Register(new Extractors.CExtractor());
        registry.Register(new Extractors.CppExtractor());
        registry.Register(new Extractors.CSharpExtractor());
        registry.Register(new Extractors.JavaExtractor());
        registry.Register(new Extractors.GoExtractor());
        registry.Register(new Extractors.RustExtractor());
        return registry;
    }
}
