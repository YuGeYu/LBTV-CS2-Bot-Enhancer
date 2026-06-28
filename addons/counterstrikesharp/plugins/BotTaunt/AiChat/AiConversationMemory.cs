using CounterStrikeSharp.API.Modules.Utils;

namespace BotTaunt.AiChat;

internal sealed record AiChatLine(string SpeakerName, bool IsBot, string Text);

internal sealed class AiConversationMemory
{
    private readonly object _gate = new();
    private readonly Queue<AiChatLine> _recent = new();
    private readonly Dictionary<string, float> _lastReplyByPlayer = new(StringComparer.Ordinal);
    private readonly Dictionary<int, float> _lastReplyByBot = new();
    private readonly List<float> _replyTimes = [];
    private int _repliesThisRound;

    public int RepliesThisRound
    {
        get
        {
            lock (_gate)
            {
                return _repliesThisRound;
            }
        }
    }

    public int RecentCount
    {
        get
        {
            lock (_gate)
            {
                return _recent.Count;
            }
        }
    }

    public void ResetRound()
    {
        lock (_gate)
        {
            _repliesThisRound = 0;
        }
    }

    public void ResetAll()
    {
        lock (_gate)
        {
            _recent.Clear();
            _lastReplyByPlayer.Clear();
            _lastReplyByBot.Clear();
            _replyTimes.Clear();
            _repliesThisRound = 0;
        }
    }

    public void AddPlayerMessage(string playerName, string text, BotTauntConfig config)
    {
        AddRecent(new AiChatLine(playerName, IsBot: false, AiChatText.TrimAtSentenceBoundary(text, 160)), config);
    }

    public void AddBotMessage(string botName, string text, BotTauntConfig config)
    {
        if (!config.AiStoreBotMessagesInRecentContext)
        {
            return;
        }

        AddRecent(new AiChatLine(botName, IsBot: true, AiChatText.TrimAtSentenceBoundary(text, 160)), config);
    }

    public IReadOnlyList<AiChatLine> GetRecentContext(BotTauntConfig config)
    {
        lock (_gate)
        {
            if (config.AiRecentContextMessages <= 0 || config.AiRecentContextMaxChars <= 0)
            {
                return [];
            }

            var selected = new List<AiChatLine>();
            var usedChars = 0;
            foreach (var line in _recent.Reverse())
            {
                var chars = line.SpeakerName.Length + line.Text.Length + 8;
                if (selected.Count >= config.AiRecentContextMessages || usedChars + chars > config.AiRecentContextMaxChars)
                {
                    break;
                }

                selected.Add(line);
                usedChars += chars;
            }

            selected.Reverse();
            return selected;
        }
    }

    public bool CanReply(string playerKey, int botKey, float now, BotTauntConfig config, out string reason)
    {
        lock (_gate)
        {
            PruneReplyTimes(now);
            if (config.AiMaxRepliesPerRound > 0 && _repliesThisRound >= config.AiMaxRepliesPerRound)
            {
                reason = "round reply budget reached";
                return false;
            }

            if (config.AiMaxRepliesPerMinute > 0 && _replyTimes.Count >= config.AiMaxRepliesPerMinute)
            {
                reason = "reply per minute budget reached";
                return false;
            }

            var lastGlobal = _replyTimes.Count > 0 ? _replyTimes[^1] : float.MinValue;
            if (now - lastGlobal < config.AiGlobalCooldownSeconds)
            {
                reason = "global cooldown";
                return false;
            }

            if (_lastReplyByPlayer.TryGetValue(playerKey, out var playerLast)
                && now - playerLast < config.AiPerPlayerCooldownSeconds)
            {
                reason = "player cooldown";
                return false;
            }

            if (_lastReplyByBot.TryGetValue(botKey, out var botLast)
                && now - botLast < config.AiPerBotCooldownSeconds)
            {
                reason = "bot cooldown";
                return false;
            }

            reason = "ok";
            return true;
        }
    }

    public void MarkReplyScheduled(string playerKey, int botKey, float now)
    {
        lock (_gate)
        {
            _lastReplyByPlayer[playerKey] = now;
            _lastReplyByBot[botKey] = now;
            _replyTimes.Add(now);
            _repliesThisRound++;
            PruneReplyTimes(now);
        }
    }

    private void AddRecent(AiChatLine line, BotTauntConfig config)
    {
        lock (_gate)
        {
            _recent.Enqueue(line);
            while (_recent.Count > Math.Max(1, config.AiRecentContextMessages + 4))
            {
                _recent.Dequeue();
            }
        }
    }

    private void PruneReplyTimes(float now)
    {
        _replyTimes.RemoveAll(time => now - time > 60.0f);
    }
}
