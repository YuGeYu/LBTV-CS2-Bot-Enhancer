using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BotTaunt.AiChat;

internal sealed class AiChatClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public AiChatClient(HttpClient httpClient, JsonSerializerOptions jsonOptions)
    {
        _httpClient = httpClient;
        _jsonOptions = jsonOptions;
    }

    public async Task<string?> CreateChatReplyAsync(AiChatRequest request, IReadOnlyList<AiChatLine> recentLines, BotTauntConfig config)
    {
        var messages = AiPromptBuilder.BuildChatMessages(request, recentLines, config);
        return await SendAsync(config, messages).ConfigureAwait(false);
    }

    public async Task<string?> CreateMvpReplyAsync(MvpAiTauntRequest request, BotTauntConfig config)
    {
        var messages = AiPromptBuilder.BuildMvpMessages(request);
        return await SendAsync(config, messages).ConfigureAwait(false);
    }

    private async Task<string?> SendAsync(BotTauntConfig config, IReadOnlyList<AiPromptMessage> messages)
    {
        if (string.IsNullOrWhiteSpace(config.AiApiUrl))
        {
            return null;
        }

        var useDefaultApi = ShouldUseDefaultApi(config);
        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            useDefaultApi ? new Uri(config.AiApiUrl) : BuildChatCompletionsUri(config.AiApiUrl));

        if (!string.IsNullOrWhiteSpace(config.AiApiKey))
        {
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.AiApiKey);
        }

        httpRequest.Headers.UserAgent.ParseAdd("LBTV-CS2-BotTaunt/1.0");

        object body = useDefaultApi
            ? BuildDefaultApiBody(config, messages)
            : BuildOpenAiBody(config, messages);

        httpRequest.Content = new StringContent(JsonSerializer.Serialize(body, _jsonOptions), Encoding.UTF8);
        httpRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(config.AiChatTimeoutSeconds));
        using var response = await _httpClient.SendAsync(httpRequest, timeout.Token).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token).ConfigureAwait(false);
        return ExtractReplyContent(document.RootElement);
    }

    private static bool ShouldUseDefaultApi(BotTauntConfig config)
    {
        if (config.AiApiMode.Equals("default", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (config.AiApiMode.Equals("openai", StringComparison.OrdinalIgnoreCase)
            || config.AiApiMode.Equals("chat-completions", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return config.AiApiUrl.Equals(BotTauntPlugin.DefaultAiApiUrl, StringComparison.OrdinalIgnoreCase)
            || config.AiApiUrl.Contains("/api/open/ai-chat", StringComparison.OrdinalIgnoreCase);
    }

    private static Uri BuildChatCompletionsUri(string baseUrl)
    {
        var trimmed = baseUrl.Trim().TrimEnd('/');
        return trimmed.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase)
            ? new Uri(trimmed)
            : new Uri($"{trimmed}/chat/completions");
    }

    private static Dictionary<string, object> BuildDefaultApiBody(BotTauntConfig config, IReadOnlyList<AiPromptMessage> messages)
    {
        var body = new Dictionary<string, object>
        {
            ["temperature"] = config.AiTemperature,
            ["messages"] = messages.Select(message => new { role = message.Role, content = message.Content }).ToArray()
        };

        if (!string.IsNullOrWhiteSpace(config.AiModel))
        {
            body["model"] = config.AiModel;
        }

        return body;
    }

    private static OpenAiChatRequestBody BuildOpenAiBody(BotTauntConfig config, IReadOnlyList<AiPromptMessage> messages)
    {
        var model = string.IsNullOrWhiteSpace(config.AiModel)
            ? BotTauntPlugin.DefaultThirdPartyAiModel
            : config.AiModel;
        return new OpenAiChatRequestBody(
            model,
            messages,
            config.AiMaxOutputTokens,
            config.AiTemperature,
            config.AiTopP,
            Stream: false);
    }

    private static string? ExtractReplyContent(JsonElement root)
    {
        if (root.TryGetProperty("reply", out var reply))
        {
            var content = ReadJsonText(reply);
            if (!string.IsNullOrWhiteSpace(content))
            {
                return content;
            }
        }

        if (root.TryGetProperty("choices", out var choices)
            && choices.ValueKind == JsonValueKind.Array
            && choices.GetArrayLength() > 0)
        {
            var choice = choices[0];
            if (choice.TryGetProperty("message", out var message)
                && message.TryGetProperty("content", out var messageContent))
            {
                var content = ReadJsonText(messageContent);
                if (!string.IsNullOrWhiteSpace(content))
                {
                    return content;
                }
            }

            if (choice.TryGetProperty("text", out var text))
            {
                var content = ReadJsonText(text);
                if (!string.IsNullOrWhiteSpace(content))
                {
                    return content;
                }
            }
        }

        if (root.TryGetProperty("content", out var rootContent))
        {
            return ReadJsonText(rootContent);
        }

        return null;
    }

    private static string? ReadJsonText(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return element.GetString();
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            var builder = new StringBuilder();
            foreach (var item in element.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    builder.Append(item.GetString());
                }
                else if (item.TryGetProperty("text", out var text))
                {
                    builder.Append(text.GetString());
                }
            }

            return builder.ToString();
        }

        return null;
    }

    private sealed record OpenAiChatRequestBody(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<AiPromptMessage> Messages,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        [property: JsonPropertyName("temperature")] double Temperature,
        [property: JsonPropertyName("top_p")] double? TopP,
        [property: JsonPropertyName("stream")] bool Stream);
}
