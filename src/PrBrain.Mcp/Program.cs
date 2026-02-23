using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PrBrain.Api.Services.Ai;
using PrBrain.Api.Services.Context;
using PrBrain.Api.Services.GitHub;
using PrBrain.Mcp.Tools;

var builder = Host.CreateApplicationBuilder(args);

// MCP uses stdio for its protocol — ALL logging must go to stderr, never stdout
builder.Logging.ClearProviders();
builder.Logging.AddConsole(o =>
{
    // Route every log level to stderr so stdout stays clean for JSON-RPC
    o.LogToStandardErrorThreshold = LogLevel.Trace;
});
builder.Logging.SetMinimumLevel(LogLevel.Warning); // suppress noisy info lines

// GitHub HTTP client
builder.Services.AddHttpClient("GitHub", client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "pr-brain-cadenlabs");
    client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
    client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
});

// Core services (reused from PrBrain.Api)
builder.Services.AddSingleton<GitHubModelsClient>();
builder.Services.AddScoped<GitHubApiService>();
builder.Services.AddScoped<PrContextService>();
builder.Services.AddScoped<ReviewGeneratorService>();

// MCP server — stdio transport (VS Code spawns this process)
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly(typeof(PrReviewTool).Assembly);

await builder.Build().RunAsync();
