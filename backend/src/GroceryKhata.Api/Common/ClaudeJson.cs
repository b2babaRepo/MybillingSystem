using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;

namespace GroceryKhata.Api.Common;

/// <summary>
/// The one way the app asks Claude for structured JSON: a cached system prompt, a JSON schema for the answer, the
/// shop's text and optionally one photo. It counts against the AI budget and logs usage. Callers only check and bound
/// what comes back; the model drafts and the shop confirms.
/// </summary>
public static class ClaudeJson
{
    public static Dictionary<string, JsonElement> Schema(string json) => JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;

    public static async Task<string> AskAsync(
        AnthropicClient client, string model, ILogger? logger, string what, string instructions, Dictionary<string, JsonElement> schema, string text,
        (string MediaType, string Base64)? photo = null, Effort? effort = null, int maxTokens = 2000, CancellationToken ct = default)
    {
        AiBudget.Take();
        var content = new List<ContentBlockParam>();
        if (photo is { } p)
        {
            content.Add(new ImageBlockParam
            {
                Source = new Base64ImageSource
                {
                    Data = p.Base64,
                    MediaType = p.MediaType switch { "image/png" => MediaType.ImagePng, "image/webp" => MediaType.ImageWebP, _ => MediaType.ImageJpeg },
                },
            });
        }

        content.Add(new TextBlockParam { Text = text });
        var response = await client.Messages.Create(new MessageCreateParams
        {
            Model = model,
            MaxTokens = maxTokens,
            OutputConfig = new OutputConfig { Effort = effort ?? Effort.Low, Format = new JsonOutputFormat { Schema = schema } },
            System = new List<TextBlockParam> { new() { Text = instructions, CacheControl = new CacheControlEphemeral() } },
            Messages = [new UserMessageParam { Content = content }],
        }, ct);
        AiBudget.Record(logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, what, model, response.Usage.InputTokens, response.Usage.OutputTokens, response.Usage.CacheReadInputTokens ?? 0);
        return response.StopReason == "end_turn"
            ? string.Concat(response.Content.Select(b => b.Value).OfType<TextBlock>().Select(t => t.Text))
            : throw new InvalidOperationException(photo is null ? $"No answer ({response.StopReason})." : $"The photo could not be read ({response.StopReason}).");
    }

    /// <summary>The Anthropic key when AI is switched on (Ai:Enabled and a key), else null: callers then leave their reader out.</summary>
    public static string? Key(IConfiguration configuration)
    {
        var key = configuration["Ai:ApiKey"] ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        return configuration.GetValue("Ai:Enabled", true) && !string.IsNullOrWhiteSpace(key) ? key : null;
    }
}
