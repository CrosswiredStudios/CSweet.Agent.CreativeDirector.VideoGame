using CSweet.Agent.SDK;
using CSweet.WorkManagement.Contracts;

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
        Assert.Contains(manifest.Requires, x => x.Name == PlatformCapabilities.BusinessProfileRead);
        Assert.Contains(manifest.Requires, x => x.Name == PlatformCapabilities.FinanceProfileRead);
        Assert.Contains(manifest.Requires, x => x.Name == PlatformCapabilities.OrganizationSnapshotRead);
        Assert.Contains(manifest.Requires, x => x.Name == MemoryCapabilities.UserRead);
        Assert.Contains(manifest.Requires, x => x.Name == MemoryCapabilities.UserPropose);
        Assert.Contains(manifest.Requires, x => x.Name == MemoryCapabilities.BusinessRead);
        Assert.Contains(manifest.Requires, x => x.Name == MemoryCapabilities.BusinessPropose);
        Assert.Contains(manifest.Requires, x => x.Name == AgentLifecycleCapabilities.CompleteOnboarding);
        Assert.Contains(AgentLifecycleEvents.Onboarded, manifest.Events.Subscribes);
        Assert.Contains(PersonalTodoEvents.Available, manifest.Events.Subscribes);
        Assert.Contains(manifest.Requires, x => x.Name == PersonalTodoCapabilities.Read);
        Assert.Contains(manifest.Requires, x => x.Name == PersonalTodoCapabilities.Add);
        Assert.Contains(manifest.Requires, x => x.Name == PersonalTodoCapabilities.Requeue);
        Assert.Contains(manifest.Requires, x => x.Name == PersonalTodoCapabilities.Claim);
        Assert.Contains(manifest.Requires, x => x.Name == PersonalTodoCapabilities.Complete);
        Assert.Contains(manifest.Requires, x => x.Name == PersonalTodoCapabilities.Block);
        Assert.Contains(manifest.Requires, x => x.Name == PersonalTodoCapabilities.Release);
        Assert.Contains(manifest.Requires, x => x.Name == PersonalTodoCapabilities.Defer);
        Assert.Contains(WorkstreamEventNames.ArtifactPackageSubmittedV1, manifest.Events.Subscribes);
        Assert.Contains(WorkstreamEventNames.ArtifactPackageDecidedV1, manifest.Events.Subscribes);
        var profile = Assert.Single(manifest.WorkstreamProfiles.Provides);
        Assert.Equal("video-game-production.v2", profile.Key);
        Assert.Equal(2, profile.Version);
        Assert.True(File.Exists(Path.Combine(root,
            profile.DefinitionResource.Replace('/', Path.DirectorySeparatorChar))));
        Assert.Empty(manifest.Credentials);
        Assert.Equal("None", manifest.WebAccess.Mode);
        Assert.True(File.Exists(Path.Combine(
            root,
            manifest.Runtime.ProjectPath!.Replace('/', Path.DirectorySeparatorChar))));
    }

    [Fact]
    public async Task ProductionProfileOwnsLifecycleBoardTypesGatesAndStaffingDeclaratively()
    {
        using var profile = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(
            Path.Combine(RepositoryRoot(), "profiles", "video-game-production.v2.json")));
        var root = profile.RootElement;

        Assert.Equal("video-game-production.v2", root.GetProperty("key").GetString());
        var assignmentPolicy = System.Text.Json.JsonSerializer.Deserialize<WorkAssignmentPolicyTemplate>(
            root.GetProperty("assignmentPolicy"),
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.NotNull(assignmentPolicy);
        Assert.Contains(assignmentPolicy.SkillMatchMode, WorkSkillMatchModes.All);
        Assert.Equal(WorkSkillMatchModes.RequiredThenPreferred, assignmentPolicy.SkillMatchMode);
        Assert.Equal("video-game-production-board.v2", root.GetProperty("defaultBoardProfileKey").GetString());
        Assert.True(root.GetProperty("lifecycle").GetProperty("stages").GetArrayLength() >= 13);
        Assert.True(root.GetProperty("workItemTypes").GetArrayLength() >= 7);
        Assert.True(root.GetProperty("milestones").GetArrayLength() >= 5);
        Assert.True(root.GetProperty("staffing").GetProperty("requiredRoleKeys").GetArrayLength() >= 14);
        Assert.DoesNotContain(root.GetProperty("workItemTypes").EnumerateArray(), item =>
            string.IsNullOrWhiteSpace(item.GetProperty("key").GetString()));
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
