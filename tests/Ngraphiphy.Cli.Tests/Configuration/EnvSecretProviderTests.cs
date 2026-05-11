using Ngraphiphy.Cli.Configuration.Secrets;

namespace Ngraphiphy.Cli.Tests.Configuration;

public class EnvSecretProviderTests
{
    [Test]
    public async Task Resolve_SetVariable_ReturnsValue()
    {
        var varName = $"NGRAPHIPHY_TEST_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(varName, "test-secret");
        try
        {
            var provider = new EnvSecretProvider();
            var result = provider.Resolve(varName);
            await Assert.That(result).IsEqualTo("test-secret");
        }
        finally
        {
            Environment.SetEnvironmentVariable(varName, null);
        }
    }

    [Test]
    public async Task Resolve_MissingVariable_Throws()
    {
        var varName = $"NGRAPHIPHY_MISSING_{Guid.NewGuid():N}";
        var provider = new EnvSecretProvider();

        var act = () => provider.Resolve(varName);

        await Assert.That(act).Throws<InvalidOperationException>();
    }
}
