using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Graphiphy.Cli.Commands;
using Graphiphy.Cli.Configuration;
using Graphiphy.Cli.Infrastructure;
using Spectre.Console.Cli;
using Serilog;
using Microsoft.Extensions.Configuration;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Verbose()
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} - {Message:lj}{NewLine}{Exception}")
    .CreateBootstrapLogger();


var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((services, loggerConfig) =>
{
            
      loggerConfig
            .ReadFrom.Configuration(builder.Configuration) // allows minimum level and sinks to be configured via config files or env vars
            .ReadFrom.Services(services) // allows minimum level and sinks to be configured via config files or env vars
            .Enrich.FromLogContext()
            .WriteTo.Console(
                  standardErrorFromLevel: Serilog.Events.LogEventLevel.Verbose, // write all levels to console
                  outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} - {Message:lj}{NewLine}{Exception}"
            );
});

builder.AddCliConfiguration();

// Commands with constructor injection must be registered so DI can resolve them.
// AnalyzeCommand, ReportCommand, ServeCommand are parameterless — Spectre instantiates
// them via Activator.CreateInstance when not in the container.
builder.Services.AddTransient<AnalyzeCommand>();
builder.Services.AddTransient<QueryCommand>();
builder.Services.AddTransient<PushCommand>();

var registrar = new TypeRegistrar(builder);
var app = new CommandApp(registrar);

app.Configure(config =>
{
      config.SetApplicationName("graphiphy");
      config.AddCommand<AnalyzeCommand>("analyze")
            .WithDescription("Analyze a repository and print graph statistics.");
      config.AddCommand<ReportCommand>("report")
            .WithDescription("Generate a Markdown report for a repository.");
      config.AddCommand<QueryCommand>("query")
            .WithDescription("Ask an LLM a question about the repository graph.");
      config.AddCommand<ServeCommand>("serve")
            .WithDescription("Start an MCP server over stdio for the given repository.");
      config.AddCommand<PushCommand>("push")
            .WithDescription("Push repository graph to Neo4j or Memgraph with optional embeddings and summaries.");
});

return app.Run(args);
