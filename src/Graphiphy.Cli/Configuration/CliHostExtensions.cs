using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Graphiphy.Cli.Configuration.Options;
using Graphiphy.Cli.Configuration.Secrets;
using Graphiphy.Llm;
using Graphiphy.Storage.Embedding;
using Spectre.Console;
using Microsoft.Extensions.Logging;

namespace Graphiphy.Cli.Configuration;

public static class CliHostExtensions
{
    public static HostApplicationBuilder AddCliConfiguration(this HostApplicationBuilder builder)
    {
        // 1. JSON sources (both optional)
        var currentDir = Directory.GetCurrentDirectory();
        var binaryDir = AppContext.BaseDirectory;
        var userConfigDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".graphiphy");

        // Load order: lowest priority first, highest last.
        // Each source overrides the previous for any key it defines.
        builder.Configuration
            .AddJsonFile(Path.Combine(binaryDir, "appsettings.json"),    // shipped defaults
                optional: true, reloadOnChange: false)
            .AddJsonFile(Path.Combine(binaryDir, "graphiphy.json"),       // binary-local overrides
                optional: true, reloadOnChange: false)
            .AddJsonFile(Path.Combine(userConfigDir, "appsettings.json"), // user-wide overrides
                optional: true, reloadOnChange: false)
            .AddJsonFile(Path.Combine(currentDir, "graphiphy.json"),      // project-specific overrides
                optional: true, reloadOnChange: false)
            .AddJsonFile(Path.Combine(currentDir, "serilog.json"),      // logging-specific overrides
                optional: true, reloadOnChange: false)
            .AddEnvironmentVariables(prefix: "GRAPHIPHY_");               // highest priority

        // 2. Secret overlay
        var passProvider = new PassSecretProvider();
        var envProvider = new EnvSecretProvider();
        var providers = new Dictionary<string, ISecretProvider>(StringComparer.Ordinal)
        {
            ["pass"] = passProvider,
            ["env"] = envProvider,
        };

        var snapshot = ((IConfigurationBuilder)builder.Configuration).Build();
        // Scope startup resolution to sections bound via IOptions<T>.
        // Providers:* secrets are resolved lazily by the resolver classes when actually needed.
        SecretResolver.ResolveAndOverlayAsync(
            builder.Configuration, snapshot, providers,
            warn: msg => AnsiConsole.MarkupLineInterpolated($"[yellow]Warning: {msg}[/]"),
            sectionPrefixes: ["GraphDatabase"])
            .GetAwaiter().GetResult();

        // 3. Register secret providers (keyed + default)
        builder.Services.AddKeyedSingleton<ISecretProvider>("pass", passProvider);
        builder.Services.AddKeyedSingleton<ISecretProvider>("env", envProvider);
        builder.Services.AddSingleton<ISecretProvider>(passProvider);

        // 4. Register provider resolvers with a lazy secret-resolution delegate.
        //    Secrets are resolved on demand when a provider is actually used, not at startup.
        Func<string?, string?> resolveSecret =
            value => SecretResolver.ResolveValue(value, providers);

        builder.Services.AddSingleton(sp =>
            new AgentProviderResolver(
                sp.GetRequiredService<IConfiguration>(), 
                sp.GetRequiredService<ILoggerFactory>(),
                resolveSecret));
        builder.Services.AddSingleton(sp =>
            new EmbeddingProviderResolver(sp.GetRequiredService<IConfiguration>(), resolveSecret));

        // 5. Bind options
        builder.Services.Configure<GraphDatabaseOptions>(builder.Configuration.GetSection("GraphDatabase"));
        builder.Services.Configure<AnalysisOptions>(builder.Configuration.GetSection("Analysis"));

        return builder;
    }
}
