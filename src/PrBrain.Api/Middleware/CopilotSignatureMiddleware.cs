using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PrBrain.Api.Middleware;

public class CopilotSignatureMiddleware(RequestDelegate next, IHttpClientFactory httpClientFactory, ILogger<CopilotSignatureMiddleware> logger)
{
    private const string PublicKeysUrl = "https://api.github.com/meta/public_keys/copilot_api";

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path == "/health")
        {
            await next(context);
            return;
        }

        var keyIdentifier = context.Request.Headers["X-GitHub-Public-Key-Identifier"].ToString();
        var signature = context.Request.Headers["X-GitHub-Public-Key-Signature"].ToString();

        if (string.IsNullOrEmpty(keyIdentifier) || string.IsNullOrEmpty(signature))
        {
            logger.LogWarning("Missing Copilot signature headers");
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("Missing signature headers");
            return;
        }

        // Buffer the body so we can read it for verification AND later for the endpoint
        context.Request.EnableBuffering();
        var body = await new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true).ReadToEndAsync();
        context.Request.Body.Position = 0;

        var isValid = await VerifySignatureAsync(keyIdentifier, signature, Encoding.UTF8.GetBytes(body));

        if (!isValid)
        {
            logger.LogWarning("Invalid Copilot signature from key: {KeyId}", keyIdentifier);
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("Invalid signature");
            return;
        }

        await next(context);
    }

    private async Task<bool> VerifySignatureAsync(string keyIdentifier, string signature, byte[] payload)
    {
        try
        {
            var client = httpClientFactory.CreateClient("GitHub");
            var response = await client.GetFromJsonAsync<PublicKeysResponse>(PublicKeysUrl);
            var key = response?.PublicKeys?.FirstOrDefault(k => k.KeyIdentifier == keyIdentifier);

            if (key is null)
            {
                logger.LogWarning("Public key not found for identifier: {KeyId}", keyIdentifier);
                return false;
            }

            using var ecdsa = ECDsa.Create();
            ecdsa.ImportFromPem(key.Key);

            var signatureBytes = Convert.FromBase64String(signature);
            return ecdsa.VerifyData(payload, signatureBytes, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Signature verification failed");
            return false;
        }
    }
}

file class PublicKeysResponse
{
    [JsonPropertyName("public_keys")]
    public List<PublicKey>? PublicKeys { get; set; }
}

file class PublicKey
{
    [JsonPropertyName("key_identifier")]
    public string KeyIdentifier { get; set; } = string.Empty;

    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;
}
