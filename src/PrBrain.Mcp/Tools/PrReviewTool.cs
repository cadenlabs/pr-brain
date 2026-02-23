using System.ComponentModel;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Server;
using PrBrain.Api.Models.Review;
using PrBrain.Api.Services.Ai;
using PrBrain.Api.Services.Context;

namespace PrBrain.Mcp.Tools;

[McpServerToolType]
public class PrReviewTool(PrContextService contextService, ReviewGeneratorService reviewer, IConfiguration config)
{
    private static readonly Regex UrlPattern =
        new(@"github\.com/([^/]+)/([^/]+)/pull/(\d+)", RegexOptions.IgnoreCase);

    private static readonly Regex RefPattern =
        new(@"([a-zA-Z0-9_-]+)/([a-zA-Z0-9_.-]+)[^\d#]*#(\d+)", RegexOptions.IgnoreCase);

    [McpServerTool]
    [Description("Review a GitHub Pull Request with full context — diff, linked ticket, team standards, interface contracts, and test coverage.")]
    public async Task<string> ReviewPr(
        [Description("GitHub PR URL (https://github.com/owner/repo/pull/42) or shorthand (owner/repo #42)")] string pr)
    {
        // Token comes from environment — never from the chat
        // .NET env var provider maps GitHub__Token → GitHub:Token (__ becomes :)
        var githubToken = config["GitHub:Token"]
            ?? throw new InvalidOperationException("GitHub__Token env var is not configured. Add it to the MCP server env in mcp.json.");

        var prRef = ParsePrReference(pr);
        if (prRef is null)
            return "❌ Could not parse PR reference. Use: https://github.com/owner/repo/pull/42 or owner/repo #42";

        var context = await contextService.AssembleAsync(prRef, githubToken);
        var review = new System.Text.StringBuilder();

        await foreach (var chunk in reviewer.GenerateAsync(context))
            review.Append(chunk);

        return review.ToString();
    }

    private static PrReference? ParsePrReference(string input)
    {
        var urlMatch = UrlPattern.Match(input);
        if (urlMatch.Success)
            return new PrReference
            {
                Owner = urlMatch.Groups[1].Value,
                Repo = urlMatch.Groups[2].Value,
                Number = int.Parse(urlMatch.Groups[3].Value)
            };

        var refMatch = RefPattern.Match(input);
        if (refMatch.Success)
            return new PrReference
            {
                Owner = refMatch.Groups[1].Value,
                Repo = refMatch.Groups[2].Value,
                Number = int.Parse(refMatch.Groups[3].Value)
            };

        return null;
    }
}
