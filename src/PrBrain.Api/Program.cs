using PrBrain.Api.Endpoints;
using PrBrain.Api.Middleware;
using PrBrain.Api.Services.Ai;
using PrBrain.Api.Services.Context;
using PrBrain.Api.Services.GitHub;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .AddUserSecrets<Program>(optional: true);

// HTTP client for GitHub API calls (signature verification + API)
builder.Services.AddHttpClient("GitHub", client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "pr-brain-cadenlabs");
    client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
    client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
});

// Core services
builder.Services.AddSingleton<GitHubModelsClient>();
builder.Services.AddScoped<GitHubApiService>();
builder.Services.AddScoped<PrContextService>();
builder.Services.AddScoped<ReviewGeneratorService>();

builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Information);
});

var app = builder.Build();

// Verify GitHub's Copilot Extension signature on every request
app.UseMiddleware<CopilotSignatureMiddleware>();

app.MapCopilotEndpoints();

app.Run();
