using Azure;
using Azure.AI.Inference;

namespace PrBrain.Api.Services.Ai;

public class GitHubModelsClient
{
    private readonly ChatCompletionsClient _client;
    private readonly string _model;

    public GitHubModelsClient(IConfiguration config)
    {
        var endpoint = config["GitHubModels:Endpoint"] ?? "https://models.inference.ai.azure.com";
        var token = config["GitHubModels:Token"] ?? throw new InvalidOperationException("GitHubModels:Token is required");
        _model = config["GitHubModels:Model"] ?? "gpt-4o";

        _client = new ChatCompletionsClient(new Uri(endpoint), new AzureKeyCredential(token));
    }

    public async IAsyncEnumerable<string> StreamAsync(string systemPrompt, string userPrompt)
    {
        var options = new ChatCompletionsOptions
        {
            Model = _model,
            Messages =
            {
                new ChatRequestSystemMessage(systemPrompt),
                new ChatRequestUserMessage(userPrompt)
            }
        };

        var response = await _client.CompleteStreamingAsync(options);

        await foreach (var chunk in response)
        {
            if (!string.IsNullOrEmpty(chunk.ContentUpdate))
                yield return chunk.ContentUpdate;
        }
    }
}
