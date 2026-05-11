using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ngraphiphy.Cli.Configuration.Options;
using Ngraphiphy.Cli.Configuration.Secrets;
using Ngraphiphy.Llm;
using Ngraphiphy.Storage.Embedding;
using Spectre.Console;

namespace Ngraphiphy.Cli.Configuration;

public static class CliHostExtensions
{
    public static HostApplicationBuilder AddCliConfiguration(this HostApplicationBuilder builder)
    {
        // 1. JSON sources (both optional)
        var binaryDir = AppContext.BaseDirectory;
        var userConfigDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".ngraphiphy");

        builder.Configuration
            .AddJsonFile(Path.Combine(binaryDir, "appsettings.json"),
                optional: true, reloadOnChange: false)
            .AddJsonFile(Path.Combine(userConfigDir, "appsettings.json"),
                optional: true, reloadOnChange: false)
            .AddEnvironmentVariables(prefix: "NGRAPHIPHY_");

        // 2. Secret overlay
        var passProvider = new PassSecretProvider();
        var envProvider = new EnvSecretProvider();
        var providers = new Dictionary<string, ISecretProvider>(StringComparer.Ordinal)
        {
            ["pass"] = passProvider,
            ["env"] = envProvider,
        };

        var snapshot = ((IConfigurationBuilder)builder.Configuration).Build();
        SecretResolver.ResolveAndOverlayAsync(
            builder.Configuration, snapshot, providers,
            warn: msg => AnsiConsole.MarkupLineInterpolated($"[yellow]Warning: {msg}[/]"))
            .GetAwaiter().GetResult();

        // 3. Register secret providers (keyed + default)
        builder.Services.AddKeyedSingleton<ISecretProvider>("pass", passProvider);
        builder.Services.AddKeyedSingleton<ISecretProvider>("env", envProvider);
        builder.Services.AddSingleton<ISecretProvider>(passProvider);

        // 4. Register provider resolvers — both take IConfiguration directly
        builder.Services.AddSingleton<AgentProviderResolver>();
        builder.Services.AddSingleton<EmbeddingProviderResolver>();

        // 5. Bind graph database options (unchanged)
        builder.Services.Configure<GraphDatabaseOptions>(builder.Configuration.GetSection("GraphDatabase"));

        return builder;
    }
}
