// src/Ngraphiphy/Models/Confidence.cs
namespace Ngraphiphy.Models;

public enum Confidence
{
    Extracted,
    Inferred,
    Ambiguous,
}

public static class ConfidenceExtensions
{
    public static string ToSchemaString(this Confidence c) => c switch
    {
        Confidence.Extracted => "EXTRACTED",
        Confidence.Inferred => "INFERRED",
        Confidence.Ambiguous => "AMBIGUOUS",
        _ => "EXTRACTED",
    };

    public static Confidence FromString(string? s) => s?.ToUpperInvariant() switch
    {
        "EXTRACTED" => Confidence.Extracted,
        "INFERRED" => Confidence.Inferred,
        "AMBIGUOUS" => Confidence.Ambiguous,
        _ => Confidence.Extracted,
    };
}
