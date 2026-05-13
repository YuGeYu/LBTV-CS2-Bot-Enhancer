using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Utils;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace BotTaunt;

[MinimumApiVersion(304)]
public sealed class BotTauntPlugin : BasePlugin
{
    public override string ModuleName => "BotTaunt";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleAuthor => "OpenAI";
    public override string ModuleDescription => "Lets bots taunt human players after kills.";

    private const int MaxRoundTaunts = 9;
    private const float BotCooldownSeconds = 30.0f;
    private const float NormalTauntChance = 0.50f;
    private const float SpecialTauntChance = 0.70f;
    private const float MvpTauntChance = 0.40f;
    private const float AiChatCooldownSeconds = 1.0f;
    private const int AiChatTimeoutSeconds = 15;
    private const int MaxPlayerMessageLength = 300;
    private const int MaxAiReplyLength = 220;
    private const float MultiKillWindowSeconds = 5.0f;
    private const int MultiKillThreshold = 3;
    private const int RoundKillTauntThreshold = 5;
    private const float OpeningTrashTalkIntervalSeconds = 1.15f;
    private const float LateRoundPollSeconds = 1.0f;

    private const string AiApiUrl = "https://maomaochongmiao.600318.xyz/api/open/ai-chat";
    private const string AiApiKey = "mmc_owner_698539c6dc8e829421bcbe793f8d08c97ac4e15469007c290bee8d3ac3a46c6e";

    private const string LbtvPrefix = "[LBTV]";
    private const string ChatColorRed = "\u0002";
    private const string ChatColorDefault = "\u0001";
    private static readonly HttpClient AiHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(AiChatTimeoutSeconds)
    };

    private readonly Random _random = new();
    private readonly Dictionary<int, float> _nextTauntTimeByBot = new();
    private readonly Dictionary<int, int> _roundKillsByBot = new();
    private readonly Dictionary<int, Queue<float>> _recentKillTimesByBot = new();
    private readonly HashSet<int> _multiKillTauntedBots = new();
    private readonly HashSet<int> _aceTauntedBots = new();
    private float _nextAiChatTime;
    private float _lastChatHandledTime;
    private string _lastChatSignature = string.Empty;
    private bool _aiReplyInFlight;
    private AiChatRequest? _pendingAiChatRequest;
    private int _roundTauntCount;
    private bool _clutchTauntedThisRound;
    private bool _saveTauntedThisRound;
    private bool _roundKillTauntedThisRound;
    private bool _openingTrashTalkStarted;
    private int _roundSerial;
    private bool _roundEnded = true;
    private bool _enabled = true;
    private bool _aiChatEnabled = true;

    private static readonly string[] NormalTaunts =
    {
        "就这？BOT 都看不下去了。",
        "你这波像是在给我送训练靶。",
        "别急，下一把也许能开枪。",
        "这个身位，教科书级白给。",
        "谢谢你的枪，我会比你用得好。",
        "你的准星是不是还没加载出来？",
        "反应慢半拍，坟头多一块。",
        "你刚露头，我已经想好台词了。",
        "这不是对枪，这是单方面验货。",
        "I just f*king destroy you!",
        "EZ",
        "收徒",
        "上门安装假肢",
        "建议先打 BOT 简单难度。",
        "你们的钱留着买棺材吧，反正用不上",
        "你们的大脑是不是还在用拨号上网？",
        "人类进化的时候你们是不是躲起来了？",
        "你们的压枪轨迹像地震波形图",
        "建议你们卸载游戏，这样对大家都好",
        "你们的鼠标灵敏度是不是调反了？",
        "我奶奶来打都比你们强，可惜她没有显卡"
    };

    private static readonly string[] HeadshotTaunts =
    {
        "头皮还在吗？我确认一下。",
        "这一枪，头盔都嫌你菜。",
        "别抬头，抬头就没了。",
        "一发入魂，顺便帮你关机。",
        "你的脑袋比包点还好预瞄。",
        "头盔尽力了，你没有。",
        "爆头线都不会躲，还敢peek？",
        "看我用小手枪点爆你们的头",
        "准星路过，你人没了。",
        "颗秒！！！！！",
        "我杀你们比删除临时文件还简单，至少文件还会占用0.1秒",
        "喜欢露",
        "建议你们重开，我是说人生，不是游戏",
        "我杀你们不需要瞄准，只需要存在",
        "我分析了你们的DNA，发现你们和草履虫是近亲",
        "你的键盘是不是只有W键能按？",
        "你们的游戏时长应该算入'公益时长'，毕竟在给AI做慈善",
        "我即将上传这段录像到P站，标题是'人类被AI羞辱'"
    };

    private static readonly string[] KnifeTaunts =
    {
        "背身给刀，你是真大方。",
        "刀都掏出来了，你还没反应。",
        "这一刀不疼，丢人比较疼。",
        "近战教学结束，学费是一条命。",
        "你耳机是不是只剩装饰作用？",
        "我拿刀都比你拿枪有威胁。",
        "这刀必须进回放，太下饭了。",
        "被 BOT 刀了，今晚别睡太早。"
    };

    private static readonly string[] OpeningTrashTalks =
    {
        "开局先点名，等会按顺序送你们回家。",
        "你们五个站一起，也像五个移动补给箱。",
        "别买甲了，买了也是给我验货。",
        "这把我先热身，你们先练投降。",
        "看到你们进服，我已经开始算战绩了。",
        "别急着抢点，先抢一下遗言。",
        "你们的战术是不是叫集体白给？",
        "开局提醒一下，准星在屏幕中间。",
        "我建议你们先商量谁第一个倒。",
        "这局不用暂停，菜不会因为暂停变熟。",
        "别报点了，你们的位置我用脚都能猜。",
        "你们这阵容，像临时拼的掉分车队。",
        "枪声一响，谁菜谁先躺。",
        "我看你们经济不用管，反正也活不到花钱。",
        "别给自己压力，你们本来就没机会。",
        "开局就这么安静，是都在查怎么开枪吗？",
        "你们先跑图，我负责把你们送回出生点。",
        "这把我不针对谁，反正你们都差不多。",
        "建议全员静步，至少死得有仪式感。",
        "别急着封烟，先把自己脑子封上。",
        "等会别说运气差，你们是基础差。",
        "我先把话放这，比分会比你们嘴硬。",
        "你们这压迫感，主要压迫的是队友血压。",
        "别学职业哥了，先学会别白给。",
        "md队友1w块不给老子发枪",
        "这架不住？",
        "对面有一个是人啊？",
        "没有人类了。",
        "bot",
        "别逗我机哥笑了",
        "建议你们直接投降，节省大家时间",
        "对面的还是去堵桥吧",
        "电脑玩家",
        "有钳子给你了呗"
    };

    public override void Load(bool hotReload)
    {
        RegisterEventHandler<EventRoundStart>(OnRoundStart);
        RegisterEventHandler<EventRoundFreezeEnd>(OnRoundFreezeEnd);
        RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
        RegisterEventHandler<EventRoundMvp>(OnRoundMvp, HookMode.Pre);
        RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
        RegisterListener<Listeners.OnPlayerChat>(OnPlayerChat);
        AddCommandListener("say", OnSayCommand, HookMode.Post);
        AddCommandListener("say_team", OnSayTeamCommand, HookMode.Post);
        RegisterListener<Listeners.OnMapStart>(_ => ResetState());
    }

    [ConsoleCommand("lbtv_bot_taunt", "Enable or disable LBTV bot taunts. Usage: lbtv_bot_taunt 0/1")]
    public void OnTauntCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (command.ArgCount < 2)
        {
            command.ReplyToCommand($"lbtv_bot_taunt is {(_enabled ? 1 : 0)}");
            return;
        }

        var value = command.ArgByIndex(1);
        if (value == "1")
        {
            _enabled = true;
            command.ReplyToCommand("lbtv_bot_taunt set to 1");
            return;
        }

        if (value == "0")
        {
            _enabled = false;
            command.ReplyToCommand("lbtv_bot_taunt set to 0");
            return;
        }

        command.ReplyToCommand("Usage: lbtv_bot_taunt 0/1");
    }

    [ConsoleCommand("lbtv_bot_chat", "Enable or disable LBTV bot chat replies. Usage: lbtv_bot_chat 0/1")]
    public void OnAiChatCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (command.ArgCount < 2)
        {
            command.ReplyToCommand($"lbtv_bot_chat is {(_aiChatEnabled ? 1 : 0)}");
            return;
        }

        var value = command.ArgByIndex(1);
        if (value == "1")
        {
            _aiChatEnabled = true;
            command.ReplyToCommand("lbtv_bot_chat set to 1");
            return;
        }

        if (value == "0")
        {
            _aiChatEnabled = false;
            command.ReplyToCommand("lbtv_bot_chat set to 0");
            return;
        }

        command.ReplyToCommand("Usage: lbtv_bot_chat 0/1");
    }

    private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        _roundSerial++;
        _roundEnded = false;
        _roundTauntCount = 0;
        _roundKillsByBot.Clear();
        _recentKillTimesByBot.Clear();
        _multiKillTauntedBots.Clear();
        _aceTauntedBots.Clear();
        _clutchTauntedThisRound = false;
        _saveTauntedThisRound = false;
        _roundKillTauntedThisRound = false;
        StartOpeningTrashTalkIfNeeded();
        return HookResult.Continue;
    }

    private HookResult OnRoundFreezeEnd(EventRoundFreezeEnd @event, GameEventInfo info)
    {
        ScheduleLateRoundChecks(_roundSerial);
        return HookResult.Continue;
    }

    private HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        _roundEnded = true;
        return HookResult.Continue;
    }

    private void ScheduleLateRoundChecks(int roundSerial)
    {
        var roundSeconds = GetConfiguredRoundSeconds();
        var clutchDelay = Math.Max(1.0f, roundSeconds - 40.0f);
        var saveDelay = Math.Max(1.0f, roundSeconds - 20.0f);

        AddTimer(clutchDelay, () => PollLateRoundClutchTaunt(roundSerial));
        AddTimer(saveDelay, () => TryPrintLateRoundSaveTaunt(roundSerial));
    }

    private void TryPrintLateRoundSaveTaunt(int roundSerial)
    {
        if (!IsActiveRoundTimer(roundSerial) || !_enabled || _saveTauntedThisRound)
        {
            return;
        }

        var alivePlayers = GetAliveRoundPlayers();
        var ctAlive = alivePlayers.Where(p => p.Team == CsTeam.CounterTerrorist).ToList();
        var tAlive = alivePlayers.Where(p => p.Team == CsTeam.Terrorist).ToList();
        if (ctAlive.Count == 0 || tAlive.Count == 0 || ctAlive.Count == tAlive.Count)
        {
            return;
        }

        var advantagedTeam = ctAlive.Count > tAlive.Count ? CsTeam.CounterTerrorist : CsTeam.Terrorist;
        var bot = PickRandomAliveBot(advantagedTeam);
        if (bot == null)
        {
            return;
        }

        PrintTaunt(bot, "给阿姨来一杯卡布奇诺。");
        _saveTauntedThisRound = true;
    }

    private void PollLateRoundClutchTaunt(int roundSerial)
    {
        if (!IsActiveRoundTimer(roundSerial) || !_enabled || _clutchTauntedThisRound)
        {
            return;
        }

        if (TryPrintLateRoundClutchTaunt(roundSerial))
        {
            return;
        }

        AddTimer(LateRoundPollSeconds, () => PollLateRoundClutchTaunt(roundSerial));
    }

    private bool TryPrintLateRoundClutchTaunt(int roundSerial)
    {
        if (!IsActiveRoundTimer(roundSerial) || !_enabled || _clutchTauntedThisRound)
        {
            return false;
        }

        var alivePlayers = GetAliveRoundPlayers();
        var ctAlive = alivePlayers.Where(p => p.Team == CsTeam.CounterTerrorist).ToList();
        var tAlive = alivePlayers.Where(p => p.Team == CsTeam.Terrorist).ToList();

        var advantagedTeam = CsTeam.None;
        if (ctAlive.Count == 1 && tAlive.Count >= 3)
        {
            advantagedTeam = CsTeam.Terrorist;
        }
        else if (tAlive.Count == 1 && ctAlive.Count >= 3)
        {
            advantagedTeam = CsTeam.CounterTerrorist;
        }

        if (advantagedTeam is not (CsTeam.CounterTerrorist or CsTeam.Terrorist))
        {
            return false;
        }

        var bot = PickRandomAliveBot(advantagedTeam);
        if (bot == null)
        {
            return false;
        }

        PrintTaunt(bot, "请开始你的表演。");
        _clutchTauntedThisRound = true;
        return true;
    }

    private bool IsActiveRoundTimer(int roundSerial)
    {
        return !_roundEnded && roundSerial == _roundSerial;
    }

    private HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player != null)
        {
            _nextTauntTimeByBot.Remove(GetPlayerKey(player));
        }

        return HookResult.Continue;
    }

    private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        if (!_enabled)
        {
            return HookResult.Continue;
        }

        var attacker = @event.Attacker;
        var victim = @event.Userid;
        if (!IsBotAttacker(attacker) || attacker == null || !IsValidKillVictim(attacker, victim))
        {
            return HookResult.Continue;
        }

        TrackBotKill(attacker);
        if (TryPrintMultiKillTaunt(attacker) || TryPrintRoundKillTaunt(attacker))
        {
            return HookResult.Continue;
        }

        if (!IsHumanVictim(victim) || _roundTauntCount >= MaxRoundTaunts)
        {
            return HookResult.Continue;
        }

        if (!CanBotTaunt(attacker))
        {
            return HookResult.Continue;
        }

        var taunts = GetTauntPool(@event);
        var chance = taunts == NormalTaunts ? NormalTauntChance : SpecialTauntChance;
        if (_random.NextDouble() >= chance)
        {
            return HookResult.Continue;
        }

        PrintTaunt(attacker, taunts[_random.Next(taunts.Length)]);
        _roundTauntCount++;
        _nextTauntTimeByBot[GetPlayerKey(attacker)] = Server.CurrentTime + BotCooldownSeconds;
        return HookResult.Continue;
    }

    private void TrackBotKill(CCSPlayerController bot)
    {
        var key = GetPlayerKey(bot);
        _roundKillsByBot[key] = _roundKillsByBot.TryGetValue(key, out var kills) ? kills + 1 : 1;

        if (!_recentKillTimesByBot.TryGetValue(key, out var killTimes))
        {
            killTimes = new Queue<float>();
            _recentKillTimesByBot[key] = killTimes;
        }

        killTimes.Enqueue(Server.CurrentTime);
        while (killTimes.Count > 0 && Server.CurrentTime - killTimes.Peek() > MultiKillWindowSeconds)
        {
            killTimes.Dequeue();
        }
    }

    private bool TryPrintRoundKillTaunt(CCSPlayerController bot)
    {
        var key = GetPlayerKey(bot);
        if (_roundKillTauntedThisRound
            || _aceTauntedBots.Contains(key)
            || !_roundKillsByBot.TryGetValue(key, out var kills)
            || kills < RoundKillTauntThreshold)
        {
            return false;
        }

        PrintTaunt(bot, "我卢本伟没有开挂。");
        _aceTauntedBots.Add(key);
        _roundKillTauntedThisRound = true;
        _nextTauntTimeByBot[key] = Server.CurrentTime + BotCooldownSeconds;
        return true;
    }

    private bool TryPrintMultiKillTaunt(CCSPlayerController bot)
    {
        var key = GetPlayerKey(bot);
        if (_multiKillTauntedBots.Contains(key)
            || !_recentKillTimesByBot.TryGetValue(key, out var killTimes)
            || killTimes.Count < MultiKillThreshold)
        {
            return false;
        }

        PrintTaunt(bot, "番茄连招。");
        _multiKillTauntedBots.Add(key);
        _nextTauntTimeByBot[key] = Server.CurrentTime + BotCooldownSeconds;
        return true;
    }

    private HookResult OnRoundMvp(EventRoundMvp @event, GameEventInfo info)
    {
        if (!_enabled || _roundTauntCount >= MaxRoundTaunts)
        {
            return HookResult.Continue;
        }

        var bot = @event.Userid;
        if (!IsBotAttacker(bot) || bot == null)
        {
            return HookResult.Continue;
        }

        if (_random.NextDouble() >= MvpTauntChance)
        {
            return HookResult.Continue;
        }

        StartMvpAiTauntRequest(GetPlayerKey(bot), bot.PlayerName, TeamName(bot.Team));
        _roundTauntCount++;
        return HookResult.Continue;
    }

    private void OnPlayerChat(CCSPlayerController? player, string message, bool teamChat)
    {
        HandlePlayerChat(player, message, teamChat);
    }

    private HookResult OnSayCommand(CCSPlayerController? player, CommandInfo command)
    {
        HandlePlayerChat(player, ExtractSayMessage(command), false);
        return HookResult.Continue;
    }

    private HookResult OnSayTeamCommand(CCSPlayerController? player, CommandInfo command)
    {
        HandlePlayerChat(player, ExtractSayMessage(command), true);
        return HookResult.Continue;
    }

    private void HandlePlayerChat(CCSPlayerController? player, string message, bool teamChat)
    {
        if (!_aiChatEnabled || teamChat || !IsHumanVictim(player) || player == null)
        {
            return;
        }

        var cleanMessage = NormalizePlayerMessage(message);
        if (string.IsNullOrWhiteSpace(cleanMessage) || cleanMessage.StartsWith("!", StringComparison.Ordinal)
            || cleanMessage.StartsWith("/", StringComparison.Ordinal))
        {
            return;
        }

        if (IsDuplicateChat(player, cleanMessage))
        {
            return;
        }

        var bot = PickReplyBot(player);
        if (bot == null)
        {
            return;
        }

        var request = new AiChatRequest(
            PlayerName: player!.PlayerName,
            PlayerTeam: TeamName(player.Team),
            PlayerAlive: player.PawnIsAlive,
            BotKey: GetPlayerKey(bot),
            BotName: bot.PlayerName,
            BotTeam: TeamName(bot.Team),
            BattleSummary: BuildBattleSummary(player, bot),
            PlayerMessage: cleanMessage
        );

        if (_aiReplyInFlight || Server.CurrentTime < _nextAiChatTime)
        {
            _pendingAiChatRequest = request;
            SchedulePendingAiChat();
            return;
        }

        StartAiChatRequest(request);
    }

    private static string[] GetTauntPool(EventPlayerDeath @event)
    {
        if (IsAwpWeapon(@event.Weapon))
        {
            return NormalTaunts;
        }

        if (@event.Headshot)
        {
            return HeadshotTaunts;
        }

        if (IsKnifeWeapon(@event.Weapon))
        {
            return KnifeTaunts;
        }

        return NormalTaunts;
    }

    private static void PrintTaunt(CCSPlayerController bot, string taunt)
    {
        Server.PrintToChatAll($" {ChatColorRed}{LbtvPrefix} {bot.PlayerName}: {taunt}{ChatColorDefault}");
    }

    private static void PrintTaunt(string botName, string taunt)
    {
        Server.PrintToChatAll($" {ChatColorRed}{LbtvPrefix} {botName}: {taunt}{ChatColorDefault}");
    }

    private static void PrintAiChat(CCSPlayerController bot, string reply)
    {
        Server.PrintToChatAll($" {ChatColors.Green}{LbtvPrefix} {bot.PlayerName}: {reply}{ChatColorDefault}");
    }

    private static void PrintAiChat(string botName, string reply)
    {
        Server.PrintToChatAll($" {ChatColors.Green}{LbtvPrefix} {botName}: {reply}{ChatColorDefault}");
    }

    private static void PrintOpeningTrashTalk(string botName, string trashTalk)
    {
        Server.PrintToChatAll($" {ChatColors.Green}{LbtvPrefix} {botName}: {trashTalk}{ChatColorDefault}");
    }

    private void StartOpeningTrashTalkIfNeeded()
    {
        if (!_enabled || _openingTrashTalkStarted)
        {
            return;
        }

        var bots = Utilities.GetPlayers()
            .Where(p => p is { IsValid: true, IsBot: true, IsHLTV: false }
                        && p.Team is CsTeam.CounterTerrorist or CsTeam.Terrorist)
            .Select(p => new OpeningTrashTalkSpeaker(GetPlayerKey(p), p.PlayerName))
            .ToList();
        if (bots.Count == 0)
        {
            return;
        }

        Shuffle(bots);
        _openingTrashTalkStarted = true;

        for (var i = 0; i < bots.Count; i++)
        {
            var speaker = bots[i];
            var delay = (i + 1) * OpeningTrashTalkIntervalSeconds;
            AddTimer(delay, () => PrintOpeningTrashTalkIfStillBot(speaker));
        }
    }

    private void PrintOpeningTrashTalkIfStillBot(OpeningTrashTalkSpeaker speaker)
    {
        var bot = FindPlayerByKey(speaker.BotKey);
        if (bot is not { IsValid: true, IsBot: true, IsHLTV: false }
            || bot.Team is not (CsTeam.CounterTerrorist or CsTeam.Terrorist))
        {
            return;
        }

        PrintOpeningTrashTalk(bot.PlayerName, OpeningTrashTalks[_random.Next(OpeningTrashTalks.Length)]);
    }

    private void Shuffle<T>(IList<T> items)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = _random.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }

    private void StartAiChatRequest(AiChatRequest request)
    {
        _nextAiChatTime = Server.CurrentTime + AiChatCooldownSeconds;
        _aiReplyInFlight = true;
        _ = ReplyToPlayerChatAsync(request);
    }

    private void StartMvpAiTauntRequest(int botKey, string botName, string botTeam)
    {
        _ = PrintMvpAiTauntAsync(new MvpAiTauntRequest(botKey, botName, botTeam, BuildMvpBattleSummary(botKey)));
    }

    private async Task ReplyToPlayerChatAsync(AiChatRequest request)
    {
        string? reply = null;
        try
        {
            reply = await FetchAiReplyAsync(request);
        }
        catch
        {
            // AI chat is optional; fixed taunts should keep working if the network fails.
        }

        reply = NormalizeAiReply(reply) ?? BuildFallbackAiReply(request);
        if (string.IsNullOrWhiteSpace(reply))
        {
            Server.NextFrame(() =>
            {
                _aiReplyInFlight = false;
                SchedulePendingAiChat();
            });
            return;
        }

        Server.NextFrame(() =>
        {
            _aiReplyInFlight = false;
            var bot = FindPlayerByKey(request.BotKey);
            if (bot is { IsValid: true, IsBot: true })
            {
                PrintAiChat(bot, reply);
            }
            else
            {
                PrintAiChat(request.BotName, reply);
            }

            SchedulePendingAiChat();
        });
    }

    private async Task PrintMvpAiTauntAsync(MvpAiTauntRequest request)
    {
        string? taunt = null;
        try
        {
            taunt = await FetchMvpAiTauntAsync(request);
        }
        catch
        {
            // MVP taunts fall back to the fixed local pool if the AI service is unavailable.
        }

        taunt = NormalizeAiReply(taunt);
        Server.NextFrame(() =>
        {
            var fallback = NormalTaunts[_random.Next(NormalTaunts.Length)];
            var bot = FindPlayerByKey(request.BotKey);
            if (bot is { IsValid: true, IsBot: true })
            {
                PrintTaunt(bot, taunt ?? fallback);
            }
            else
            {
                PrintTaunt(request.BotName, taunt ?? fallback);
            }
        });
    }

    private void SchedulePendingAiChat()
    {
        if (_pendingAiChatRequest == null || _aiReplyInFlight)
        {
            return;
        }

        var delay = Math.Max(0.1f, _nextAiChatTime - Server.CurrentTime);
        AddTimer(delay, () =>
        {
            if (_pendingAiChatRequest == null || _aiReplyInFlight || Server.CurrentTime < _nextAiChatTime)
            {
                SchedulePendingAiChat();
                return;
            }

            var request = _pendingAiChatRequest;
            _pendingAiChatRequest = null;
            StartAiChatRequest(request);
        });
    }

    private static async Task<string?> FetchAiReplyAsync(AiChatRequest request)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var reply = await FetchAiReplyFromSiteApiAsync(request);
            if (!string.IsNullOrWhiteSpace(reply))
            {
                return reply;
            }
        }

        return null;
    }

    private static async Task<string?> FetchMvpAiTauntAsync(MvpAiTauntRequest request)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var reply = await FetchMvpAiTauntFromSiteApiAsync(request);
            if (!string.IsNullOrWhiteSpace(reply))
            {
                return reply;
            }
        }

        return null;
    }

    private static async Task<string?> FetchAiReplyFromSiteApiAsync(AiChatRequest request)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, AiApiUrl);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AiApiKey);
        httpRequest.Headers.UserAgent.ParseAdd("LBTV-CS2-BotTaunt/1.0");

        var body = new
        {
            temperature = 0.95,
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = "你是 Counter-Strike 2 对局里的 BOT 玩家，正在用游戏聊天回复真人玩家。请优先用中文，尽量只说一句短话，通常 10 到 35 个汉字左右。语气可以嘲讽、斗嘴、玩梗或接话，要像游戏内聊天，不要解释规则，不要输出多段说明。"
                },
                new
                {
                    role = "user",
                    content = $"玩家：{request.PlayerName}（{request.PlayerTeam}，{(request.PlayerAlive ? "存活" : "死亡")}）\nBOT：{request.BotName}（{request.BotTeam}）\n战场情况：{request.BattleSummary}\n玩家刚说：{request.PlayerMessage}\n请用 {request.BotName} 的口吻直接回复一句简短中文聊天。"
                }
            }
        };

        httpRequest.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        using var response = await AiHttpClient.SendAsync(httpRequest);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        return ExtractReplyContent(document.RootElement);
    }

    private static async Task<string?> FetchMvpAiTauntFromSiteApiAsync(MvpAiTauntRequest request)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, AiApiUrl);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AiApiKey);
        httpRequest.Headers.UserAgent.ParseAdd("LBTV-CS2-BotTaunt/1.0");

        var body = new
        {
            temperature = 0.95,
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = "你是 Counter-Strike 2 对局里刚拿下 MVP 的 BOT 玩家。请用中文输出一句很短的嘲讽聊天，10 到 30 个汉字左右。只输出台词本身，不要解释，不要加引号，不要加前缀。"
                },
                new
                {
                    role = "user",
                    content = $"BOT：{request.BotName}（{request.BotTeam}）刚拿下本回合 MVP。\n战场情况：{request.BattleSummary}\n请用 {request.BotName} 的口吻发一句 MVP 嘲讽。"
                }
            }
        };

        httpRequest.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        using var response = await AiHttpClient.SendAsync(httpRequest);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        return ExtractReplyContent(document.RootElement);
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

    private static string BuildFallbackAiReply(AiChatRequest request)
    {
        var variants = new[]
        {
            $"{request.PlayerName}，嘴比枪准是吧？",
            "你这输出全在聊天框里。",
            "先赢一把，再来跟 BOT 叫。",
            "打不过就开始打字，懂了。",
            "别急，下一回合继续给你上课。"
        };

        var index = Math.Abs(HashCode.Combine(request.PlayerName, request.PlayerMessage, request.BotName)) % variants.Length;
        return variants[index];
    }

    private bool CanBotTaunt(CCSPlayerController bot)
    {
        var key = GetPlayerKey(bot);
        return !_nextTauntTimeByBot.TryGetValue(key, out var nextTime) || Server.CurrentTime >= nextTime;
    }

    private void ResetState()
    {
        _roundTauntCount = 0;
        _nextTauntTimeByBot.Clear();
        _roundKillsByBot.Clear();
        _recentKillTimesByBot.Clear();
        _multiKillTauntedBots.Clear();
        _aceTauntedBots.Clear();
        _nextAiChatTime = 0f;
        _lastChatHandledTime = 0f;
        _lastChatSignature = string.Empty;
        _aiReplyInFlight = false;
        _pendingAiChatRequest = null;
        _clutchTauntedThisRound = false;
        _saveTauntedThisRound = false;
        _roundKillTauntedThisRound = false;
        _openingTrashTalkStarted = false;
        _roundSerial = 0;
        _roundEnded = true;
    }

    private CCSPlayerController? PickReplyBot(CCSPlayerController player)
    {
        var bots = Utilities.GetPlayers()
            .Where(p => p is { IsValid: true, IsBot: true, IsHLTV: false }
                        && p.Team is CsTeam.CounterTerrorist or CsTeam.Terrorist)
            .ToList();
        if (bots.Count == 0)
        {
            return null;
        }

        var enemyBots = bots
            .Where(p => IsEnemyTeam(player.Team, p.Team))
            .OrderByDescending(p => p.PawnIsAlive)
            .ToList();
        var pool = enemyBots.Count > 0 ? enemyBots : bots.OrderByDescending(p => p.PawnIsAlive).ToList();
        return pool[_random.Next(pool.Count)];
    }

    private CCSPlayerController? PickRandomAliveBot(CsTeam team)
    {
        var bots = Utilities.GetPlayers()
            .Where(p => p is { IsValid: true, IsBot: true, IsHLTV: false, PawnIsAlive: true } && p.Team == team)
            .ToList();

        return bots.Count == 0 ? null : bots[_random.Next(bots.Count)];
    }

    private static CCSPlayerController? FindPlayerByKey(int key)
    {
        return Utilities.GetPlayers().FirstOrDefault(player => player is { IsValid: true } && GetPlayerKey(player) == key);
    }

    private static string ExtractSayMessage(CommandInfo command)
    {
        if (command.ArgCount < 2)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        for (var i = 1; i < command.ArgCount; i++)
        {
            var arg = command.ArgByIndex(i);
            if (string.IsNullOrWhiteSpace(arg))
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(arg);
        }

        return builder.ToString().Trim().Trim('"');
    }

    private bool IsDuplicateChat(CCSPlayerController player, string message)
    {
        var signature = $"{GetPlayerKey(player)}:{message}";
        if (_lastChatSignature == signature && Server.CurrentTime - _lastChatHandledTime < 0.25f)
        {
            return true;
        }

        _lastChatSignature = signature;
        _lastChatHandledTime = Server.CurrentTime;
        return false;
    }

    private static string BuildBattleSummary(CCSPlayerController player, CCSPlayerController bot)
    {
        var players = Utilities.GetPlayers()
            .Where(p => p is { IsValid: true, IsHLTV: false }
                        && p.Team is CsTeam.CounterTerrorist or CsTeam.Terrorist)
            .ToList();

        var humanAlive = players.Count(p => !p.IsBot && p.PawnIsAlive);
        var botAlive = players.Count(p => p.IsBot && p.PawnIsAlive);
        var enemyBotAlive = players.Count(p => p.IsBot && IsEnemyTeam(player.Team, p.Team) && p.PawnIsAlive);
        var playerState = player.PawnIsAlive ? "说话的真人还活着" : "说话的真人已经死亡";
        var botState = bot.PawnIsAlive ? "回复 BOT 还活着" : "回复 BOT 已死亡";

        return $"{playerState}；{botState}；场上存活真人 {humanAlive} 个，存活 BOT {botAlive} 个，其中敌方 BOT {enemyBotAlive} 个。";
    }

    private static string BuildMvpBattleSummary(int botKey)
    {
        var players = Utilities.GetPlayers()
            .Where(p => p is { IsValid: true, IsHLTV: false }
                        && p.Team is CsTeam.CounterTerrorist or CsTeam.Terrorist)
            .ToList();

        var humans = players.Count(p => !p.IsBot);
        var bots = players.Count(p => p.IsBot);
        var humanAlive = players.Count(p => !p.IsBot && p.PawnIsAlive);
        var botAlive = players.Count(p => p.IsBot && p.PawnIsAlive);
        var mvp = players.FirstOrDefault(p => GetPlayerKey(p) == botKey);
        var mvpState = mvp == null
            ? "MVP BOT 状态未知"
            : (mvp.PawnIsAlive ? "MVP BOT 回合结束仍存活" : "MVP BOT 回合结束已阵亡");

        return $"{mvpState}；本局玩家 {humans} 个，BOT {bots} 个；回合结束时存活真人 {humanAlive} 个，存活 BOT {botAlive} 个。";
    }

    private static List<CCSPlayerController> GetAliveRoundPlayers()
    {
        return Utilities.GetPlayers()
            .Where(p => p is { IsValid: true, IsHLTV: false, PawnIsAlive: true }
                        && p.Team is CsTeam.CounterTerrorist or CsTeam.Terrorist)
            .ToList();
    }

    private static float GetConfiguredRoundSeconds()
    {
        var minutes = ConVar.Find("mp_roundtime_defuse")?.GetPrimitiveValue<float>() ?? 0f;
        if (minutes <= 0f)
        {
            minutes = ConVar.Find("mp_roundtime")?.GetPrimitiveValue<float>() ?? 1.92f;
        }

        if (minutes <= 0f)
        {
            minutes = 1.92f;
        }

        return minutes * 60.0f;
    }

    private static string NormalizePlayerMessage(string? message)
    {
        var normalized = (message ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
        return normalized.Length <= MaxPlayerMessageLength
            ? normalized
            : normalized[..MaxPlayerMessageLength];
    }

    private static string? NormalizeAiReply(string? reply)
    {
        var normalized = (reply ?? string.Empty)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim()
            .Trim('"', '\'', '“', '”', '‘', '’');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized.Length <= MaxAiReplyLength
            ? normalized
            : normalized[..MaxAiReplyLength];
    }

    private static bool IsBotAttacker(CCSPlayerController? player)
    {
        return player is { IsValid: true, IsBot: true, IsHLTV: false };
    }

    private static bool IsHumanVictim(CCSPlayerController? player)
    {
        return player is { IsValid: true, IsBot: false, IsHLTV: false };
    }

    private static bool IsValidKillVictim(CCSPlayerController attacker, CCSPlayerController? victim)
    {
        return victim is { IsValid: true, IsHLTV: false }
               && victim.Team is CsTeam.CounterTerrorist or CsTeam.Terrorist
               && IsEnemyTeam(attacker.Team, victim.Team)
               && !IsSamePlayer(attacker, victim);
    }

    private static bool IsSamePlayer(CCSPlayerController attacker, CCSPlayerController victim)
    {
        return GetPlayerKey(attacker) == GetPlayerKey(victim);
    }

    private static int GetPlayerKey(CCSPlayerController player)
    {
        return player.UserId ?? player.Slot;
    }

    private static bool IsEnemyTeam(CsTeam left, CsTeam right)
    {
        return (left == CsTeam.CounterTerrorist && right == CsTeam.Terrorist)
               || (left == CsTeam.Terrorist && right == CsTeam.CounterTerrorist);
    }

    private static string TeamName(CsTeam team)
    {
        return team switch
        {
            CsTeam.CounterTerrorist => "CT",
            CsTeam.Terrorist => "T",
            _ => "Unknown"
        };
    }

    private static bool IsKnifeWeapon(string? weapon)
    {
        return !string.IsNullOrWhiteSpace(weapon)
            && weapon.Contains("knife", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAwpWeapon(string? weapon)
    {
        return string.Equals(weapon, "awp", StringComparison.OrdinalIgnoreCase)
            || string.Equals(weapon, "weapon_awp", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record AiChatRequest(
        string PlayerName,
        string PlayerTeam,
        bool PlayerAlive,
        int BotKey,
        string BotName,
        string BotTeam,
        string BattleSummary,
        string PlayerMessage);

    private sealed record MvpAiTauntRequest(
        int BotKey,
        string BotName,
        string BotTeam,
        string BattleSummary);

    private sealed record OpeningTrashTalkSpeaker(int BotKey, string BotName);
}
