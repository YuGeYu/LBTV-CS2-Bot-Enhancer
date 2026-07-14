namespace BotHiderApi;
// Slot is the engine player slot (CCSPlayerController.Slot.Value)
public interface IBotHiderApi
{
    bool IsManagedBot(int slot);

    ulong GetBotSteamId(int slot);

    int[] GetManagedSlots();

    string GetPersonaName(int slot);

    int GetPing(int slot);

    string GetCrosshairCode(int slot);

    uint GetScoreboardFlair(int slot);

    (string Name, ulong Addr)[] GetSignatures();

    // returns false if the slot is out of range.
    bool SetBotSteamId(int slot, ulong steamId64);

    bool SetCrosshairCode(int slot, string code);

    // returns false if the slot/name is invalid.
    bool SetPersonaName(int slot, string name);

    bool SetScoreboardFlair(int slot, uint itemDefIndex);

    // Global disguise toggle; off lets the bot manager spawn bots on aim_*/practice maps
    bool SetDisguise(bool enabled);

    // Display-name source toggle; true=bot_info.json name, false=botprofile name (affects newly created bots)
    bool SetNameSource(bool useBotInfo);
}
