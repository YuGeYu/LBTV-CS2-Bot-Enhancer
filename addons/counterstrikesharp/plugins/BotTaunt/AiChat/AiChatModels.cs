namespace BotTaunt.AiChat;

internal sealed record AiChatRequest(
    string PlayerKey,
    string PlayerName,
    string PlayerTeam,
    bool PlayerAlive,
    int BotKey,
    string BotName,
    string BotTeam,
    string BattleSummary,
    string PlayerMessage,
    bool TeamChat);

internal sealed record MvpAiTauntRequest(
    int BotKey,
    string BotName,
    string BotTeam,
    string BattleSummary);
