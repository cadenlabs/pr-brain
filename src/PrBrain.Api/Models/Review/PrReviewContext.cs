namespace PrBrain.Api.Models.Review;

public class PrReference
{
    public string Owner { get; set; } = string.Empty;
    public string Repo { get; set; } = string.Empty;
    public int Number { get; set; }
}

public class PrReviewContext
{
    // Layer 1 + 2: PR metadata and diff
    public string PrTitle { get; set; } = string.Empty;
    public int PrNumber { get; set; }
    public string PrBody { get; set; } = string.Empty;
    public string PrAuthor { get; set; } = string.Empty;
    public string Diff { get; set; } = string.Empty;
    public List<string> ChangedFiles { get; set; } = [];

    // Layer 3: Linked ticket / issue
    public string? TicketTitle { get; set; }
    public string? TicketBody { get; set; }
    public string? TicketNumber { get; set; }

    // Layer 4: Team standards
    public string? TeamStandards { get; set; }

    // Layer 5: Interface / contract files
    public List<FileContent> InterfaceFiles { get; set; } = [];

    // Layer 6: Related test files
    public List<FileContent> TestFiles { get; set; } = [];
}

public class FileContent
{
    public string Path { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
