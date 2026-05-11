using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectre.Console.Cli;

namespace Graphiphy.Cli.Infrastructure;

internal sealed class TypeResolver(IHost host) : ITypeResolver, IDisposable
{
    public object? Resolve(Type? type) =>
        type is null ? null : host.Services.GetService(type);

    public void Dispose() => host.Dispose();
}
