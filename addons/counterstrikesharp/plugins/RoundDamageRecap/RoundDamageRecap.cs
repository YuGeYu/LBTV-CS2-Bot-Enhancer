using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using System.Security.Cryptography;

namespace RoundDamageRecap;

[MinimumApiVersion(304)]
public sealed class RoundDamageRecapPlugin : BasePlugin
{
    public override string ModuleName => "RoundDamageRecap";
    public override string ModuleVersion => "1.2.1";
    public override string ModuleAuthor => "YuGeYu (modified by ed0ard)";
    public override string ModuleDescription => "Shows a round-end damage recap and current difficulty in chat.";

    private const string LbtvPrefix = "[LBTV]";
    private const string ChatColorGreen = "\u0006";
    private const string ChatColorDefault = "\u0001";
    private const float RecentUtilityKeepSeconds = 20.0f;
    private const float ExplosionAttributionSeconds = 3.0f;
    private const float FireAttributionSeconds = 12.0f;
    private const float ThrownImpactAttributionSeconds = 2.5f;
    private const float HeAttributionRadius = 500.0f;
    private const float FireAttributionRadius = 700.0f;
    private const float OtherUtilityAttributionRadius = 300.0f;

    private readonly Dictionary<int, Dictionary<int, DamageEntry>> _damageByAttacker = new();
    private readonly Dictionary<int, PlayerSnapshot> _playersByKey = new();
    private readonly List<UtilityDetonation> _recentUtilityDetonations = new();
    private readonly List<ThrownUtility> _recentThrownUtilities = new();
    private bool _announcedDifficultyThisMap;

    public override void Load(bool hotReload)
    {
        RegisterEventHandler<EventRoundStart>(OnRoundStart);
        RegisterEventHandler<EventPlayerHurt>(OnPlayerHurt);
        RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
        RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
        RegisterEventHandler<EventGrenadeThrown>(OnGrenadeThrown);
        RegisterEventHandler<EventHegrenadeDetonate>(OnHegrenadeDetonate);
        RegisterEventHandler<EventMolotovDetonate>(OnMolotovDetonate);
        RegisterEventHandler<EventSmokegrenadeDetonate>(OnSmokegrenadeDetonate);
        RegisterEventHandler<EventFlashbangDetonate>(OnFlashbangDetonate);
        RegisterEventHandler<EventTagrenadeDetonate>(OnTagrenadeDetonate);
        RegisterListener<Listeners.OnMapStart>(_ =>
        {
            _damageByAttacker.Clear();
            _playersByKey.Clear();
            _recentUtilityDetonations.Clear();
            _recentThrownUtilities.Clear();
            _announcedDifficultyThisMap = false;
        });
    }

    private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        _damageByAttacker.Clear();
        _recentUtilityDetonations.Clear();
        _recentThrownUtilities.Clear();
        SnapshotAllPlayers();
        AnnounceDifficultyOncePerMap();
        return HookResult.Continue;
    }

    private HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null)
        {
            return HookResult.Continue;
        }

        var key = GetPlayerKey(player);
        RemovePlayerStats(key);
        _playersByKey.Remove(key);
        _recentUtilityDetonations.RemoveAll(item => item.ThrowerKey == key);
        _recentThrownUtilities.RemoveAll(item => item.ThrowerKey == key);
        return HookResult.Continue;
    }

    private HookResult OnPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
    {
        var victim = @event.Userid;
        if (!IsTrackablePlayer(victim) || victim == null)
        {
            return HookResult.Continue;
        }

        var attacker = ResolveDamageAttacker(@event, victim);
        if (!IsTrackablePlayer(attacker) || attacker == null)
        {
            return HookResult.Continue;
        }

        var attackerKey = GetPlayerKey(attacker);
        var victimKey = GetPlayerKey(victim);
        if (attackerKey == victimKey)
        {
            return HookResult.Continue;
        }

        AddDamage(attacker, victim, Math.Max(0, @event.DmgHealth), Math.Max(0, @event.Health));
        return HookResult.Continue;
    }

    private HookResult OnGrenadeThrown(EventGrenadeThrown @event, GameEventInfo info)
    {
        if (IsTrackablePlayer(@event.Userid) && @event.Userid != null)
        {
            RememberThrownUtility(@event.Userid, @event.Weapon);
        }

        return HookResult.Continue;
    }

    private HookResult OnHegrenadeDetonate(EventHegrenadeDetonate @event, GameEventInfo info)
    {
        RememberUtilityDetonation(@event.Userid, "hegrenade", @event.X, @event.Y, @event.Z);
        return HookResult.Continue;
    }

    private HookResult OnMolotovDetonate(EventMolotovDetonate @event, GameEventInfo info)
    {
        RememberUtilityDetonation(@event.Userid, "inferno", @event.X, @event.Y, @event.Z);
        RememberUtilityDetonation(@event.Userid, "molotov", @event.X, @event.Y, @event.Z);
        RememberUtilityDetonation(@event.Userid, "incgrenade", @event.X, @event.Y, @event.Z);
        return HookResult.Continue;
    }

    private HookResult OnSmokegrenadeDetonate(EventSmokegrenadeDetonate @event, GameEventInfo info)
    {
        RememberUtilityDetonation(@event.Userid, "smokegrenade", @event.X, @event.Y, @event.Z);
        return HookResult.Continue;
    }

    private HookResult OnFlashbangDetonate(EventFlashbangDetonate @event, GameEventInfo info)
    {
        RememberUtilityDetonation(@event.Userid, "flashbang", @event.X, @event.Y, @event.Z);
        return HookResult.Continue;
    }

    private HookResult OnTagrenadeDetonate(EventTagrenadeDetonate @event, GameEventInfo info)
    {
        RememberUtilityDetonation(@event.Userid, "tagrenade", @event.X, @event.Y, @event.Z);
        return HookResult.Continue;
    }

    private void AddDamage(CCSPlayerController attacker, CCSPlayerController victim, int damage, int victimHealth)
    {
        SnapshotPlayer(attacker);
        SnapshotPlayer(victim);

        var attackerKey = GetPlayerKey(attacker);
        var victimKey = GetPlayerKey(victim);
        if (!_damageByAttacker.TryGetValue(attackerKey, out var victimEntries))
        {
            victimEntries = new Dictionary<int, DamageEntry>();
            _damageByAttacker[attackerKey] = victimEntries;
        }

        if (!victimEntries.TryGetValue(victimKey, out var entry))
        {
            entry = new DamageEntry();
            victimEntries[victimKey] = entry;
        }

        entry.TargetName = victim.PlayerName;
        entry.TotalDamage += damage;
        entry.HitCount += 1;
        entry.LastKnownHealth = victimHealth;
    }

    private HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        SnapshotAllPlayers();

        foreach (var player in Utilities.GetPlayers().Where(IsEligibleRecipient))
        {
            PrintRecapForPlayer(player);
        }

        return HookResult.Continue;
    }

    [ConsoleCommand("lbtv_difficulty", "Shows the currently active bot difficulty profile.")]
    public void OnDifficultyCommand(CCSPlayerController? player, CommandInfo command)
    {
        var message = BuildDifficultyMessage();
        if (player is { IsValid: true })
        {
            PrintLbtvLine(player, message);
        }
        else
        {
            command.ReplyToCommand(message);
        }
    }

    private void PrintRecapForPlayer(CCSPlayerController player)
    {
        var enemyTeam = GetEnemyTeam(player.Team);
        if (enemyTeam == null)
        {
            return;
        }

        var enemyPlayers = Utilities.GetPlayers()
            .Where(p => IsTrackablePlayer(p) && p.Team == enemyTeam)
            .OrderByDescending(p => GetDamageBetween(player, p).TotalDamage + GetDamageBetween(p, player).TotalDamage)
            .ThenBy(p => p.PlayerName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var enemy in enemyPlayers)
        {
            var toEntry = GetDamageBetween(player, enemy);
            var fromEntry = GetDamageBetween(enemy, player);
            var remainingHp = GetDisplayedHealth(enemy, toEntry);

            PrintLbtvLine(
                player,
                $"{enemy.PlayerName} [{(remainingHp > 0 ? $"{remainingHp} HP left" : "DEAD")}] - " +
                $"Dealt to: [{toEntry.TotalDamage} in {toEntry.HitCount} {(toEntry.HitCount <= 1 ? "hit" : "hits")}] - " +
                $"Taken from: [{fromEntry.TotalDamage} in {fromEntry.HitCount} {(fromEntry.HitCount <= 1 ? "hit" : "hits")}]");
        }
    }

    private static void PrintLbtvLine(CCSPlayerController player, string text)
    {
        player.PrintToChat($" {ChatColorGreen}{text}{ChatColorDefault}");
    }

    private CCSPlayerController? ResolveDamageAttacker(EventPlayerHurt @event, CCSPlayerController victim)
    {
        if (IsTrackablePlayer(@event.Attacker) && @event.Attacker != null && !IsSamePlayer(@event.Attacker, victim))
        {
            return @event.Attacker;
        }

        var weapon = NormalizeWeaponName(@event.Weapon);
        if (string.IsNullOrWhiteSpace(weapon))
        {
            return null;
        }

        PruneRecentUtilityRecords();

        return ResolveUtilityDetonationAttacker(weapon, victim)
               ?? ResolveThrownUtilityImpactAttacker(weapon, victim);
    }

    private CCSPlayerController? ResolveUtilityDetonationAttacker(string weapon, CCSPlayerController victim)
    {
        var victimPosition = GetPlayerPosition(victim);
        if (victimPosition == null)
        {
            return null;
        }

        var window = IsFireDamageWeapon(weapon) ? FireAttributionSeconds : ExplosionAttributionSeconds;
        var radius = GetUtilityAttributionRadius(weapon);
        var now = Server.CurrentTime;

        var match = _recentUtilityDetonations
            .Where(item => item.Matches(weapon)
                           && now - item.Time <= window
                           && DistanceSquared(item.Position, victimPosition) <= radius * radius)
            .OrderBy(item => DistanceSquared(item.Position, victimPosition))
            .ThenByDescending(item => item.Time)
            .FirstOrDefault();

        if (match == null)
        {
            return null;
        }

        var attacker = FindPlayerByKey(match.ThrowerKey);
        if (!IsTrackablePlayer(attacker) || attacker == null || IsSamePlayer(attacker, victim))
        {
            return null;
        }

        return attacker;
    }

    private CCSPlayerController? ResolveThrownUtilityImpactAttacker(string weapon, CCSPlayerController victim)
    {
        if (!IsThrownUtilityImpactWeapon(weapon))
        {
            return null;
        }

        var now = Server.CurrentTime;
        var match = _recentThrownUtilities
            .Where(item => item.Matches(weapon) && now - item.Time <= ThrownImpactAttributionSeconds)
            .OrderByDescending(item => item.Time)
            .FirstOrDefault();
        if (match == null)
        {
            return null;
        }

        var attacker = FindPlayerByKey(match.ThrowerKey);
        if (!IsTrackablePlayer(attacker) || attacker == null || IsSamePlayer(attacker, victim))
        {
            return null;
        }

        return attacker;
    }

    private void RememberThrownUtility(CCSPlayerController thrower, string? weapon)
    {
        var normalizedWeapon = NormalizeWeaponName(weapon);
        if (string.IsNullOrWhiteSpace(normalizedWeapon))
        {
            return;
        }

        _recentThrownUtilities.Add(new ThrownUtility(GetPlayerKey(thrower), normalizedWeapon, Server.CurrentTime));
        PruneRecentUtilityRecords();
    }

    private void RememberUtilityDetonation(CCSPlayerController? thrower, string weapon, float x, float y, float z)
    {
        if (!IsTrackablePlayer(thrower) || thrower == null)
        {
            return;
        }

        _recentUtilityDetonations.Add(new UtilityDetonation(
            GetPlayerKey(thrower),
            NormalizeWeaponName(weapon),
            new Vector(x, y, z),
            Server.CurrentTime));
        PruneRecentUtilityRecords();
    }

    private void PruneRecentUtilityRecords()
    {
        var oldestAllowed = Server.CurrentTime - RecentUtilityKeepSeconds;
        _recentUtilityDetonations.RemoveAll(item => item.Time < oldestAllowed);
        _recentThrownUtilities.RemoveAll(item => item.Time < oldestAllowed);
    }

    private void AnnounceDifficultyOncePerMap()
    {
        if (_announcedDifficultyThisMap)
        {
            return;
        }

        var recipients = Utilities.GetPlayers()
            .Where(IsEligibleRecipient)
            .ToList();
        if (recipients.Count == 0)
        {
            return;
        }

        var message = BuildDifficultyMessage();
        foreach (var player in recipients)
        {
            PrintLbtvLine(player, message);
        }

        _announcedDifficultyThisMap = true;
    }

    private string BuildDifficultyMessage()
    {
        var difficulty = DetectDifficulty();
        return $"{LbtvPrefix} BOT Difficulty: {difficulty.Name} [{difficulty.Level}]";
    }

    private DifficultyResult DetectDifficulty()
    {
        var overridesDir = FindOverridesDirectory();
        if (overridesDir == null)
        {
            return new DifficultyResult("Unknown - overrides directory missing", "?/3");
        }

        var activePath = Path.Combine(overridesDir, "botprofile.vpk");
        if (!File.Exists(activePath))
        {
            return new DifficultyResult("Unknown - active botprofile.vpk missing", "?/3");
        }

        var activeHash = ComputeSha256(activePath);
        var knownProfiles = new[]
        {
            new DifficultyProfile("Low", "1/3", Path.Combine(overridesDir, "Low", "botprofile.vpk")),
            new DifficultyProfile("Medium", "2/3", Path.Combine(overridesDir, "Medium", "botprofile.vpk")),
            new DifficultyProfile("High", "3/3", Path.Combine(overridesDir, "High", "botprofile.vpk"))
        };

        foreach (var profile in knownProfiles)
        {
            if (!File.Exists(profile.Path))
            {
                continue;
            }

            if (CryptographicOperations.FixedTimeEquals(activeHash, ComputeSha256(profile.Path)))
            {
                return new DifficultyResult(profile.Name, profile.Level);
            }
        }

        return new DifficultyResult("Custom / Unknown", "?/3");
    }

    private static string? FindOverridesDirectory()
    {
        var candidates = new[]
        {
            Path.Combine(Server.GameDirectory, "overrides"),
            Path.Combine(Server.GameDirectory, "csgo", "overrides"),
            Path.Combine(Server.GameDirectory, "game", "csgo", "overrides")
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(Path.Combine(candidate, "botprofile.vpk")))
            {
                return candidate;
            }
        }

        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            var candidate = Path.Combine(current.FullName, "overrides");
            if (File.Exists(Path.Combine(candidate, "botprofile.vpk")))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return null;
    }

    private static byte[] ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return SHA256.HashData(stream);
    }

    private static Vector? GetPlayerPosition(CCSPlayerController player)
    {
        var pawn = player.PlayerPawn.Value;
        if (pawn == null || !pawn.IsValid)
        {
            return null;
        }

        return pawn.AbsOrigin;
    }

    private static double DistanceSquared(Vector left, Vector? right)
    {
        if (right == null)
        {
            return double.MaxValue;
        }

        var dx = left.X - right.X;
        var dy = left.Y - right.Y;
        var dz = left.Z - right.Z;
        return dx * dx + dy * dy + dz * dz;
    }

    private static string NormalizeWeaponName(string? weapon)
    {
        return (weapon ?? string.Empty)
            .Trim()
            .Replace("weapon_", string.Empty, StringComparison.OrdinalIgnoreCase)
            .ToLowerInvariant();
    }

    private static bool IsFireDamageWeapon(string weapon)
    {
        return weapon is "inferno" or "molotov" or "incgrenade" or "incendiarygrenade" or "fire";
    }

    private static bool IsThrownUtilityImpactWeapon(string weapon)
    {
        return weapon is "hegrenade"
            or "molotov"
            or "incgrenade"
            or "incendiarygrenade"
            or "smokegrenade"
            or "flashbang"
            or "decoy"
            or "tagrenade";
    }

    private static float GetUtilityAttributionRadius(string weapon)
    {
        if (IsFireDamageWeapon(weapon))
        {
            return FireAttributionRadius;
        }

        return weapon == "hegrenade" ? HeAttributionRadius : OtherUtilityAttributionRadius;
    }

    private void SnapshotAllPlayers()
    {
        foreach (var player in Utilities.GetPlayers().Where(IsTrackablePlayer))
        {
            SnapshotPlayer(player);
        }
    }

    private void SnapshotPlayer(CCSPlayerController player)
    {
        var key = GetPlayerKey(player);
        _playersByKey[key] = new PlayerSnapshot(player.PlayerName, player.Team);
    }

    private DamageEntry GetDamageBetween(CCSPlayerController attacker, CCSPlayerController victim)
    {
        var attackerKey = GetPlayerKey(attacker);
        var victimKey = GetPlayerKey(victim);

        if (_damageByAttacker.TryGetValue(attackerKey, out var victims)
            && victims.TryGetValue(victimKey, out var entry))
        {
            return entry;
        }

        return DamageEntry.Empty;
    }

    private int GetDisplayedHealth(CCSPlayerController enemy, DamageEntry toEntry)
    {
        if (enemy.PawnIsAlive && enemy.PlayerPawn.Value is { IsValid: true } pawn)
        {
            return Math.Max(0, pawn.Health);
        }

        if (toEntry.HitCount > 0)
        {
            return toEntry.LastKnownHealth;
        }

        return 0;
    }

    private void RemovePlayerStats(int key)
    {
        _damageByAttacker.Remove(key);

        foreach (var victimEntries in _damageByAttacker.Values)
        {
            victimEntries.Remove(key);
        }
    }

    private static int GetPlayerKey(CCSPlayerController player)
    {
        return player.UserId ?? player.Slot;
    }

    private static CCSPlayerController? FindPlayerByKey(int key)
    {
        return Utilities.GetPlayers().FirstOrDefault(player => player is { IsValid: true } && GetPlayerKey(player) == key);
    }

    private static bool IsSamePlayer(CCSPlayerController left, CCSPlayerController right)
    {
        return GetPlayerKey(left) == GetPlayerKey(right);
    }

    private static CsTeam? GetEnemyTeam(CsTeam team)
    {
        return team switch
        {
            CsTeam.CounterTerrorist => CsTeam.Terrorist,
            CsTeam.Terrorist => CsTeam.CounterTerrorist,
            _ => null
        };
    }

    private static bool IsTrackablePlayer(CCSPlayerController? player)
    {
        return player is { IsValid: true, IsHLTV: false }
               && player.Team is CsTeam.CounterTerrorist or CsTeam.Terrorist;
    }

    private static bool IsEligibleRecipient(CCSPlayerController? player)
    {
        return IsTrackablePlayer(player) && player is { IsBot: false };
    }

    private sealed class DamageEntry
    {
        public static readonly DamageEntry Empty = new();

        public string TargetName { get; set; } = string.Empty;
        public int TotalDamage { get; set; }
        public int HitCount { get; set; }
        public int LastKnownHealth { get; set; } = 100;
    }

    private sealed record PlayerSnapshot(string Name, CsTeam Team);

    private sealed record DifficultyProfile(string Name, string Level, string Path);

    private sealed record DifficultyResult(string Name, string Level);

    private sealed record UtilityDetonation(int ThrowerKey, string Weapon, Vector Position, float Time)
    {
        public bool Matches(string weapon)
        {
            return Weapon == weapon || IsEquivalentFireWeapon(Weapon, weapon);
        }
    }

    private sealed record ThrownUtility(int ThrowerKey, string Weapon, float Time)
    {
        public bool Matches(string weapon)
        {
            return Weapon == weapon || IsEquivalentFireWeapon(Weapon, weapon);
        }
    }

    private static bool IsEquivalentFireWeapon(string left, string right)
    {
        return IsFireDamageWeapon(left) && IsFireDamageWeapon(right);
    }
}
