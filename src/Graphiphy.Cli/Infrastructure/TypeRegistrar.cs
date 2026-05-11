using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectre.Console.Cli;

namespace Graphiphy.Cli.Infrastructure;

internal sealed class TypeRegistrar(HostApplicationBuilder builder) : ITypeRegistrar
{
    public ITypeResolver Build()
    {
        var host = builder.Build();
        return new TypeResolver(host);
    }

    public void Register(Type service, Type implementation) =>
        builder.Services.AddSingleton(service, implementation);

    public void RegisterInstance(Type service, object implementation) =>
        builder.Services.AddSingleton(service, implementation);

    public void RegisterLazy(Type service, Func<object> factory) =>
        builder.Services.AddSingleton(service, _ => factory());
}
