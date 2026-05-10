using Ngraphiphy.Cli.Commands;
using Spectre.Console.Cli;

var app = new CommandApp();
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
});
return app.Run(args);
