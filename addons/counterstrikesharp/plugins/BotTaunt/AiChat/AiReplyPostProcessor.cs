namespace BotTaunt.AiChat;

internal sealed record ProcessedAiReply(bool ShouldSend, string Text, string Reason)
{
    public static ProcessedAiReply Send(string text) => new(true, text, "ok");

    public static ProcessedAiReply Drop(string reason) => new(false, string.Empty, reason);
}

internal static class AiReplyPostProcessor
{
    private static readonly string[] ForbiddenPrefixes =
    [
        "作为一个AI",
        "我是一个AI",
        "As an AI",
        "AI：",
        "AI:"
    ];

    public static ProcessedAiReply Process(string? rawReply, int maxLength, string? botName)
    {
        var clean = AiChatText.Sanitize(rawReply)
            .Trim('"', '\'', '“', '”', '‘', '’', '「', '」', '『', '』');
        clean = StripMarkdownFence(clean);
        clean = StripSpeakerPrefix(clean, botName);

        foreach (var prefix in ForbiddenPrefixes)
        {
            if (clean.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                clean = clean[prefix.Length..].TrimStart('：', ':', '，', ',', ' ', '\t');
            }
        }

        if (LooksLikeJson(clean))
        {
            return ProcessedAiReply.Drop("json-looking reply");
        }

        if (clean.Length > maxLength)
        {
            clean = AiChatText.TrimAtSentenceBoundary(clean, maxLength);
        }

        return string.IsNullOrWhiteSpace(clean)
            ? ProcessedAiReply.Drop("empty after processing")
            : ProcessedAiReply.Send(clean);
    }

    private static string StripMarkdownFence(string value)
    {
        var clean = value.Trim();
        if (clean.StartsWith("```", StringComparison.Ordinal))
        {
            clean = clean.Trim('`').Trim();
        }

        return clean;
    }

    private static string StripSpeakerPrefix(string text, string? botName)
    {
        var normalized = text.Trim();
        if (string.IsNullOrWhiteSpace(botName))
        {
            return normalized;
        }

        var speakerName = botName.Trim();
        for (var i = 0; i < 3; i++)
        {
            var before = normalized;
            normalized = StripOneSpeakerPrefix(normalized, speakerName);
            if (normalized == before)
            {
                break;
            }
        }

        return normalized;
    }

    private static string StripOneSpeakerPrefix(string text, string speakerName)
    {
        var normalized = text.Trim();

        if (normalized.StartsWith("[LBTV]", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized["[LBTV]".Length..].TrimStart(' ', ':', '：');
        }
        else if (normalized.StartsWith("【LBTV】", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized["【LBTV】".Length..].TrimStart(' ', ':', '：');
        }

        if (!normalized.StartsWith(speakerName, StringComparison.OrdinalIgnoreCase))
        {
            return text;
        }

        var rest = normalized[speakerName.Length..].TrimStart();
        if (rest.Length == 0)
        {
            return string.Empty;
        }

        if (rest[0] is ':' or '：')
        {
            return rest[1..].Trim().Trim('"', '\'', '“', '”', '‘', '’');
        }

        return text;
    }

    private static bool LooksLikeJson(string value)
    {
        return (value.StartsWith('{') && value.EndsWith('}'))
            || (value.StartsWith('[') && value.EndsWith(']'));
    }
}
