using System.Text.Json.Serialization;

namespace PrBrain.Api.Models.Copilot;

public class CopilotStreamChunk
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("object")]
    public string Object { get; set; } = "chat.completion.chunk";

    [JsonPropertyName("created")]
    public long Created { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    [JsonPropertyName("choices")]
    public List<StreamChoice> Choices { get; set; } = [];

    public static CopilotStreamChunk FromContent(string content) => new()
    {
        Choices = [new StreamChoice { Delta = new StreamDelta { Content = content } }]
    };

    public static CopilotStreamChunk RoleChunk() => new()
    {
        Choices = [new StreamChoice { Delta = new StreamDelta { Role = "assistant", Content = "" } }]
    };

    public static CopilotStreamChunk StopChunk() => new()
    {
        Choices = [new StreamChoice { Delta = new StreamDelta(), FinishReason = "stop" }]
    };
}

public class StreamChoice
{
    [JsonPropertyName("delta")]
    public StreamDelta Delta { get; set; } = new();

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }

    [JsonPropertyName("index")]
    public int Index { get; set; }
}

public class StreamDelta
{
    [JsonPropertyName("role")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Role { get; set; }

    [JsonPropertyName("content")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Content { get; set; }
}
