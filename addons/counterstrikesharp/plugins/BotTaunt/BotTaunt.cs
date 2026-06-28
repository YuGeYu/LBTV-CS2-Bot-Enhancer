using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Utils;
using BotTaunt.AiChat;
using System.Net.Http.Headers;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BotTaunt;

[MinimumApiVersion(304)]
public sealed class BotTauntPlugin : BasePlugin, IPluginConfig<BotTauntConfig>
{
    public override string ModuleName => "BotTaunt";
    public override string ModuleVersion => "1.2.0";
    public override string ModuleAuthor => "OpenAI";
    public override string ModuleDescription => "Lets bots taunt human players and optionally rival bots after kills.";

    internal const string DefaultAiApiUrl = "https://maomaochongmiao.600318.xyz/api/open/ai-chat";
    internal const string DefaultAiApiKey = "";
    internal const double DefaultAiTemperature = 0.95;
    internal const int DefaultAiChatTimeoutSeconds = 15;
    internal const string DefaultThirdPartyAiModel = "deepseek-chat";
    internal const int DefaultMaxPlayerMessageLength = 300;
    internal const int DefaultMaxAiReplyLength = 220;
    internal const bool DefaultAiPreferNativeBotSay = true;
    internal const bool DefaultAiFallbackToPluginPrint = true;
    internal const bool DefaultAiReplyToTeamChat = true;
    internal const bool DefaultAiPreferSameTeamBot = true;
    internal const double DefaultAiNoMentionReplyChance = 1.0;
    internal const double DefaultAiMentionReplyChance = 1.0;
    internal const double DefaultAiQuestionBoost = 0.0;
    internal const double DefaultAiGlobalCooldownSeconds = 10.0;
    internal const double DefaultAiPerPlayerCooldownSeconds = 0.0;
    internal const double DefaultAiPerBotCooldownSeconds = 10.0;
    internal const int DefaultAiMaxRepliesPerMinute = 10;
    internal const int DefaultAiMaxRepliesPerRound = 12;
    internal const int DefaultAiRecentContextMessages = 5;
    internal const int DefaultAiRecentContextMaxChars = 520;
    internal const int DefaultAiMaxOutputTokens = 80;
    internal const double DefaultOpeningTrashTalkBotChance = 0.65;
    internal const double DefaultMvpTauntChance = 0.40;
    internal const bool DefaultBotRivalryEnabled = false;
    internal const double DefaultBotRivalryTauntChance = 0.15;
    internal const double DefaultBotRivalrySpecialTauntChance = 0.25;
    internal const int DefaultMaxBotRivalryTauntsPerRound = 2;
    internal const double DefaultBotRivalryCooldownSeconds = 45.0;
    private const string DefaultRoundKillTaunt = "我卢本伟没有开挂。";
    private const string DefaultMultiKillTaunt = "番茄连招。";
    private const string DefaultClutchTaunt = "请开始你的表演。";
    private const string DefaultSaveTaunt = "给阿姨来一杯卡布奇诺。";

    private const int MaxRoundTaunts = 9;
    private const float BotCooldownSeconds = 30.0f;
    private const float NormalTauntChance = 0.50f;
    private const float SpecialTauntChance = 0.70f;
    private const float MultiKillWindowSeconds = 5.0f;
    private const int MultiKillThreshold = 3;
    private const int RoundKillTauntThreshold = 5;
    private const float OpeningTrashTalkIntervalSeconds = 1.15f;
    private const float LateRoundPollSeconds = 1.0f;

    private const string LbtvPrefix = "[LBTV]";
    private const string ChatColorRed = "\u0002";
    private const string ChatColorDefault = "\u0001";
    private static readonly HttpClient AiHttpClient = new();
    private static readonly UTF8Encoding Utf8NoBom = new(false);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly Random _random = new();
    private readonly Dictionary<int, float> _nextTauntTimeByBot = new();
    private readonly Dictionary<int, float> _nextRivalryTauntTimeByBot = new();
    private readonly Dictionary<int, int> _roundKillsByBot = new();
    private readonly Dictionary<int, Queue<float>> _recentKillTimesByBot = new();
    private readonly HashSet<int> _multiKillTauntedBots = new();
    private readonly HashSet<int> _aceTauntedBots = new();
    private float _lastChatHandledTime;
    private string _lastChatSignature = string.Empty;
    private bool _aiReplyInFlight;
    private AiChatRequest? _pendingAiChatRequest;
    private readonly AiChatClient _aiChatClient = new(AiHttpClient, JsonOptions);
    private readonly AiConversationMemory _aiMemory = new();
    private int _roundTauntCount;
    private int _roundBotRivalryTauntCount;
    private bool _clutchTauntedThisRound;
    private bool _saveTauntedThisRound;
    private bool _roundKillTauntedThisRound;
    private bool _openingTrashTalkStarted;
    private int _roundSerial;
    private bool _roundEnded = true;
    private bool _enabled = true;
    private bool _aiChatEnabled = true;
    private bool _botRivalryEnabled = DefaultBotRivalryEnabled;
    private TauntPools _tauntPools = TauntPools.CreateDefault();

    public BotTauntConfig Config { get; set; } = BotTauntConfig.CreateDefault();

    private string TauntsConfigPath => Path.GetFullPath(Path.Combine(
        ModuleDirectory,
        "..",
        "..",
        "configs",
        "plugins",
        "BotTaunt",
        "Taunts.json"));

    private string BotTauntConfigPath => Path.GetFullPath(Path.Combine(
        ModuleDirectory,
        "..",
        "..",
        "configs",
        "plugins",
        "BotTaunt",
        "BotTaunt.json"));

    private void LoadTaunts()
    {
        try
        {
            var directory = Path.GetDirectoryName(TauntsConfigPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (!File.Exists(TauntsConfigPath))
            {
                _tauntPools = TauntPools.CreateDefault();
                File.WriteAllText(TauntsConfigPath, JsonSerializer.Serialize(_tauntPools, JsonOptions), Utf8NoBom);
                return;
            }

            var text = File.ReadAllText(TauntsConfigPath, Encoding.UTF8).TrimStart('\uFEFF');
            _tauntPools = (JsonSerializer.Deserialize<TauntPools>(text, JsonOptions) ?? TauntPools.CreateDefault()).Normalized();
        }
        catch (Exception ex)
        {
            _tauntPools = TauntPools.CreateDefault();
            Server.PrintToConsole($"[BotTaunt] Failed to load Taunts.json, using built-in taunts: {ex.Message}");
        }
    }

    public void OnConfigParsed(BotTauntConfig config)
    {
        Config = config.Normalized();
        _aiChatEnabled = Config.AiChatEnabled;
        _botRivalryEnabled = Config.BotRivalryEnabled;
    }

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

    private static readonly string[] BotRivalryTaunts =
    {
        "同样是 BOT，你怎么像训练靶？",
        "你这走位，是导航坏了吗？",
        "别装职业哥了，回去跑路径点。",
        "我都替你的脚本着急。",
        "这枪法，建议重启一下自己。",
        "你这反应延迟，服务器都看不下去了。",
        "BOT 之间也有差距，今天你证明了。",
        "别怪我，怪你的难度模板。",
        "你刚才是在执行投降脚本吗？",
        "我这是战术，你那是随机游走。"
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
        LoadTaunts();
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

    [ConsoleCommand("lbtv_bot_chat_reload", "Reload BotTaunt config and taunt pools.")]
    public void OnAiChatReloadCommand(CCSPlayerController? player, CommandInfo command)
    {
        try
        {
            if (!File.Exists(BotTauntConfigPath))
            {
                throw new FileNotFoundException("BotTaunt.json not found.", BotTauntConfigPath);
            }

            var text = File.ReadAllText(BotTauntConfigPath, Encoding.UTF8).TrimStart('\uFEFF');
            var config = JsonSerializer.Deserialize<BotTauntConfig>(text, JsonOptions) ?? BotTauntConfig.CreateDefault();
            OnConfigParsed(config);
            LoadTaunts();
            ReplyToCommand(player, command, "[BotTaunt] Config reloaded.");
        }
        catch (Exception ex)
        {
            ReplyToCommand(player, command, $"[BotTaunt] Config reload failed: {ex.Message}");
        }
    }

    [ConsoleCommand("lbtv_bot_chat_diag", "Show BotTaunt AI chat diagnostics.")]
    public void OnAiChatDiagCommand(CCSPlayerController? player, CommandInfo command)
    {
        var bots = Utilities.GetPlayers()
            .Where(p => p is { IsValid: true, IsBot: true, IsHLTV: false }
                        && p.Team is CsTeam.CounterTerrorist or CsTeam.Terrorist)
            .ToList();
        ReplyToCommand(
            player,
            command,
            $"[BotTaunt] ai={(_aiChatEnabled ? 1 : 0)} mode={Config.AiApiMode} nativeSay={(Config.AiPreferNativeBotSay ? 1 : 0)} teamChat={(Config.AiReplyToTeamChat ? 1 : 0)} bots={bots.Count} pending={(_aiReplyInFlight ? 1 : 0)}");
        ReplyToCommand(
            player,
            command,
            $"[BotTaunt] cooldown global={Config.AiGlobalCooldownSeconds:0.##}s player={Config.AiPerPlayerCooldownSeconds:0.##}s bot={Config.AiPerBotCooldownSeconds:0.##}s roundReplies={_aiMemory.RepliesThisRound}/{Config.AiMaxRepliesPerRound} recent={_aiMemory.RecentCount}");
        foreach (var bot in bots.Take(8))
        {
            ReplyToCommand(player, command, $"[BotTaunt] bot key={GetPlayerKey(bot)} name='{bot.PlayerName}' team={TeamName(bot.Team)} alive={(bot.PawnIsAlive ? 1 : 0)}");
        }
    }

    [ConsoleCommand("lbtv_bot_chat_saytest", "Make a bot send a native say message. Usage: lbtv_bot_chat_saytest <message>")]
    public void OnAiChatSayTestCommand(CCSPlayerController? player, CommandInfo command)
    {
        var message = AiChatText.Sanitize(command.ArgString);
        if (string.IsNullOrWhiteSpace(message))
        {
            ReplyToCommand(player, command, "[BotTaunt] Usage: lbtv_bot_chat_saytest <message>");
            return;
        }

        var bot = player == null
            ? Utilities.GetPlayers().FirstOrDefault(p => p is { IsValid: true, IsBot: true, IsHLTV: false })
            : PickReplyBot(player, teamChat: false, Config);
        if (bot is not { IsValid: true, IsBot: true })
        {
            ReplyToCommand(player, command, "[BotTaunt] Native say test failed: no valid bot.");
            return;
        }

        var sent = TryNativeBotSay(bot, teamChat: false, message, Config.MaxAiReplyLength);
        ReplyToCommand(player, command, sent
            ? $"[BotTaunt] Native say called on bot {bot.PlayerName}."
            : $"[BotTaunt] Native say failed for bot {bot.PlayerName}.");
    }

    [ConsoleCommand("lbtv_bot_chat_test", "Send a test message to the configured AI. Usage: lbtv_bot_chat_test <message>")]
    public void OnAiChatTestCommand(CCSPlayerController? player, CommandInfo command)
    {
        var message = AiChatText.Sanitize(command.ArgString);
        if (string.IsNullOrWhiteSpace(message))
        {
            ReplyToCommand(player, command, "[BotTaunt] Usage: lbtv_bot_chat_test <message>");
            return;
        }

        var bot = player == null
            ? Utilities.GetPlayers().FirstOrDefault(p => p is { IsValid: true, IsBot: true, IsHLTV: false })
            : PickReplyBot(player, teamChat: false, Config);
        if (bot is not { IsValid: true, IsBot: true })
        {
            ReplyToCommand(player, command, "[BotTaunt] AI test failed: no valid bot.");
            return;
        }

        ReplyToCommand(player, command, "[BotTaunt] Sending AI test request...");
        var playerName = player?.PlayerName ?? "Server";
        var request = new AiChatRequest(
            PlayerKey: playerName,
            PlayerName: playerName,
            PlayerTeam: player == null ? "None" : TeamName(player.Team),
            PlayerAlive: player?.PawnIsAlive ?? false,
            BotKey: GetPlayerKey(bot),
            BotName: bot.PlayerName,
            BotTeam: TeamName(bot.Team),
            BattleSummary: player == null ? "命令测试，无真实战场上下文。" : BuildBattleSummary(player, bot),
            PlayerMessage: message,
            TeamChat: false);
        _ = ReplyToAiTestCommandAsync(player, command, request, Config);
    }

    private async Task ReplyToAiTestCommandAsync(CCSPlayerController? player, CommandInfo command, AiChatRequest request, BotTauntConfig config)
    {
        try
        {
            var reply = await _aiChatClient.CreateChatReplyAsync(request, _aiMemory.GetRecentContext(config), config);
            var processed = AiReplyPostProcessor.Process(reply, config.MaxAiReplyLength, request.BotName);
            Server.NextFrame(() =>
            {
                ReplyToCommand(player, command, processed.ShouldSend
                    ? $"[BotTaunt] {request.BotName}: {processed.Text}"
                    : $"[BotTaunt] Empty response from AI: {processed.Reason}");
            });
        }
        catch (Exception ex)
        {
            Server.NextFrame(() => ReplyToCommand(player, command, $"[BotTaunt] AI test failed: {ex.Message}"));
        }
    }

    private static void ReplyToCommand(CCSPlayerController? player, CommandInfo command, string message)
    {
        if (player is { IsValid: true })
        {
            player.PrintToChat(message);
            return;
        }

        command.ReplyToCommand(message);
    }

    [ConsoleCommand("lbtv_bot_rivalry", "Enable or disable low-frequency bot-vs-bot taunts. Usage: lbtv_bot_rivalry 0/1")]
    public void OnBotRivalryCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (command.ArgCount < 2)
        {
            command.ReplyToCommand($"lbtv_bot_rivalry is {(_botRivalryEnabled ? 1 : 0)}");
            return;
        }

        var value = command.ArgByIndex(1);
        if (value == "1")
        {
            _botRivalryEnabled = true;
            command.ReplyToCommand("lbtv_bot_rivalry set to 1");
            return;
        }

        if (value == "0")
        {
            _botRivalryEnabled = false;
            command.ReplyToCommand("lbtv_bot_rivalry set to 0");
            return;
        }

        command.ReplyToCommand("Usage: lbtv_bot_rivalry 0/1");
    }

    private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        _roundSerial++;
        _roundEnded = false;
        _aiMemory.ResetRound();
        _roundTauntCount = 0;
        _roundBotRivalryTauntCount = 0;
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

        SendTauntReply(bot, _tauntPools.SaveTaunt);
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

        SendTauntReply(bot, _tauntPools.ClutchTaunt);
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
            _nextRivalryTauntTimeByBot.Remove(GetPlayerKey(player));
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

        if (IsBotVictim(victim))
        {
            if (TryPrintBotRivalryTaunt(attacker, victim, @event))
            {
                return HookResult.Continue;
            }

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

        var taunts = GetTauntPool(@event, out var chance);
        if (_random.NextDouble() >= chance)
        {
            return HookResult.Continue;
        }

        SendTauntReply(attacker, taunts[_random.Next(taunts.Length)]);
        _roundTauntCount++;
        _nextTauntTimeByBot[GetPlayerKey(attacker)] = Server.CurrentTime + BotCooldownSeconds;
        return HookResult.Continue;
    }

    private bool TryPrintBotRivalryTaunt(CCSPlayerController attacker, CCSPlayerController? victim, EventPlayerDeath @event)
    {
        if (!_botRivalryEnabled
            || !IsBotVictim(victim)
            || _roundBotRivalryTauntCount >= Config.MaxBotRivalryTauntsPerRound)
        {
            return false;
        }

        var key = GetPlayerKey(attacker);
        if (_nextRivalryTauntTimeByBot.TryGetValue(key, out var nextTime) && Server.CurrentTime < nextTime)
        {
            return false;
        }

        var chance = (@event.Headshot || IsKnifeWeapon(@event.Weapon))
            ? Config.BotRivalrySpecialTauntChance
            : Config.BotRivalryTauntChance;
        if (_random.NextDouble() >= chance)
        {
            return false;
        }

        var taunts = _tauntPools.BotRivalryTaunts;
        SendTauntReply(attacker, taunts[_random.Next(taunts.Length)]);
        _roundBotRivalryTauntCount++;
        _nextRivalryTauntTimeByBot[key] = Server.CurrentTime + (float)Config.BotRivalryCooldownSeconds;
        return true;
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

        SendTauntReply(bot, _tauntPools.RoundKillTaunt);
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

        SendTauntReply(bot, _tauntPools.MultiKillTaunt);
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

        if (_random.NextDouble() >= Config.MvpTauntChance)
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
        if (!_aiChatEnabled || !IsHumanVictim(player) || player == null)
        {
            return;
        }

        var config = Config;
        if (teamChat && !config.AiReplyToTeamChat)
        {
            return;
        }

        if (IsPlayerMessageTooLong(message, config.MaxPlayerMessageLength))
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

        _aiMemory.AddPlayerMessage(player.PlayerName, cleanMessage, config);

        if (!ShouldAttemptAiReply(cleanMessage, config))
        {
            return;
        }

        var bot = PickReplyBot(player, teamChat, config);
        if (bot == null)
        {
            return;
        }

        var playerKey = GetPlayerKey(player).ToString(CultureInfo.InvariantCulture);
        var botKey = GetPlayerKey(bot);
        var request = new AiChatRequest(
            PlayerKey: playerKey,
            PlayerName: player!.PlayerName,
            PlayerTeam: TeamName(player.Team),
            PlayerAlive: player.PawnIsAlive,
            BotKey: botKey,
            BotName: bot.PlayerName,
            BotTeam: TeamName(bot.Team),
            BattleSummary: BuildBattleSummary(player, bot),
            PlayerMessage: cleanMessage,
            TeamChat: teamChat
        );

        if (_aiReplyInFlight)
        {
            _pendingAiChatRequest = request;
            return;
        }

        if (!_aiMemory.CanReply(playerKey, botKey, Server.CurrentTime, config, out _))
        {
            return;
        }

        StartAiChatRequest(request);
    }

    private string[] GetTauntPool(EventPlayerDeath @event, out float chance)
    {
        if (IsAwpWeapon(@event.Weapon))
        {
            chance = NormalTauntChance;
            return _tauntPools.NormalTaunts;
        }

        if (@event.Headshot)
        {
            chance = SpecialTauntChance;
            return _tauntPools.HeadshotTaunts;
        }

        if (IsKnifeWeapon(@event.Weapon))
        {
            chance = SpecialTauntChance;
            return _tauntPools.KnifeTaunts;
        }

        chance = NormalTauntChance;
        return _tauntPools.NormalTaunts;
    }

    private static void PrintTaunt(CCSPlayerController bot, string taunt)
    {
        Server.PrintToChatAll($" {ChatColorRed}{LbtvPrefix} {bot.PlayerName}: {taunt}{ChatColorDefault}");
    }

    private static void PrintTaunt(string botName, string taunt)
    {
        Server.PrintToChatAll($" {ChatColorRed}{LbtvPrefix} {botName}: {taunt}{ChatColorDefault}");
    }

    private void SendTauntReply(CCSPlayerController bot, string taunt)
    {
        var sent = TryNativeBotSay(bot, teamChat: false, taunt, Config.MaxAiReplyLength);
        if (!sent)
        {
            PrintTaunt(bot, taunt);
        }

        _aiMemory.AddBotMessage(bot.PlayerName, taunt, Config);
    }

    private static void PrintAiChat(CCSPlayerController bot, string reply)
    {
        Server.PrintToChatAll($" {ChatColors.Green}{LbtvPrefix} {bot.PlayerName}: {reply}{ChatColorDefault}");
    }

    private static void PrintAiChat(string botName, string reply)
    {
        Server.PrintToChatAll($" {ChatColors.Green}{LbtvPrefix} {botName}: {reply}{ChatColorDefault}");
    }

    private static void PrintAiChatTeam(CsTeam team, string botName, string reply)
    {
        foreach (var target in Utilities.GetPlayers().Where(player => player is { IsValid: true } && player.Team == team))
        {
            target.PrintToChat($" {ChatColors.Green}{LbtvPrefix} {botName}: {reply}{ChatColorDefault}");
        }
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
        var speakers = bots
            .Where(_ => _random.NextDouble() < Config.OpeningTrashTalkBotChance)
            .ToList();
        if (speakers.Count == 0)
        {
            speakers.Add(bots[0]);
        }

        _openingTrashTalkStarted = true;

        for (var i = 0; i < speakers.Count; i++)
        {
            var speaker = speakers[i];
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

        var trashTalk = _tauntPools.OpeningTrashTalks[_random.Next(_tauntPools.OpeningTrashTalks.Length)];
        SendAiChatReply(bot, teamChat: false, trashTalk, Config);
        _aiMemory.AddBotMessage(bot.PlayerName, trashTalk, Config);
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
        _aiMemory.MarkReplyScheduled(request.PlayerKey, request.BotKey, Server.CurrentTime);
        _aiReplyInFlight = true;
        _ = ReplyToPlayerChatAsync(request, Config);
    }

    private void StartMvpAiTauntRequest(int botKey, string botName, string botTeam)
    {
        _ = PrintMvpAiTauntAsync(new MvpAiTauntRequest(botKey, botName, botTeam, BuildMvpBattleSummary(botKey)), Config);
    }

    private async Task ReplyToPlayerChatAsync(AiChatRequest request, BotTauntConfig config)
    {
        string? reply = null;
        try
        {
            reply = await _aiChatClient.CreateChatReplyAsync(request, _aiMemory.GetRecentContext(config), config);
        }
        catch
        {
            // AI chat is optional; fixed taunts should keep working if the network fails.
        }

        var processed = AiReplyPostProcessor.Process(reply, config.MaxAiReplyLength, request.BotName);
        reply = processed.ShouldSend ? processed.Text : BuildFallbackAiReply(request);
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
                SendAiChatReply(bot, request.TeamChat, reply, config);
            }
            else if (config.AiFallbackToPluginPrint)
            {
                PrintAiChat(request.BotName, reply);
            }

            _aiMemory.AddBotMessage(bot?.PlayerName ?? request.BotName, reply, config);
            SchedulePendingAiChat();
        });
    }

    private async Task PrintMvpAiTauntAsync(MvpAiTauntRequest request, BotTauntConfig config)
    {
        string? taunt = null;
        try
        {
            taunt = await _aiChatClient.CreateMvpReplyAsync(request, config);
        }
        catch
        {
            // MVP taunts fall back to the fixed local pool if the AI service is unavailable.
        }

        var processed = AiReplyPostProcessor.Process(taunt, config.MaxAiReplyLength, request.BotName);
        taunt = processed.ShouldSend ? processed.Text : null;
        Server.NextFrame(() =>
        {
            var fallback = _tauntPools.NormalTaunts[_random.Next(_tauntPools.NormalTaunts.Length)];
            var bot = FindPlayerByKey(request.BotKey);
            if (bot is { IsValid: true, IsBot: true })
            {
                SendTauntReply(bot, taunt ?? fallback);
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

        var delay = Math.Max(0.1f, (float)Config.AiGlobalCooldownSeconds);
        AddTimer(delay, () =>
        {
            if (_pendingAiChatRequest == null || _aiReplyInFlight)
            {
                SchedulePendingAiChat();
                return;
            }

            var request = _pendingAiChatRequest;
            _pendingAiChatRequest = null;
            StartAiChatRequest(request);
        });
    }

    private bool ShouldAttemptAiReply(string message, BotTauntConfig config)
    {
        var hasMention = message.Contains("@", StringComparison.Ordinal)
            || message.Contains("bot", StringComparison.OrdinalIgnoreCase)
            || message.Contains("机器人", StringComparison.OrdinalIgnoreCase)
            || message.Contains("张老师", StringComparison.OrdinalIgnoreCase)
            || message.Contains("张雪峰", StringComparison.OrdinalIgnoreCase);
        var chance = hasMention ? config.AiMentionReplyChance : config.AiNoMentionReplyChance;
        if (LooksLikeQuestion(message))
        {
            chance += config.AiQuestionBoost;
        }

        chance = Math.Clamp(chance, 0.0, 1.0);
        return chance >= 1.0 || _random.NextDouble() < chance;
    }

    private static bool LooksLikeQuestion(string message)
    {
        return message.Contains('?')
            || message.Contains('？')
            || message.Contains("吗", StringComparison.OrdinalIgnoreCase)
            || message.Contains("呢", StringComparison.OrdinalIgnoreCase)
            || message.Contains("怎么", StringComparison.OrdinalIgnoreCase)
            || message.Contains("咋", StringComparison.OrdinalIgnoreCase)
            || message.Contains("为什么", StringComparison.OrdinalIgnoreCase)
            || message.Contains("怎么看", StringComparison.OrdinalIgnoreCase)
            || message.Contains("what", StringComparison.OrdinalIgnoreCase)
            || message.Contains("why", StringComparison.OrdinalIgnoreCase)
            || message.Contains("how", StringComparison.OrdinalIgnoreCase);
    }

    private void SendAiChatReply(CCSPlayerController bot, bool teamChat, string reply, BotTauntConfig config)
    {
        var sent = false;
        if (config.AiPreferNativeBotSay)
        {
            sent = TryNativeBotSay(bot, teamChat, reply, config.MaxAiReplyLength);
        }

        if (sent || !config.AiFallbackToPluginPrint)
        {
            return;
        }

        if (teamChat)
        {
            PrintAiChatTeam(bot.Team, bot.PlayerName, reply);
            return;
        }

        PrintAiChat(bot, reply);
    }

    private static bool TryNativeBotSay(CCSPlayerController bot, bool teamChat, string reply, int maxChars)
    {
        var safeLine = AiChatText.SanitizeForClientCommand(reply, maxChars);
        if (safeLine.Length == 0)
        {
            return false;
        }

        var commandName = teamChat ? "say_team" : "say";
        var command = $"{commandName} {QuoteConsoleArgument(safeLine)}";
        try
        {
            bot.ExecuteClientCommandFromServer(command);
            return true;
        }
        catch
        {
            try
            {
                bot.ExecuteClientCommand(command);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    private static string QuoteConsoleArgument(string value)
    {
        return $"\"{value.Replace("\\", "\\\\").Replace("\"", "'")}\"";
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
        _roundBotRivalryTauntCount = 0;
        _nextTauntTimeByBot.Clear();
        _nextRivalryTauntTimeByBot.Clear();
        _roundKillsByBot.Clear();
        _recentKillTimesByBot.Clear();
        _multiKillTauntedBots.Clear();
        _aceTauntedBots.Clear();
        _lastChatHandledTime = 0f;
        _lastChatSignature = string.Empty;
        _aiReplyInFlight = false;
        _pendingAiChatRequest = null;
        _aiMemory.ResetAll();
        _clutchTauntedThisRound = false;
        _saveTauntedThisRound = false;
        _roundKillTauntedThisRound = false;
        _openingTrashTalkStarted = false;
        _roundSerial = 0;
        _roundEnded = true;
    }

    private CCSPlayerController? PickReplyBot(CCSPlayerController player, bool teamChat, BotTauntConfig config)
    {
        var bots = Utilities.GetPlayers()
            .Where(p => p is { IsValid: true, IsBot: true, IsHLTV: false }
                        && p.Team is CsTeam.CounterTerrorist or CsTeam.Terrorist)
            .ToList();
        if (bots.Count == 0)
        {
            return null;
        }

        if (teamChat && config.AiPreferSameTeamBot)
        {
            var sameTeamBots = bots
                .Where(p => p.Team == player.Team)
                .OrderByDescending(p => p.PawnIsAlive)
                .ToList();
            if (sameTeamBots.Count > 0)
            {
                return sameTeamBots[_random.Next(sameTeamBots.Count)];
            }
        }

        var pool = bots.OrderByDescending(p => p.PawnIsAlive).ToList();
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

    private static bool IsPlayerMessageTooLong(string? message, int maxLength)
    {
        var normalized = NormalizePlayerMessage(message);
        return normalized.Length > maxLength;
    }

    private static string NormalizePlayerMessage(string? message)
    {
        return (message ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
    }

    private static bool IsBotAttacker(CCSPlayerController? player)
    {
        return player is { IsValid: true, IsBot: true, IsHLTV: false };
    }

    private static bool IsHumanVictim(CCSPlayerController? player)
    {
        return player is { IsValid: true, IsBot: false, IsHLTV: false };
    }

    private static bool IsBotVictim(CCSPlayerController? player)
    {
        return player is { IsValid: true, IsBot: true, IsHLTV: false };
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

    private sealed class TauntPools
    {
        [JsonPropertyName("NormalTaunts")]
        public string[] NormalTaunts { get; set; } = BotTauntPlugin.NormalTaunts;

        [JsonPropertyName("HeadshotTaunts")]
        public string[] HeadshotTaunts { get; set; } = BotTauntPlugin.HeadshotTaunts;

        [JsonPropertyName("KnifeTaunts")]
        public string[] KnifeTaunts { get; set; } = BotTauntPlugin.KnifeTaunts;

        [JsonPropertyName("BotRivalryTaunts")]
        public string[] BotRivalryTaunts { get; set; } = BotTauntPlugin.BotRivalryTaunts;

        [JsonPropertyName("OpeningTrashTalks")]
        public string[] OpeningTrashTalks { get; set; } = BotTauntPlugin.OpeningTrashTalks;

        [JsonPropertyName("RoundKillTaunt")]
        public string RoundKillTaunt { get; set; } = BotTauntPlugin.DefaultRoundKillTaunt;

        [JsonPropertyName("MultiKillTaunt")]
        public string MultiKillTaunt { get; set; } = BotTauntPlugin.DefaultMultiKillTaunt;

        [JsonPropertyName("ClutchTaunt")]
        public string ClutchTaunt { get; set; } = BotTauntPlugin.DefaultClutchTaunt;

        [JsonPropertyName("SaveTaunt")]
        public string SaveTaunt { get; set; } = BotTauntPlugin.DefaultSaveTaunt;

        public static TauntPools CreateDefault()
        {
            return new TauntPools
            {
                NormalTaunts = BotTauntPlugin.NormalTaunts,
                HeadshotTaunts = BotTauntPlugin.HeadshotTaunts,
                KnifeTaunts = BotTauntPlugin.KnifeTaunts,
                BotRivalryTaunts = BotTauntPlugin.BotRivalryTaunts,
                OpeningTrashTalks = BotTauntPlugin.OpeningTrashTalks,
                RoundKillTaunt = BotTauntPlugin.DefaultRoundKillTaunt,
                MultiKillTaunt = BotTauntPlugin.DefaultMultiKillTaunt,
                ClutchTaunt = BotTauntPlugin.DefaultClutchTaunt,
                SaveTaunt = BotTauntPlugin.DefaultSaveTaunt,
            };
        }

        public TauntPools Normalized()
        {
            NormalTaunts = NormalizePool(NormalTaunts, BotTauntPlugin.NormalTaunts);
            HeadshotTaunts = NormalizePool(HeadshotTaunts, BotTauntPlugin.HeadshotTaunts);
            KnifeTaunts = NormalizePool(KnifeTaunts, BotTauntPlugin.KnifeTaunts);
            BotRivalryTaunts = NormalizePool(BotRivalryTaunts, BotTauntPlugin.BotRivalryTaunts);
            OpeningTrashTalks = NormalizePool(OpeningTrashTalks, BotTauntPlugin.OpeningTrashTalks);
            RoundKillTaunt = NormalizeLine(RoundKillTaunt, BotTauntPlugin.DefaultRoundKillTaunt);
            MultiKillTaunt = NormalizeLine(MultiKillTaunt, BotTauntPlugin.DefaultMultiKillTaunt);
            ClutchTaunt = NormalizeLine(ClutchTaunt, BotTauntPlugin.DefaultClutchTaunt);
            SaveTaunt = NormalizeLine(SaveTaunt, BotTauntPlugin.DefaultSaveTaunt);
            return this;
        }

        private static string[] NormalizePool(string[]? pool, string[] fallback)
        {
            var cleaned = (pool ?? Array.Empty<string>())
                .Select(item => (item ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToArray();
            return cleaned.Length > 0 ? cleaned : fallback;
        }

        private static string NormalizeLine(string? line, string fallback)
        {
            var cleaned = (line ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
            return string.IsNullOrWhiteSpace(cleaned) ? fallback : cleaned;
        }
    }

    private sealed record OpeningTrashTalkSpeaker(int BotKey, string BotName);
}

public sealed class BotTauntConfig : BasePluginConfig
{
    [JsonPropertyName("AiChatEnabled")]
    public bool AiChatEnabled { get; set; } = true;

    [JsonPropertyName("AiApiUrl")]
    public string AiApiUrl { get; set; } = BotTauntPlugin.DefaultAiApiUrl;

    [JsonPropertyName("AiApiKey")]
    public string AiApiKey { get; set; } = BotTauntPlugin.DefaultAiApiKey;

    [JsonPropertyName("AiModel")]
    public string AiModel { get; set; } = string.Empty;

    [JsonPropertyName("AiApiMode")]
    public string AiApiMode { get; set; } = "auto";

    [JsonPropertyName("AiTemperature")]
    public double AiTemperature { get; set; } = BotTauntPlugin.DefaultAiTemperature;

    [JsonPropertyName("AiTopP")]
    public double? AiTopP { get; set; } = 0.9;

    [JsonPropertyName("AiMaxOutputTokens")]
    public int AiMaxOutputTokens { get; set; } = BotTauntPlugin.DefaultAiMaxOutputTokens;

    [JsonPropertyName("AiChatTimeoutSeconds")]
    public int AiChatTimeoutSeconds { get; set; } = BotTauntPlugin.DefaultAiChatTimeoutSeconds;

    [JsonPropertyName("MaxPlayerMessageLength")]
    public int MaxPlayerMessageLength { get; set; } = BotTauntPlugin.DefaultMaxPlayerMessageLength;

    [JsonPropertyName("MaxAiReplyLength")]
    public int MaxAiReplyLength { get; set; } = BotTauntPlugin.DefaultMaxAiReplyLength;

    [JsonPropertyName("AiPreferNativeBotSay")]
    public bool AiPreferNativeBotSay { get; set; } = BotTauntPlugin.DefaultAiPreferNativeBotSay;

    [JsonPropertyName("AiFallbackToPluginPrint")]
    public bool AiFallbackToPluginPrint { get; set; } = BotTauntPlugin.DefaultAiFallbackToPluginPrint;

    [JsonPropertyName("AiReplyToTeamChat")]
    public bool AiReplyToTeamChat { get; set; } = BotTauntPlugin.DefaultAiReplyToTeamChat;

    [JsonPropertyName("AiPreferSameTeamBot")]
    public bool AiPreferSameTeamBot { get; set; } = BotTauntPlugin.DefaultAiPreferSameTeamBot;

    [JsonPropertyName("AiNoMentionReplyChance")]
    public double AiNoMentionReplyChance { get; set; } = BotTauntPlugin.DefaultAiNoMentionReplyChance;

    [JsonPropertyName("AiMentionReplyChance")]
    public double AiMentionReplyChance { get; set; } = BotTauntPlugin.DefaultAiMentionReplyChance;

    [JsonPropertyName("AiQuestionBoost")]
    public double AiQuestionBoost { get; set; } = BotTauntPlugin.DefaultAiQuestionBoost;

    [JsonPropertyName("AiGlobalCooldownSeconds")]
    public double AiGlobalCooldownSeconds { get; set; } = BotTauntPlugin.DefaultAiGlobalCooldownSeconds;

    [JsonPropertyName("AiPerPlayerCooldownSeconds")]
    public double AiPerPlayerCooldownSeconds { get; set; } = BotTauntPlugin.DefaultAiPerPlayerCooldownSeconds;

    [JsonPropertyName("AiPerBotCooldownSeconds")]
    public double AiPerBotCooldownSeconds { get; set; } = BotTauntPlugin.DefaultAiPerBotCooldownSeconds;

    [JsonPropertyName("AiMaxRepliesPerMinute")]
    public int AiMaxRepliesPerMinute { get; set; } = BotTauntPlugin.DefaultAiMaxRepliesPerMinute;

    [JsonPropertyName("AiMaxRepliesPerRound")]
    public int AiMaxRepliesPerRound { get; set; } = BotTauntPlugin.DefaultAiMaxRepliesPerRound;

    [JsonPropertyName("AiRecentContextMessages")]
    public int AiRecentContextMessages { get; set; } = BotTauntPlugin.DefaultAiRecentContextMessages;

    [JsonPropertyName("AiRecentContextMaxChars")]
    public int AiRecentContextMaxChars { get; set; } = BotTauntPlugin.DefaultAiRecentContextMaxChars;

    [JsonPropertyName("AiStoreBotMessagesInRecentContext")]
    public bool AiStoreBotMessagesInRecentContext { get; set; } = true;

    [JsonPropertyName("OpeningTrashTalkBotChance")]
    public double OpeningTrashTalkBotChance { get; set; } = BotTauntPlugin.DefaultOpeningTrashTalkBotChance;

    [JsonPropertyName("MvpTauntChance")]
    public double MvpTauntChance { get; set; } = BotTauntPlugin.DefaultMvpTauntChance;

    [JsonPropertyName("BotRivalryEnabled")]
    public bool BotRivalryEnabled { get; set; } = BotTauntPlugin.DefaultBotRivalryEnabled;

    [JsonPropertyName("BotRivalryTauntChance")]
    public double BotRivalryTauntChance { get; set; } = BotTauntPlugin.DefaultBotRivalryTauntChance;

    [JsonPropertyName("BotRivalrySpecialTauntChance")]
    public double BotRivalrySpecialTauntChance { get; set; } = BotTauntPlugin.DefaultBotRivalrySpecialTauntChance;

    [JsonPropertyName("MaxBotRivalryTauntsPerRound")]
    public int MaxBotRivalryTauntsPerRound { get; set; } = BotTauntPlugin.DefaultMaxBotRivalryTauntsPerRound;

    [JsonPropertyName("BotRivalryCooldownSeconds")]
    public double BotRivalryCooldownSeconds { get; set; } = BotTauntPlugin.DefaultBotRivalryCooldownSeconds;

    public override int Version { get; set; } = 1;

    public static BotTauntConfig CreateDefault()
    {
        return new BotTauntConfig();
    }

    public BotTauntConfig Normalized()
    {
        AiApiUrl = string.IsNullOrWhiteSpace(AiApiUrl) ? BotTauntPlugin.DefaultAiApiUrl : AiApiUrl.Trim();
        AiApiKey = AiApiKey?.Trim() ?? string.Empty;
        AiModel = AiModel?.Trim() ?? string.Empty;
        AiApiMode = NormalizeApiMode(AiApiMode);
        AiTemperature = double.IsFinite(AiTemperature) ? Math.Clamp(AiTemperature, 0.0, 2.0) : BotTauntPlugin.DefaultAiTemperature;
        AiTopP = AiTopP is { } topP && double.IsFinite(topP) ? Math.Clamp(topP, 0.0, 1.0) : null;
        AiMaxOutputTokens = Math.Clamp(AiMaxOutputTokens, 1, 512);
        AiChatTimeoutSeconds = Math.Clamp(AiChatTimeoutSeconds, 3, 60);
        MaxPlayerMessageLength = Math.Clamp(MaxPlayerMessageLength, 1, 1000);
        MaxAiReplyLength = Math.Clamp(MaxAiReplyLength, 1, 1000);
        AiNoMentionReplyChance = double.IsFinite(AiNoMentionReplyChance)
            ? Math.Clamp(AiNoMentionReplyChance, 0.0, 1.0)
            : BotTauntPlugin.DefaultAiNoMentionReplyChance;
        AiMentionReplyChance = double.IsFinite(AiMentionReplyChance)
            ? Math.Clamp(AiMentionReplyChance, 0.0, 1.0)
            : BotTauntPlugin.DefaultAiMentionReplyChance;
        AiQuestionBoost = double.IsFinite(AiQuestionBoost)
            ? Math.Clamp(AiQuestionBoost, 0.0, 1.0)
            : BotTauntPlugin.DefaultAiQuestionBoost;
        AiGlobalCooldownSeconds = double.IsFinite(AiGlobalCooldownSeconds)
            ? Math.Clamp(AiGlobalCooldownSeconds, 0.0, 60.0)
            : BotTauntPlugin.DefaultAiGlobalCooldownSeconds;
        AiPerPlayerCooldownSeconds = double.IsFinite(AiPerPlayerCooldownSeconds)
            ? Math.Clamp(AiPerPlayerCooldownSeconds, 0.0, 180.0)
            : BotTauntPlugin.DefaultAiPerPlayerCooldownSeconds;
        AiPerBotCooldownSeconds = double.IsFinite(AiPerBotCooldownSeconds)
            ? Math.Clamp(AiPerBotCooldownSeconds, 0.0, 180.0)
            : BotTauntPlugin.DefaultAiPerBotCooldownSeconds;
        AiMaxRepliesPerMinute = Math.Clamp(AiMaxRepliesPerMinute, 0, 60);
        AiMaxRepliesPerRound = Math.Clamp(AiMaxRepliesPerRound, 0, 60);
        AiRecentContextMessages = Math.Clamp(AiRecentContextMessages, 0, 12);
        AiRecentContextMaxChars = Math.Clamp(AiRecentContextMaxChars, 0, 2000);
        OpeningTrashTalkBotChance = double.IsFinite(OpeningTrashTalkBotChance)
            ? Math.Clamp(OpeningTrashTalkBotChance, 0.0, 1.0)
            : BotTauntPlugin.DefaultOpeningTrashTalkBotChance;
        MvpTauntChance = double.IsFinite(MvpTauntChance)
            ? Math.Clamp(MvpTauntChance, 0.0, 1.0)
            : BotTauntPlugin.DefaultMvpTauntChance;
        BotRivalryTauntChance = double.IsFinite(BotRivalryTauntChance)
            ? Math.Clamp(BotRivalryTauntChance, 0.0, 1.0)
            : BotTauntPlugin.DefaultBotRivalryTauntChance;
        BotRivalrySpecialTauntChance = double.IsFinite(BotRivalrySpecialTauntChance)
            ? Math.Clamp(BotRivalrySpecialTauntChance, 0.0, 1.0)
            : BotTauntPlugin.DefaultBotRivalrySpecialTauntChance;
        MaxBotRivalryTauntsPerRound = Math.Clamp(MaxBotRivalryTauntsPerRound, 0, 10);
        BotRivalryCooldownSeconds = double.IsFinite(BotRivalryCooldownSeconds)
            ? Math.Clamp(BotRivalryCooldownSeconds, 5.0, 180.0)
            : BotTauntPlugin.DefaultBotRivalryCooldownSeconds;
        return this;
    }

    private static string NormalizeApiMode(string? mode)
    {
        var normalized = (mode ?? string.Empty).Trim().ToLowerInvariant();
        return normalized is "default" or "openai" or "chat-completions"
            ? normalized
            : "auto";
    }
}
