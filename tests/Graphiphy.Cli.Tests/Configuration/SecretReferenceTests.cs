using Graphiphy.Cli.Configuration.Secrets;

namespace Graphiphy.Cli.Tests.Configuration;

public class SecretReferenceTests
{
    [Test]
    public async Task Parse_PassScheme_ReturnsReference()
    {
        var r = SecretReference.Parse("pass://anthropic/api-key");
        await Assert.That(r.IsReference).IsTrue();
        await Assert.That(r.Scheme).IsEqualTo("pass");
        await Assert.That(r.Path).IsEqualTo("anthropic/api-key");
    }

    [Test]
    public async Task Parse_EnvScheme_ReturnsReference()
    {
        var r = SecretReference.Parse("env://SOME_VAR");
        await Assert.That(r.IsReference).IsTrue();
        await Assert.That(r.Scheme).IsEqualTo("env");
        await Assert.That(r.Path).IsEqualTo("SOME_VAR");
    }

    [Test]
    public async Task Parse_PlainValue_IsNotReference()
    {
        var r = SecretReference.Parse("supersecret");
        await Assert.That(r.IsReference).IsFalse();
    }

    [Test]
    public async Task Parse_HttpUrl_IsNotReference()
    {
        var r = SecretReference.Parse("http://localhost:11434");
        await Assert.That(r.IsReference).IsFalse();
    }

    [Test]
    public async Task Parse_Null_IsNotReference()
    {
        var r = SecretReference.Parse(null);
        await Assert.That(r.IsReference).IsFalse();
    }

    [Test]
    public async Task Parse_EmptyPassPath_IsReferenceWithEmptyPath()
    {
        var r = SecretReference.Parse("pass://");
        await Assert.That(r.IsReference).IsTrue();
        await Assert.That(r.Path).IsEqualTo("");
    }
}
