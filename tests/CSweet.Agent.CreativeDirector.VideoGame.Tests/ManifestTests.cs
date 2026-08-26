using CSweet.Agent.SDK;

namespace CSweet.Agent.CreativeDirector.VideoGame.Tests;

public sealed class ManifestTests
{
    [Fact]
    public async Task Manifest_IsValidAndMatchesAgent()
    {
        var root = RepositoryRoot();
        var path = Path.Combine(root, "csweet-plugin.json");

        var manifest = await AgentManifestLoader.LoadAsync(path, CancellationToken.None);
        var agent = new VideoGameCreativeDirectorAgent();

        Assert.Equal(agent.AgentId, manifest.Id);
        Assert.Equal(agent.Version, manifest.Version);
        Assert.Contains(VideoGameCreativeDirectorAgent.GameVisionCapability, manifest.Capabilities);
        Assert.NotNull(manifest.RolePolicy);
        Assert.Equal("manager.v1", manifest.RolePolicy!.Profile);
        Assert.Equal(["creative-director"], manifest.RolePolicy.DeclaredRoleKeys);
        Assert.Equal(["video-game-development", "game-creative-direction"], manifest.RolePolicy.SpecializationKeys);
        Assert.Equal("AlwaysOn", manifest.Runtime.DefaultActivationMode);
        Assert.Empty(manifest.Credentials);
        Assert.Equal("None", manifest.WebAccess.Mode);
        Assert.True(File.Exists(Path.Combine(
            root,
            manifest.Runtime.ProjectPath!.Replace('/', Path.DirectorySeparatorChar))));
    }

    [Fact]
    public async Task Manifest_RequestsOneVideoGameRoleWithoutHiringOrSpendingAuthority()
    {
        var manifest = await AgentManifestLoader.LoadAsync(
            Path.Combine(RepositoryRoot(), "csweet-plugin.json"), CancellationToken.None);
        using var document = System.Text.Json.JsonDocument.Parse(
            await File.ReadAllTextAsync(Path.Combine(RepositoryRoot(), "csweet-plugin.json")));
        Assert.Equal("video-game-creative-director",
            document.RootElement.GetProperty("catalog").GetProperty("role").GetProperty("key").GetString());
        Assert.DoesNotContain(manifest.Requires, x =>
            x.Name.Contains("marketplace", StringComparison.OrdinalIgnoreCase) ||
            x.Name.Contains("hiring.workflow", StringComparison.OrdinalIgnoreCase) ||
            x.Name.Contains("budget", StringComparison.OrdinalIgnoreCase));
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CSweet.Agent.CreativeDirector.VideoGame.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }
}
