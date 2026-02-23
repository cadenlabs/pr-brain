using System.Text.RegularExpressions;
using Octokit;
using PrBrain.Api.Models.Review;

namespace PrBrain.Api.Services.GitHub;

public class GitHubApiService(ILogger<GitHubApiService> logger)
{
    private static readonly Regex IssueRefPattern =
        new(@"(?:closes?|fixes?|resolves?)\s*#(\d+)", RegexOptions.IgnoreCase);

    private GitHubClient BuildClient(string userToken) => new(new ProductHeaderValue("pr-brain"))
    {
        Credentials = new Credentials(userToken)
    };

    public async Task<PullRequest> GetPullRequestAsync(string owner, string repo, int number, string userToken)
    {
        var client = BuildClient(userToken);
        return await client.PullRequest.Get(owner, repo, number);
    }

    public async Task<string> GetPullRequestDiffAsync(string owner, string repo, int number, string userToken)
    {
        try
        {
            // Use raw HTTP with diff accept header — Octokit doesn't expose this directly
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Add("Authorization", $"Bearer {userToken}");
            http.DefaultRequestHeaders.Add("User-Agent", "pr-brain");
            http.DefaultRequestHeaders.Add("Accept", "application/vnd.github.diff");

            var url = $"https://api.github.com/repos/{owner}/{repo}/pulls/{number}";
            var response = await http.GetStringAsync(url);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch diff, falling back to file list");
            return string.Empty;
        }
    }

    public async Task<IReadOnlyList<PullRequestFile>> GetPullRequestFilesAsync(string owner, string repo, int number, string userToken)
    {
        var client = BuildClient(userToken);
        return await client.PullRequest.Files(owner, repo, number);
    }

    public async Task<Issue?> GetLinkedIssueAsync(string owner, string repo, PullRequest pr, string userToken)
    {
        try
        {
            var match = IssueRefPattern.Match(pr.Body ?? string.Empty);
            if (!match.Success) return null;

            var issueNumber = int.Parse(match.Groups[1].Value);
            var client = BuildClient(userToken);
            return await client.Issue.Get(owner, repo, issueNumber);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not fetch linked issue");
            return null;
        }
    }

    public async Task<string?> GetFileContentAsync(string owner, string repo, string path, string userToken)
    {
        try
        {
            var client = BuildClient(userToken);
            var contents = await client.Repository.Content.GetAllContents(owner, repo, path);
            return contents.FirstOrDefault()?.Content is { } encoded
                ? System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(encoded.Replace("\n", "")))
                : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<Models.Review.FileContent>> GetRelatedFilesAsync(
        string owner, string repo,
        IReadOnlyList<PullRequestFile> changedFiles,
        string userToken)
    {
        var result = new List<Models.Review.FileContent>();

        // For each changed file, look for matching interface and test files
        var relatedPaths = new HashSet<string>();

        foreach (var file in changedFiles.Take(10)) // cap to avoid token explosion
        {
            var fileName = Path.GetFileNameWithoutExtension(file.FileName);
            var dir = Path.GetDirectoryName(file.FileName) ?? "";

            // Interface pattern: IService.cs for Service.cs
            relatedPaths.Add(Path.Combine(dir, $"I{fileName}.cs").Replace("\\", "/"));

            // Test patterns
            relatedPaths.Add(file.FileName.Replace("src/", "tests/").Replace(".cs", "Tests.cs"));
            relatedPaths.Add(file.FileName.Replace(".cs", "Tests.cs"));
        }

        foreach (var path in relatedPaths)
        {
            var content = await GetFileContentAsync(owner, repo, path, userToken);
            if (content is not null)
                result.Add(new Models.Review.FileContent { Path = path, Content = content });
        }

        return result;
    }
}
