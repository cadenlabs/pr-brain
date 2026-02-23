using System.Text.Json;
using System.Text.RegularExpressions;
using PrBrain.Api.Models.Copilot;
using PrBrain.Api.Models.Review;
using PrBrain.Api.Services.Ai;
using PrBrain.Api.Services.Context;

namespace PrBrain.Api.Endpoints;

public static class CopilotEndpoints
{
    private static readonly Regex PrUrlPattern =
        new(@"github\.com/([^/]+)/([^/]+)/pull/(\d+)", RegexOptions.IgnoreCase);

    private static readonly Regex PrRefPattern =
        new(@"(?:in\s+)?([a-zA-Z0-9_-]+)/([a-zA-Z0-9_.-]+)[^\d]*#(\d+)", RegexOptions.IgnoreCase);

    private static readonly Regex PrNumberPattern =
        new(@"#(\d+)", RegexOptions.IgnoreCase);

    public static void MapCopilotEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "pr-brain" }));

        app.MapPost("/", async (HttpContext http, PrContextService contextService, ReviewGeneratorService reviewer) =>
        {
            // Extract user's GitHub token from Copilot Extension header
            var userToken = http.Request.Headers["X-GitHub-Token"].ToString();
            if (string.IsNullOrEmpty(userToken))
            {
                http.Response.StatusCode = 401;
                await http.Response.WriteAsync("Missing X-GitHub-Token");
                return;
            }

            // Parse the Copilot request body
            CopilotRequest? request;
            try
            {
                request = await JsonSerializer.DeserializeAsync<CopilotRequest>(http.Request.Body);
            }
            catch
            {
                http.Response.StatusCode = 400;
                await http.Response.WriteAsync("Invalid request body");
                return;
            }

            var userMessage = request?.Messages.LastOrDefault(m => m.Role == "user")?.Content ?? string.Empty;

            // Set up SSE response
            http.Response.Headers.ContentType = "text/event-stream";
            http.Response.Headers.CacheControl = "no-cache";
            http.Response.Headers.Connection = "keep-alive";

            // Parse PR reference from the user's message
            var prRef = ParsePrReference(userMessage);

            if (prRef is null)
            {
                await StreamMessageAsync(http, """
                    👋 Hey! I'm **PR Brain** — I review PRs with full context, not just the diff.

                    Tell me which PR to review:
                    ```
                    @pr-brain review https://github.com/owner/repo/pull/42
                    @pr-brain review owner/repo #42
                    ```
                    I'll cross-reference the linked ticket, team standards, interface contracts, and test coverage.
                    """);
                await StreamDoneAsync(http);
                return;
            }

            // Stream progress updates while assembling context
            await StreamMessageAsync(http, $"⚙️ Fetching context for **{prRef.Owner}/{prRef.Repo} #{prRef.Number}**...\n\n");

            PrReviewContext context;
            try
            {
                context = await contextService.AssembleAsync(prRef, userToken);
            }
            catch (Exception ex)
            {
                await StreamMessageAsync(http, $"❌ Failed to fetch PR: {ex.Message}\n\nCheck that the PR exists and the GitHub App has access to the repo.");
                await StreamDoneAsync(http);
                return;
            }

            // Stream context summary
            var contextSummary = BuildContextSummary(context);
            await StreamMessageAsync(http, contextSummary);

            // Stream the review token by token
            await foreach (var chunk in reviewer.GenerateAsync(context))
            {
                await StreamChunkAsync(http, chunk);
            }

            await StreamDoneAsync(http);
        });
    }

    private static PrReference? ParsePrReference(string message)
    {
        // Try: https://github.com/owner/repo/pull/42
        var urlMatch = PrUrlPattern.Match(message);
        if (urlMatch.Success)
            return new PrReference
            {
                Owner = urlMatch.Groups[1].Value,
                Repo = urlMatch.Groups[2].Value,
                Number = int.Parse(urlMatch.Groups[3].Value)
            };

        // Try: owner/repo #42 or in owner/repo #42
        var refMatch = PrRefPattern.Match(message);
        if (refMatch.Success)
            return new PrReference
            {
                Owner = refMatch.Groups[1].Value,
                Repo = refMatch.Groups[2].Value,
                Number = int.Parse(refMatch.Groups[3].Value)
            };

        return null;
    }

    private static string BuildContextSummary(PrReviewContext ctx)
    {
        var lines = new List<string>
        {
            $"**📋 PR #{ctx.PrNumber}:** {ctx.PrTitle} by @{ctx.PrAuthor}",
            $"**📁 Files changed:** {ctx.ChangedFiles.Count}",
            ctx.TicketNumber is not null
                ? $"**🎫 Ticket:** #{ctx.TicketNumber} — {ctx.TicketTitle}"
                : "**🎫 Ticket:** ⚠️ None linked",
            ctx.TeamStandards is not null
                ? "**📖 Standards:** ✅ `.github/review-brain.md` loaded"
                : "**📖 Standards:** ⚠️ No `review-brain.md` found — using defaults",
            ctx.InterfaceFiles.Count > 0
                ? $"**🔗 Interfaces:** {ctx.InterfaceFiles.Count} contract file(s) loaded"
                : "**🔗 Interfaces:** None found",
            ctx.TestFiles.Count > 0
                ? $"**🧪 Tests:** {ctx.TestFiles.Count} test file(s) loaded"
                : "**🧪 Tests:** None found"
        };

        return string.Join("\n", lines) + "\n\n---\n\n";
    }

    private static async Task StreamMessageAsync(HttpContext http, string message)
    {
        // Send role first
        await WriteEventAsync(http, CopilotStreamChunk.RoleChunk());
        await WriteEventAsync(http, CopilotStreamChunk.FromContent(message));
    }

    private static async Task StreamChunkAsync(HttpContext http, string content)
    {
        await WriteEventAsync(http, CopilotStreamChunk.FromContent(content));
    }

    private static async Task StreamDoneAsync(HttpContext http)
    {
        await WriteEventAsync(http, CopilotStreamChunk.StopChunk());
        await http.Response.WriteAsync("data: [DONE]\n\n");
        await http.Response.Body.FlushAsync();
    }

    private static async Task WriteEventAsync(HttpContext http, CopilotStreamChunk chunk)
    {
        var json = JsonSerializer.Serialize(chunk);
        await http.Response.WriteAsync($"data: {json}\n\n");
        await http.Response.Body.FlushAsync();
    }
}
