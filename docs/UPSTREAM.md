# Upstream Policy

## Base

- Project: `numakkiyu/CS2-Bot-Improver-Plus`
- Plus URL: https://github.com/numakkiyu/CS2-Bot-Improver-Plus
- Original upstream URL: https://github.com/ed0ard/CS2-Bot-Improver
- Pinned source commit: `055df1b617cf1aa5ff45911bcff9fd05ab2f48aa`
- Packaged runtime base: `v1.4.2.1`

The working tree is synchronized from the pinned Plus source archive. The package script obtains the matching Plus
runtime layout, then overlays rebuilt source outputs and the pinned engine-compatible runtimes. The only local gameplay
delta is NadeSystem's freeze-end utility budget and per-bot grenade quota gate.

## Pinned Runtime Inputs

The machine-readable source of truth is `scripts/dependencies.json`.

- `CS2BotImproverPlus-v1.4.2.1-windows.zip` supplies the official Windows runtime layout.
- MetaMod 2.0.0-git1406 supplies the engine 26 loader.
- CounterStrikeSharp v1.0.371 with its bundled .NET runtime replaces the stale v1.4.1 copy.
- RayTrace v1.0.16 supplies both the native module and CounterStrikeSharp API/implementation.
- `BotHider-windows-0.3.0.zip` supplies the native BotHider module.
- BotAI includes the tested Windows signature refresh from upstream PR #75 (`3db93ba`).
- BotAI, BotAimImprover, BotBuy, and NadeSystem are rebuilt from the pinned source tree; NadeSystem alone carries the
  documented utility-budget and quota patch.
- BotAimImprover and NadeSystem receive `RayTraceApi.dll` from the verified v1.0.16 archive through an explicit
  MSBuild property; clean builds do not depend on an ignored `libs` file left on the developer machine.
- Plus-built `BotHiderImpl`, `BotHiderApi`, and `PlayerKnifeCustomizer` assemblies overlay their upstream locations.
- The Plus Panel replaces the upstream Panel executable while retaining the same standalone workflow.

Every downloaded archive and each critical runtime DLL is SHA-256 verified before packaging.

## Synchronizing

1. Fetch `upstream/main` and inspect release notes, issues, and relevant PRs.
2. Rebase or merge in an isolated branch.
3. Preserve Plus-only modules and Panel routes.
4. Reconcile I18N by keeping the entire new upstream key/dictionary set, then reapply Plus keys and translations.
5. Refresh catalogs only from a traceable source and verify locale and entry counts.
6. Build all three targets and create a disposable package before updating the pinned manifest.

## Third-Party Data

Weapon images and localized skin names are derived from `Nereziel/cs2-WeaponPaints`. Indonesian currently uses the
English fallback because that source does not provide an Indonesian skin-name table. This fallback affects display
only; item application uses numeric catalog identifiers.

BotHider is maintained at `XBribo/CS2-Bot-Hider`. The package tracks v0.3.0, which supplies the current Windows
identity synchronization, team-join scope, entity-packing protection, and gamedata-driven
`CServerSideClient::SetName` target. Packaging verifies the official release archive and native DLL hashes without
binary patching.

`BotHiderImpl` supplements native name publication only for slots reported by BotHider as managed bots, through
`CBasePlayerController.m_iszPlayerName`, and forces the Plus `bot_info.json` name source before bots are created. It
does not write names for human-player slots. Steam IDs, avatars, cards,
crosshair codes, ping, scoreboard flair, bot disguise, respawn behavior, and every upstream enhanced-bot module stay
on their existing paths. The repository can verify this isolation and the package layout automatically, but the final
host-local scoreboard result still requires an in-game Enhanced Bots practice match.
