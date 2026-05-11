using Ngraphiphy.Storage.Models;

namespace Ngraphiphy.Storage.Tests;

public class SnapshotIdTests
{
    [Test]
    public async Task Resolve_InGitRepo_ReturnsCommitHash()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"ngraphiphy_snap_{Guid.NewGuid().ToString("N")[..8]}");
        Directory.CreateDirectory(tempDir);
        try
        {
            // Initialize git repo and create a commit
            LibGit2Sharp.Repository.Init(tempDir);
            using var repo = new LibGit2Sharp.Repository(tempDir);
            var identity = new LibGit2Sharp.Signature("Test User", "test@example.com", System.DateTimeOffset.UtcNow);
            File.WriteAllText(Path.Combine(tempDir, "test.cs"), "// test");
            LibGit2Sharp.Commands.Stage(repo, "test.cs");
            var commit = repo.Commit("initial commit", identity, identity, new LibGit2Sharp.CommitOptions());

            var snapshot = SnapshotId.Resolve(tempDir);

            await Assert.That(snapshot.CommitHash).IsEqualTo(commit.Sha);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task Resolve_NotGitRepo_ReturnsContentHash()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"ngraphiphy_snap_{Guid.NewGuid().ToString("N")[..8]}");
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "test.cs"), "// test");

            var snapshot = SnapshotId.Resolve(tempDir);

            await Assert.That(snapshot.CommitHash).IsNotNull().And.Length().IsEqualTo(16);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
