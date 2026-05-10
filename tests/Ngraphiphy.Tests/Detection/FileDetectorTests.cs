using Ngraphiphy.Detection;
using Ngraphiphy.Models;

namespace Ngraphiphy.Tests.Detection;

public class FileDetectorTests
{
    [Test]
    public async Task Detect_FindsPythonFiles()
    {
        var dir = CreateTempDir();
        File.WriteAllText(Path.Combine(dir, "main.py"), "class Foo: pass");
        File.WriteAllText(Path.Combine(dir, "readme.md"), "# Hello");

        var results = FileDetector.Detect(dir);

        await Assert.That(results).Contains(r => r.RelativePath == "main.py");
        await Assert.That(results.First(r => r.RelativePath == "main.py").FileType).IsEqualTo(FileType.Code);
    }

    [Test]
    public async Task Detect_RespectsGitignore()
    {
        var dir = CreateTempDir();
        File.WriteAllText(Path.Combine(dir, "main.py"), "x = 1");
        File.WriteAllText(Path.Combine(dir, "generated.py"), "x = 2");
        File.WriteAllText(Path.Combine(dir, ".gitignore"), "generated.py");

        var results = FileDetector.Detect(dir);

        await Assert.That(results).DoesNotContain(r => r.RelativePath == "generated.py");
        await Assert.That(results).Contains(r => r.RelativePath == "main.py");
    }

    [Test]
    public async Task Detect_RespectsNestedGitignore()
    {
        var dir = CreateTempDir();
        Directory.CreateDirectory(Path.Combine(dir, "src"));
        File.WriteAllText(Path.Combine(dir, "src", "main.py"), "x = 1");
        File.WriteAllText(Path.Combine(dir, "src", "generated.py"), "x = 2");
        File.WriteAllText(Path.Combine(dir, "top.py"), "x = 3");
        // Only ignores files under src/
        File.WriteAllText(Path.Combine(dir, "src", ".gitignore"), "generated.py");

        var results = FileDetector.Detect(dir);

        await Assert.That(results).DoesNotContain(r => r.RelativePath == "src/generated.py");
        await Assert.That(results).Contains(r => r.RelativePath == "src/main.py");
        await Assert.That(results).Contains(r => r.RelativePath == "top.py");
    }

    [Test]
    public async Task Detect_IgnoresHiddenDirs()
    {
        var dir = CreateTempDir();
        Directory.CreateDirectory(Path.Combine(dir, ".git"));
        File.WriteAllText(Path.Combine(dir, ".git", "config"), "x");
        File.WriteAllText(Path.Combine(dir, "main.py"), "x = 1");

        var results = FileDetector.Detect(dir);

        await Assert.That(results).DoesNotContain(r => r.RelativePath.Contains(".git"));
    }

    [Test]
    public async Task Detect_IgnoresNodeModules()
    {
        var dir = CreateTempDir();
        Directory.CreateDirectory(Path.Combine(dir, "node_modules", "pkg"));
        File.WriteAllText(Path.Combine(dir, "node_modules", "pkg", "index.js"), "x");
        File.WriteAllText(Path.Combine(dir, "app.js"), "x");

        var results = FileDetector.Detect(dir);

        await Assert.That(results).DoesNotContain(r => r.RelativePath.Contains("node_modules"));
    }

    [Test]
    public async Task Detect_IgnoresSensitiveFiles()
    {
        var dir = CreateTempDir();
        File.WriteAllText(Path.Combine(dir, ".env"), "SECRET=x");
        File.WriteAllText(Path.Combine(dir, "id_rsa"), "-----BEGIN RSA PRIVATE KEY-----");
        File.WriteAllText(Path.Combine(dir, "main.py"), "x = 1");

        var results = FileDetector.Detect(dir);

        await Assert.That(results).DoesNotContain(r => r.RelativePath == ".env");
        await Assert.That(results).DoesNotContain(r => r.RelativePath == "id_rsa");
    }

    [Test]
    public async Task Detect_WildcardGitignorePattern()
    {
        var dir = CreateTempDir();
        File.WriteAllText(Path.Combine(dir, "foo.gen.py"), "x");
        File.WriteAllText(Path.Combine(dir, "bar.gen.py"), "x");
        File.WriteAllText(Path.Combine(dir, "main.py"), "x");
        File.WriteAllText(Path.Combine(dir, ".gitignore"), "*.gen.py");

        var results = FileDetector.Detect(dir);

        await Assert.That(results.Count).IsEqualTo(1);
        await Assert.That(results[0].RelativePath).IsEqualTo("main.py");
    }

    private static string CreateTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "ngraphiphy_test_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(path);
        return path;
    }
}
