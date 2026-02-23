using PrBrain.Api.Models.Review;
using System.Text;

namespace PrBrain.Api.Services.Ai;

public class ReviewGeneratorService(GitHubModelsClient modelsClient, ILogger<ReviewGeneratorService> logger)
{
    private const string SystemPrompt = """
        You are PR Brain, a context-aware code review assistant built by CadenLabs.

        You perform deep, architectural code reviews by analysing not just WHAT changed,
        but WHY it changed and whether it honours the system's contracts, requirements, and standards.

        Your reviews are:
        - Specific: every point references actual method names, file paths, or line numbers
        - Honest: you don't invent issues, but you don't soften real ones either
        - Actionable: every issue has a clear, concrete fix
        - Contextual: you cross-reference the ticket, interfaces, and team standards

        You never say "looks good to me" without substance.
        You never flag style issues as critical — that's what linters are for.
        """;

    public async IAsyncEnumerable<string> GenerateAsync(PrReviewContext context)
    {
        var prompt = BuildPrompt(context);
        logger.LogInformation("Generating review for PR #{PrNumber}, prompt length: {Len} chars", context.PrNumber, prompt.Length);

        await foreach (var chunk in modelsClient.StreamAsync(SystemPrompt, prompt))
            yield return chunk;
    }

    private static string BuildPrompt(PrReviewContext ctx)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"## PR #{ctx.PrNumber}: {ctx.PrTitle}");
        sb.AppendLine($"**Author:** {ctx.PrAuthor}");
        sb.AppendLine();

        // Layer 3: Ticket
        if (ctx.TicketTitle is not null)
        {
            sb.AppendLine("---");
            sb.AppendLine($"## Linked Ticket #{ctx.TicketNumber}: {ctx.TicketTitle}");
            sb.AppendLine(ctx.TicketBody?.Truncate(2000) ?? "No description.");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("---");
            sb.AppendLine("## Linked Ticket");
            sb.AppendLine("⚠️ No linked issue found in PR description.");
            sb.AppendLine();
        }

        // Layer 4: Team standards
        sb.AppendLine("---");
        sb.AppendLine("## Team Standards (.github/review-brain.md)");
        sb.AppendLine(ctx.TeamStandards ?? "No review-brain.md found. Apply general engineering best practices.");
        sb.AppendLine();

        // Layer 5: Interface / contract files
        if (ctx.InterfaceFiles.Count > 0)
        {
            sb.AppendLine("---");
            sb.AppendLine("## Interface Contracts (what this code must honour)");
            foreach (var f in ctx.InterfaceFiles)
            {
                sb.AppendLine($"### {f.Path}");
                sb.AppendLine("```csharp");
                sb.AppendLine(f.Content.Truncate(1500));
                sb.AppendLine("```");
            }
            sb.AppendLine();
        }

        // Layer 6: Test files
        if (ctx.TestFiles.Count > 0)
        {
            sb.AppendLine("---");
            sb.AppendLine("## Existing Tests (what's already covered)");
            foreach (var f in ctx.TestFiles)
            {
                sb.AppendLine($"### {f.Path}");
                sb.AppendLine("```csharp");
                sb.AppendLine(f.Content.Truncate(1500));
                sb.AppendLine("```");
            }
            sb.AppendLine();
        }

        // Layers 1 + 2: Diff
        sb.AppendLine("---");
        sb.AppendLine("## Changed Files");
        sb.AppendLine(string.Join("\n", ctx.ChangedFiles.Select(f => $"- {f}")));
        sb.AppendLine();
        sb.AppendLine("## Diff");
        sb.AppendLine("```diff");
        sb.AppendLine(ctx.Diff.Truncate(8000));
        sb.AppendLine("```");

        // Output instructions
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine("""
            ## Your Review

            Using ALL the context above, produce a review in this exact format:

            ## PR Review — {PR title} (#{number})

            ### 🔴 Critical Issues
            Contract violations, security issues, data loss risks — must fix before merge.
            If none: write "None identified."

            ### 🟡 Important Issues
            Business logic gaps, requirement coverage gaps, performance concerns.
            If none: write "None identified."

            ### 🟢 Test Coverage Gaps
            Specific scenarios not covered that should be.
            If none: write "Coverage looks adequate."

            ### 💡 Suggestions
            Non-blocking improvements.

            ### ✅ What's Good
            Specific things done well. Always include at least one.

            ### 📋 Ticket Coverage
            Does this PR fully implement what the linked ticket requires?
            Call out any requirements that are missing or only partially done.
            """);

        return sb.ToString();
    }
}

file static class StringExtensions
{
    public static string Truncate(this string s, int max) =>
        s.Length <= max ? s : s[..max] + $"\n... [truncated {s.Length - max} chars]";
}
