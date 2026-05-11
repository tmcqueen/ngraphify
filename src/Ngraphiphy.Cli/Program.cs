using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ngraphiphy.Cli.Commands;
using Ngraphiphy.Cli.Configuration;
using Ngraphiphy.Cli.Infrastructure;
using Spectre.Console.Cli;

var builder = Host.CreateApplicationBuilder(args);
builder.AddCliConfiguration();

// Commands with constructor injection must be registered so DI can resolve them.
// AnalyzeCommand, ReportCommand, ServeCommand are parameterless — Spectre instantiates
// them via Activator.CreateInstance when not in the container.
builder.Services.AddTransient<QueryCommand>();
builder.Services.AddTransient<PushCommand>();

var registrar = new TypeRegistrar(builder);
var app = new CommandApp(registrar);

app.Configure(config =>
{
    config.SetApplicationName("ngraphiphy");
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
