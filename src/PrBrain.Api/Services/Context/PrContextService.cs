using PrBrain.Api.Models.Review;
using PrBrain.Api.Services.GitHub;

namespace PrBrain.Api.Services.Context;

public class PrContextService(GitHubApiService gitHub, ILogger<PrContextService> logger)
{
    public async Task<PrReviewContext> AssembleAsync(PrReference pr, string userToken)
    {
        logger.LogInformation("Assembling context for PR #{Number} in {Owner}/{Repo}", pr.Number, pr.Owner, pr.Repo);

        // Layers 1 + 2: PR metadata + diff (parallel)
        var prTask = gitHub.GetPullRequestAsync(pr.Owner, pr.Repo, pr.Number, userToken);
        var diffTask = gitHub.GetPullRequestDiffAsync(pr.Owner, pr.Repo, pr.Number, userToken);
        var filesTask = gitHub.GetPullRequestFilesAsync(pr.Owner, pr.Repo, pr.Number, userToken);

        await Task.WhenAll(prTask, diffTask, filesTask);

        var pullRequest = prTask.Result;
        var diff = diffTask.Result;
        var files = filesTask.Result;

        var context = new PrReviewContext
        {
            PrTitle = pullRequest.Title,
            PrNumber = pullRequest.Number,
            PrBody = pullRequest.Body ?? string.Empty,
            PrAuthor = pullRequest.User.Login,
            Diff = diff,
            ChangedFiles = files.Select(f => f.FileName).ToList()
        };

        // Layer 3: Linked ticket (parallel with layers 4+5+6)
        var issueTask = gitHub.GetLinkedIssueAsync(pr.Owner, pr.Repo, pullRequest, userToken);

        // Layer 4: Team standards from .github/review-brain.md
        var standardsTask = gitHub.GetFileContentAsync(pr.Owner, pr.Repo, ".github/review-brain.md", userToken);

        // Layers 5 + 6: Interface + test files
        var relatedFilesTask = gitHub.GetRelatedFilesAsync(pr.Owner, pr.Repo, files, userToken);

        await Task.WhenAll(issueTask, standardsTask, relatedFilesTask);

        // Layer 3
        if (issueTask.Result is { } issue)
        {
            context.TicketNumber = issue.Number.ToString();
            context.TicketTitle = issue.Title;
            context.TicketBody = issue.Body;
        }

        // Layer 4
        context.TeamStandards = standardsTask.Result;

        // Layers 5 + 6
        var relatedFiles = relatedFilesTask.Result;
        context.InterfaceFiles = relatedFiles.Where(f => Path.GetFileName(f.Path).StartsWith("I")).ToList();
        context.TestFiles = relatedFiles.Where(f => f.Path.Contains("Test", StringComparison.OrdinalIgnoreCase)).ToList();

        logger.LogInformation(
            "Context assembled: diff={DiffLen} chars, ticket={HasTicket}, standards={HasStandards}, interfaces={InterfaceCount}, tests={TestCount}",
            diff.Length, context.TicketNumber != null, context.TeamStandards != null,
            context.InterfaceFiles.Count, context.TestFiles.Count);

        return context;
    }
}
