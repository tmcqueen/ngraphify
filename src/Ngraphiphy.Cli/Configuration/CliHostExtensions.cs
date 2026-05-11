using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ngraphiphy.Cli.Configuration.Options;
using Ngraphiphy.Cli.Configuration.Secrets;

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
            // NGRAPHIPHY_Llm__Anthropic__ApiKey=sk-ant-... (double-underscore = section separator)
            .AddEnvironmentVariables(prefix: "NGRAPHIPHY_");

        // 2. Secret overlay — synchronous, before any binding.
        //    builder.Configuration implements both IConfiguration and IConfigurationBuilder
        //    in .NET 8+ (ConfigurationManager). Calling .Build() gives a snapshot to walk.
        var passProvider = new PassSecretProvider();
        var envProvider = new EnvSecretProvider();
        var providers = new Dictionary<string, ISecretProvider>(StringComparer.Ordinal)
        {
            ["pass"] = passProvider,
            ["env"] = envProvider,
        };

        var snapshot = ((IConfigurationBuilder)builder.Configuration).Build();
        SecretResolver.ResolveAndOverlay(builder.Configuration, snapshot, providers);

        // 3. Register singleton providers for any code that needs late resolution
        builder.Services.AddSingleton<ISecretProvider>(passProvider);

        // 4. Bind options — executed after the overlay, so secrets are plain strings
        builder.Services.Configure<LlmOptions>(builder.Configuration.GetSection("Llm"));
        builder.Services.Configure<GraphDatabaseOptions>(builder.Configuration.GetSection("GraphDatabase"));
        builder.Services.Configure<EmbeddingOptions>(builder.Configuration.GetSection("Embedding"));

        return builder;
    }
}
