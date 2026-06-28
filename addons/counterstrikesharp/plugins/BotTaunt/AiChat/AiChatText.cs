using System.Text;

namespace BotTaunt.AiChat;

internal static class AiChatText
{
    private static readonly HashSet<char> ConsoleSeparators = [';', '`', '\u001b'];

    public static string Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        var lastWasSpace = false;
        foreach (var current in value)
        {
            if (char.IsControl(current) || char.IsWhiteSpace(current))
            {
                if (!lastWasSpace && builder.Length > 0)
                {
                    builder.Append(' ');
                    lastWasSpace = true;
                }

                continue;
            }

            builder.Append(current);
            lastWasSpace = false;
        }

        return builder.ToString().Trim();
    }

    public static string SanitizeForClientCommand(string? value, int maxChars)
    {
        var clean = Sanitize(value);
        if (clean.Length == 0)
        {
            return string.Empty;
        }

        maxChars = Math.Clamp(maxChars, 1, 800);

        var builder = new StringBuilder(clean.Length);
        var lastWasSpace = false;
        foreach (var current in clean)
        {
            if (ConsoleSeparators.Contains(current) || char.IsControl(current))
            {
                if (!lastWasSpace && builder.Length > 0)
                {
                    builder.Append(' ');
                    lastWasSpace = true;
                }

                continue;
            }

            builder.Append(current);
            lastWasSpace = false;
        }

        var sanitized = Sanitize(builder.ToString());
        return sanitized.Length <= maxChars ? sanitized : sanitized[..maxChars].Trim();
    }

    public static string NormalizeLooseText(string? value)
    {
        var clean = Sanitize(value);
        if (clean.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(clean.Length);
        foreach (var current in clean)
        {
            if (char.IsLetterOrDigit(current) || current >= 0x4e00 && current <= 0x9fff)
            {
                builder.Append(char.ToLowerInvariant(current));
            }
        }

        return builder.ToString();
    }

    public static string TrimAtSentenceBoundary(string value, int maxChars)
    {
        if (value.Length <= maxChars)
        {
            return value;
        }

        var capped = value[..maxChars].Trim();
        var split = capped.LastIndexOfAny(['。', '！', '？', '.', '!', '?', '，', ',']);
        return split > maxChars / 2 ? capped[..(split + 1)].Trim() : capped;
    }
}
