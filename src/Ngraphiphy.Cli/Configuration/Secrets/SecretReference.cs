namespace Ngraphiphy.Cli.Configuration.Secrets;

/// <summary>
/// Parses "pass://path/to/secret" and "env://VAR_NAME" references.
/// All other strings (including "http://...") are treated as literals — IsReference = false.
/// </summary>
public sealed class SecretReference
{
    public bool IsReference { get; private init; }
    public string Scheme { get; private init; } = "";
    public string Path { get; private init; } = "";

    public static SecretReference Parse(string? value)
    {
        if (value is null) return new();

        if (value.StartsWith("pass://", StringComparison.Ordinal))
            return new() { IsReference = true, Scheme = "pass", Path = value[7..] };

        if (value.StartsWith("env://", StringComparison.Ordinal))
            return new() { IsReference = true, Scheme = "env", Path = value[6..] };

        return new(); // literal value — IsReference stays false
    }
}
