# CS2-Bot-Improver
CS2-Bot-Improver is a plugin for Counter-Strike 2 that improves bots' aim, movement, nade throwing, personalities, strategies, etc.

Aims to enhance your experience when playing against bots offline or with friends. It can be installed on both clients and servers.

## Your stars⭐ are my motivation to keep updating

## Features

1. Makes bots aim better and more human-like
2. Allows bots to throw nades deftly according to the situation
3. Improves bots' movement
4. Fixes most bot stuck issues
5. Allows bots to buy everything and overhauls their economy management
6. Refines bot behavior, allowing them to spray, flick, spam smokes and anti-flash
7. Assigns each bot their own knife, gloves, weapon skins, agent model, music kit, avatar, and profile
8. Makes bots smarter, more organized, and more alert to their surroundings
9. Changes bot names to pro and random players. (the characteristics of each pro player are based on stats from [HLTV](https://www.hltv.org/))
10. Removes the prefix from bot names
11. Tweaks game rules to make them more friendly to bots
12. Adds some commands to make the game more fun
13. Includes an optional BotVision integration for dedicated smoke-visibility testing

## Installation

### Windows

1. Download the latest **CS2BotImprover.zip** in [Releases](https://github.com/ed0ard/CS2-Bot-Improver/releases) and unzip it

   (If you run a dedicated server that is not only for bot matches, please download **CS2BotImprover_rules_unchanged.zip**)

2. Put **Panel v1.4.0.exe** anywhere convenient

<img width="128" height="128" alt="App" src="https://github.com/user-attachments/assets/7271dc7d-2436-484b-8359-6531f4abd710" />

3. Open the root of CS2 and navigate to `game/csgo` directory

<img width="405" height="256" alt="snap_1" src="https://github.com/user-attachments/assets/ae2be90e-6742-4f1f-8e0c-096b728d5dbd" />

3. Copy all the remaining files in `CS2BotImprover` and paste them into `game/csgo`

<img width="540" height="181" alt="snap_windows" src="https://github.com/user-attachments/assets/6a8645fc-78e7-4f3a-92d3-5d1b6d913918" />

4. Open **Panel v1.4.0.exe**, select **Bot Mode**, then click **Launch CS2** 

<img width="339" height="129" alt="Panel_1" src="https://github.com/user-attachments/assets/dc806991-c940-43cf-a614-f49012fae4a7" />


### Linux

1. Download the latest **CS2BotImprover_for_Linux.zip** in [Releases](https://github.com/ed0ard/CS2-Bot-Improver/releases) and unzip it

2. Put **Command.txt** anywhere convenient

3. Open the root of CS2 and navigate to `game/csgo` directory

<img width="405" height="256" alt="snap_1" src="https://github.com/user-attachments/assets/ae2be90e-6742-4f1f-8e0c-096b728d5dbd" />

4. Copy all the remaining files in `CS2BotImprover` and paste them into `game/csgo`

<img width="535" height="180" alt="snap_linux" src="https://github.com/user-attachments/assets/9bda7b1d-43d3-49cf-a283-27b124b894e0" />

5. Add `-insecure` in launch options

<img width="130" height="153" alt="snap_3" src="https://github.com/user-attachments/assets/4c775e36-3fc3-4a19-9cb1-4f0c9327838c" /><br>
<img width="625" height="423" alt="snap_4" src="https://github.com/user-attachments/assets/ac0b0c57-ee67-4e33-96fb-146d14714fc8" />

## Commands

### Aim

`bot_aim mixed`  
Bots select aiming spots flexibly based on situations (default)

`bot_aim head`  
Bots prioritize aiming at the head

`bot_aim body`  
Bots prioritize aiming at the torso

`bot_aim`  
Check the current aim mode

### Nades

`bot_nades off`  
Bots won't throw any nades

`bot_nades normal`  
Bots follow almost the same count limits as human players (default)

`bot_nades more`  
Bots use the same decision logic as normal mode with higher count limits

`bot_nades max`  
Bots have minimal limitations and think less before throwing nades

`bot_nades`  
Shows the current nade throwing mode

### Bot Taunt / AI Chat

`lbtv_bot_taunt 0/1`  
Disable or enable LBTV bot taunts.

`lbtv_bot_chat 0/1`  
Disable or enable AI chat replies. The AI chat API key is intentionally blank in the public package; fill it in `addons/counterstrikesharp/configs/plugins/BotTaunt/BotTaunt.json` before using AI replies.

`lbtv_bot_chat_reload`  
Reloads both `addons/counterstrikesharp/configs/plugins/BotTaunt/BotTaunt.json` and `addons/counterstrikesharp/configs/plugins/BotTaunt/Taunts.json` without restarting the server.

`lbtv_bot_chat_diag`  
Shows BotTaunt AI chat diagnostics.

`lbtv_bot_chat_saytest <message>`  
Makes a bot send a native say message for quick chat-path testing.

`lbtv_bot_chat_test <message>`  
Sends a test message to the configured AI endpoint.

`lbtv_bot_rivalry 0/1`  
Disable or enable low-frequency bot-vs-bot taunts. It is off by default and can be enabled with `lbtv_bot_rivalry 1`.

Editable taunt text is stored in `addons/counterstrikesharp/configs/plugins/BotTaunt/Taunts.json`.

### Round Damage / Difficulty

`lbtv_difficulty`  
Shows the active bot difficulty profile using the current `overrides/botprofile.vpk` hash. The RoundDamageRecap plugin also prints round-end damage dealt/taken lines and attributes HE, molotov, incendiary, inferno, and thrown utility damage back to the correct thrower where CS2 does not provide a direct attacker.

### BotVision

BotVision source/runtime inputs remain in the repository for dedicated-server testing. Production packages exclude its VDF by default until the current CS2 build passes the staged native-plugin verification. Build a test package with `scripts/Build-FullRelease.ps1 -IncludeBotVision`.

`bv_status`
Prints hook hit/blocked counts and resolved signature status.

`bv_smoke_mode <0|1>`
Sets smoke handling. `0` uses volume-smoke blocking, and `1` uses stock ball-smoke behavior.

`bv_density_threshold <d>`
Sets the volume-smoke blocking threshold. The upstream default is `0.2`.

`bv_test_los <x1> <y1> <z1> <x2> <y2> <z2>`
Tests smoke density along a segment.

### Map Rotation

Map rotation is off by default. Run `lbtv_map_rotation 1` to enable the fixed LBTV map rotation, and run `lbtv_map_rotation 0` to disable it again.

`lbtv_map_rotation`  
Shows whether rotation is enabled, the current map, and the next map.

`lbtv_map_next`  
Immediately changes to the next map in the rotation.

Current rotation order: `de_anubis -> de_overpass -> de_inferno -> de_mirage -> de_dust2 -> de_nuke -> de_ancient -> de_train -> de_vertigo -> de_cache`.

### Buy

Input the weapon's name in your console to give every bot this weapon from the next round

The valid names of weapons:  
`elite`  
`p250`  
`fn57`  
`deagle`  
`cz75a`  
`r8`  
`bizon`  
`p90`  
`mp5sd`  
`mp9`  
`mp7`  
`mac10`  
`ump45`  
`mag7`  
`sawedoff`  
`nova`  
`xm1014`  
`famas`  
`galilar`  
`m4a1`  
`m4a1s`  
`ak47`  
`aug`  
`sg556`  
`ssg08`  
`awp`  
`scar20`  
`g3sg1`  
`negev`  
`m249`

`bot_buy`  
Bot would buy as usual

### Teams

To add pro teams to your match, copy from [Commands.txt](https://github.com/ed0ard/CS2-Bot-Improver/blob/main/Commands.txt) and paste them to your game console. You can also add new teams in this format.

For example, if you wanna add Vit to CT, copy the commands below.

<img width="301" height="237" alt="snap_5" src="https://github.com/user-attachments/assets/a895f3a6-58f8-47dc-b6f5-b60c1b32fecd" />

### Knives

Point at the ground and press `\` on your keyboard to spawn knives using the current LBTV knife mode. This is implemented by cfg aliases in the four bot configs, not by a C# plugin command.

`lbtv_knife_hot`  
Sets `\` to spawn five popular knives: Karambit, Butterfly, Talon, M9 Bayonet, and Bayonet.

`lbtv_knife_rdm`  
Sets `\` to spawn one rotating set of five knives. Run `lbtv_knife_rdm` again to switch to the next set.

`lbtv_knife_all`  
Sets `\` to spawn all knife types.

`lbtv_knife_spawn` is the internal alias bound to `\` by `cfg/my_bot_normal_config*.cfg` and `cfg/my_bot_ffa_config*.cfg`.

### Flying Scoutsman

`scouts_on`  
`scouts_off`  
Input the command after a match begins to turn on/off Flying Scoutsman

## Panel Guide (Windows-Only)

### Status Lights
🟢 No issues detected  
🟡 Restart CS2 to apply changes  
🔴 Files missing. Click the red light to view the list of missing files  

<img width="481" height="82" alt="Status Lights" src="https://github.com/user-attachments/assets/26a947e2-4e0e-423f-bce8-f220d88509a2" />

### Matchmaking & Bot Mode Toggle
Select your desired mode, then click `Launch CS2`

<img width="472" height="179" alt="Mode_2" src="https://github.com/user-attachments/assets/3f9254fa-4cbe-4854-8fd1-0f35228fff77" />

### Settings
Click the <img width="31" height="32" alt="Settings" src="https://github.com/user-attachments/assets/7f94176b-79f1-4e22-9495-4589c4dea9eb" /> icon in the top-right corner to open `Settings`

### Commands
Click `Commands`, type keywords to search, then click a block to auto-copy

<img width="350" height="420" alt="Screenshot 2026-06-14 090901" src="https://github.com/user-attachments/assets/957cfafb-900d-4450-b985-13d3e8efc375" />

## FAQ

### How to manually change the difficulty level

1. Open the root of CS2 and navigate to `game/csgo/overrides` directory  
2. Open the `Low` for easy difficulty, `Medium` for a mixed difficulty based on HLTV stats (default), and `High` for extreme difficulty  
3. Copy `botprofile.vpk` and paste it into `game/csgo/overrides` before launching the game

### How to manually switch to normal online match mode

1. Open the root of CS2 and navigate to `game/csgo/backup/Online` directory  
2. Copy `gameinfo.gi` and paste it to `game/csgo` directory (Replace the file in the destination)  
3. Delete `-insecure` in your launch options  

After modification, if you wanna **play with bots again**, navigate to `game/csgo/backup/WithBots` directory, replace the file as above and add the launch option

### How to manually disable bot weapon skins, agent skins, music kits, knives and gloves

1. Open the root of CS2 and navigate to `game/csgo/addons/counterstrikesharp/plugins`  
2. Rename the `BotRandomizer` folder to `BotRandomizer_disabled`  
3. Navigate to `addons/counterstrikesharp/configs/core.json` and set `FollowCS2ServerGuidelines` to `true`

### How to manually disable bot steam profiles

1. Open the root of CS2 and navigate to `game/csgo/addons`  
2. Rename the `BotHider` folder to `BotHider_disabled`  

### How to play bot matches with friends

1. Start a bot match and input the required commands. Then type `status` in the console  
<img width="597" height="141" alt="snap_6" src="https://github.com/user-attachments/assets/792c4b4f-1d56-4a39-9186-b301cbff1846" />

2. Copy the text after `steamid:`, add `connect ` before it (don’t forget the space between them)  
3. Send the full command to your friends and have them paste it into their consoles

### How to run the plugin well on workshop maps

Add `-disable_workshop_command_filtering` to your launch options

### How to surf normally

Run `sv_standable_normal 0.7` in your game console

## Credits
[metamod-source](https://github.com/alliedmodders/metamod-source)  
[CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp)  
[Ray-Trace](https://github.com/FUNPLAY-pro-CS2/Ray-Trace)  
[CS2-Bullseye-Bot](https://github.com/ed0ard/CS2-Bullseye-Bot)  
[CS2-Bot-NadeSystem](https://github.com/ed0ard/CS2-Bot-NadeSystem)  
[CS2_ExecAfter_No_Admin](https://github.com/ed0ard/CS2_ExecAfter_No_Admin) forked from [kus](https://github.com/kus)  
[CS2-Bot-Randomizer](https://github.com/ed0ard/CS2-Bot-Randomizer)  
[CS2-Bot-Hider](https://github.com/XBribo/CS2-Bot-Hider) by [XBribo](https://github.com/XBribo)  
[CS2-Bot-Vision](https://github.com/XBribo/CS2-Bot-Vision) by [XBribo](https://github.com/XBribo)
[CSGOBetterBots](https://github.com/manicogaming/CSGOBetterBots/blob/master/addons/sourcemod/data/bot_info.json) by [manico](https://github.com/manicogaming)  
[CS2-Smarter-Bot](https://github.com/ed0ard/CS2-Smarter-Bot)  
[CS2-BotAI](https://github.com/ed0ard/CS2-BotAI) forked from [Austin](https://github.com/Austinbots)  
[CS2-BotAI-for-Linux](https://github.com/Austinbots/CS2-BotAI)  
[CS2-Bot-Buy](https://github.com/ed0ard/CS2-Bot-Buy)  
[RoundDamageRecap](https://github.com/YuGeYu/LBTV-CS2-Bot-Enhancer/tree/main/addons/counterstrikesharp/plugins/RoundDamageRecap) by [YuGeYu](https://github.com/YuGeYu)  
[Apple-Style-GUI](https://github.com/ed0ard/Apple-Style-GUI)  

## License
GPL-3.0

## Development

Maintainer notes for rebuilding the full LBTV package from the upstream package base are in [docs/DEVELOPMENT_zh-CN.md](docs/DEVELOPMENT_zh-CN.md).
