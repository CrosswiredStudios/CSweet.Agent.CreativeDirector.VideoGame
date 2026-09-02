using CSweet.Agent.SDK;
using CSweet.Agent.CreativeDirector.VideoGame;
using Microsoft.Extensions.Hosting;

if (args.Contains("--self-test", StringComparer.Ordinal))
{
    var agent = new VideoGameCreativeDirectorAgent();
    if (agent.AgentId != "com.csweet.video-game-creative-director" || agent.Version != "1.2.2")
        throw new InvalidOperationException("Video Game Creative Director identity self-test failed.");
    Console.WriteLine($"{agent.AgentId} {agent.Version} self-test passed.");
    return;
}

var builder = Host.CreateApplicationBuilder(args);
builder.AddCSweetAgent<VideoGameCreativeDirectorAgent>();
await builder.Build().RunAsync();
