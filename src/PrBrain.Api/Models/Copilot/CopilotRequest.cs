using System.Text.Json.Serialization;

namespace PrBrain.Api.Models.Copilot;

public class CopilotRequest
{
    [JsonPropertyName("messages")]
    public List<CopilotMessage> Messages { get; set; } = [];

    [JsonPropertyName("stream")]
    public bool Stream { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }
}

public class CopilotMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("copilot_references")]
    public List<CopilotReference>? CopilotReferences { get; set; }
}

public class CopilotReference
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public CopilotReferenceData? Data { get; set; }
}

public class CopilotReferenceData
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("ownerLogin")]
    public string? OwnerLogin { get; set; }
}
