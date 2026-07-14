# 2026-07-13 七月 CS2 兼容性修复整合研究记录

## 1. 记录目的

本文记录 2026 年 7 月 13 日在 `E:\LBTVCS2BotEnhancer` 开展的兼容性修复、构建、打包和独立测试服验证工作。

本次工作的原始目标是：保留仓库中已有的 BotVision、BotState、NadeSystem 投掷恢复/预算等本地改动，精确移植 7 月 9 日 CS2 更新后的 BotAimImprover 与 NadeSystem 上游修复，将 CounterStrikeSharp 升级到 1.0.371，并在验证通过后发布。

7 月 13 日的阶段性测试曾发现 MetaMod、RayTrace 和 BotAI 兼容性问题。7 月 14 日继续执行后，已用固定的 RayTrace 1.0.16、BotHider 0.3.0、CounterStrikeSharp 1.0.371 with-runtime 和 MetaMod 1406 完成修复，并在 Steam 最新 `buildid 24134959` 的独立服务器上重新验证。

> 2026-07-14 最终核心包已通过构建、静态审计、独立服 WithBots 启动和 Online 启动验证，可以发布；BotVision 因未纳入本轮最新服组合验证，正式包默认不启用。

## 2. 起点与保护边界

- 工作目录：`E:\LBTVCS2BotEnhancer`
- 分支：`main`
- 起点提交：`fb90e18b7e5d9d7702bc01a91ecde69be160d8c4`
- 起点 `origin/main`：`fb90e18b7e5d9d7702bc01a91ecde69be160d8c4`
- 未执行 `git reset`、`git checkout -- .` 或覆盖式同步上游。
- 保留了进入本次工作前已有的所有本地改动，包括：
  - `README.md`
  - BotAimImprover 身体优先级调整
  - BotState 修改
  - NadeSystem 投掷恢复、预算、配置、调试命令、声音优化和防重复投掷逻辑
  - BotVision 运行文件与源码快照
  - `Build-FullRelease.ps1`
  - 开发文档
- 未移植 Panel v1.4.2、`refreshAll(silent)` 或 localization 归档提交。

## 3. 已完成的源码兼容性移植

### 3.1 BotAimImprover

文件：

- `addons/counterstrikesharp/plugins/BotAimImprover/BotAimImprover.cs`
- `addons/counterstrikesharp/plugins/BotAimImprover/BotAimImprover.csproj`

已完成：

- 版本升级为 `2.1.3`。
- 作者保留原作者并追加 `XBribo`。
- 保留本地 GUT-first 身体优先级：
  - `GUT, PELVIS, CHEST`
  - 左右 GUT 位于左右 CHEST 之前。
- Windows 偏移更新为：
  - `m_targetSpot = 0x599C`
  - `m_enemy = 0x5A08`
  - `m_visibleParts = 0x5A0C`
  - `CCSBot = 0x12C0`
- Windows `PickNewAimSpot` 特征码更新为带通配符的新版本。
- CounterStrikeSharp API 升至 `1.0.371`。
- `RayTraceApi` 引用加入 `<Private>false</Private>`。

实际构建发现原交接方案存在一个依赖矛盾：`CounterStrikeSharp.API 1.0.371` 只支持 `net10.0`，BotAimImprover 若继续使用 `net8.0` 会产生 `NU1202`。因此 BotAimImprover 也必须改为 `net10.0`，不能只有 NadeSystem 使用 .NET 10。

构建结果：零警告、零错误。

### 3.2 NadeSystem

文件：

- `addons/counterstrikesharp/plugins/NadeSystem/NadeSystem.cs`
- `addons/counterstrikesharp/plugins/NadeSystem/NadeSystem.csproj`

已完成：

- 保留本地版本 `1.1.6`。
- 保留全部本地投掷恢复、预算、配置、调试命令、声音优化、同 tick 预留和防重复投掷逻辑。
- 仅替换 `_smokeCreate` 与 `_heCreate` 的 Windows/Linux 工厂签名。
- Target Framework 改为 `net10.0`。
- CounterStrikeSharp API 升至 `1.0.371`。

静态复核确认以下本地逻辑仍存在：

- `ThrowRecoveryState`
- `_roundUtilityBudgetByBot`
- `CanAffordUtilityThisRound(...)`
- `Server.NextFrame(() => SuppressBotAttack(bot))`
- 冻结结束时预算快照
- 投掷失败回滚
- 每 4 tick 记录脚步声

构建结果：零警告、零错误。

### 3.3 Competitive CFG

文件：`cfg/gamemode_competitive.cfg`

已完成：

- `sv_allow_annotations_access_level` 从 `0` 改为 `1`。
- 文件末尾仍保留 `exec my_bot_normal_config.cfg`。

### 3.4 README 行尾空白

仅移除了 BotVision 新增文案中的 5 处行尾空白，没有删除或改写 BotVision 内容。

当前 `git diff --check` 不再报告 trailing whitespace；输出中的 LF/CRLF 信息只是 Git 行尾转换警告。

## 4. 本机环境变化

本次工作前只有：

- .NET SDK `8.0.419`

本次安装：

- .NET SDK `10.0.301`

当前两个 SDK 并存，没有卸载 .NET 8。

另外为了定位 RayTrace 二进制路径字符串，临时安装了 radare2 6.1.8 到：

`C:\Users\GOPtZ\Tools\radare2\radare2-6.1.8-w64\`

IDA Pro 未安装，`ida-reverse` 后端无法启动；没有伪称 IDA 分析成功。

## 5. 固定的第三方运行时

### 5.1 CounterStrikeSharp 1.0.371

文件：

`vendor/counterstrikesharp/counterstrikesharp-windows-1.0.371.zip`

- 来源：CounterStrikeSharp 官方 GitHub Release v1.0.371
- 大小：`3,149,045` 字节
- SHA256：`23F3203A36AC407D6F66EF551FDD2C9DA456279074329577A3171EAAFA5BCF67`

该官方包不包含 `addons/counterstrikesharp/dotnet/`。打包脚本采用覆盖而非删除方式，因此旧整包底座中的 .NET 10 运行时仍保留。

最终 staging 中确认：

- `CounterStrikeSharp.API.deps.json` 包含 `CounterStrikeSharp.API/1.0.371`。
- `addons/counterstrikesharp/dotnet/host/fxr/10.0.3/hostfxr.dll` 存在。
- ZIP 内共有 344 个 `addons/counterstrikesharp/dotnet/` 条目。

### 5.2 MetaMod:Source 2.0 dev 1406

真实启动时发现旧整包底座中的 MetaMod `2.0.0-dev+1402` 无法识别当前 CS2 engine 26，日志为：

```text
MMS: Fatal error: Detected engine 26 but could not load
```

因此额外固定了官方构建：

`vendor/metamod/mmsource-2.0.0-git1406-windows.zip`

- 来源：AlliedModders 官方 `mmsdrop/2.0`
- 大小：`7,117,569` 字节
- SHA256：`E147D4CBE90BBD4BE3264CFFE2B028792165C38F49F77B21C7964BE0F117B131`
- `addons/metamod/bin/win64/server.dll` 产品版本：`2.0.0-dev+1406`

打包脚本现在会覆盖 MetaMod 二进制并校验版本，同时仍由仓库自己的 `addons/metamod/*.vdf` 覆盖回 staging。

注意：MetaMod 1406 解决了 engine 26 加载失败，但全量 VDF 启动仍出现后续 access violation，因此它是必要条件，不是完整解决方案。

## 6. 打包脚本改造

文件：`scripts/Build-FullRelease.ps1`

已完成：

- 每个 `$pluginOutputs` 条目显式声明 `Framework`。
- BotAimImprover 与 NadeSystem 使用 `net10.0`。
- 其余插件继续使用 `net8.0`。
- 展开旧整包后覆盖 CounterStrikeSharp 1.0.371。
- 校验 API deps 明确包含 `CounterStrikeSharp.API/1.0.371`。
- 保留旧底座内置 .NET 10 runtime。
- 覆盖 MetaMod 1406，并校验 `server.dll` 产品版本。
- 随后再复制仓库维护的 configs、VDF、BotVision 和插件构建产物。
- 保持 BotVision 源码与整个 `vendor/` 不进入玩家 ZIP。

## 7. 构建与静态打包结果

成功执行：

```powershell
dotnet build addons\counterstrikesharp\plugins\BotAimImprover\BotAimImprover.csproj -c Release
dotnet build addons\counterstrikesharp\plugins\NadeSystem\NadeSystem.csproj -c Release
powershell -ExecutionPolicy Bypass -File .\scripts\Build-FullRelease.ps1
```

完整构建没有错误。

已知警告：

- BotAI 有 10 个既存的 `CS0436` 类型重复警告。
- BotState 曾出现 `_isFreezeTime` 未使用警告；后续完整构建中未阻断产物。

最新研究 ZIP：

- 路径：`E:\LBTVCS2BotEnhancer\dist\LBTVCS2BotEnhancer.zip`
- 大小：`74,392,857` 字节
- 写入时间：`2026-07-13 15:41:48 +08:00`
- SHA256：`37D9215AB005D152C5ADD31412B3D20BAD0FC140D714DCA88E8DDC1B72CDD0A4`

已验证 ZIP 包含：

- `addons/BotVision/bin/win64/BotVision.dll`
- `addons/metamod/BotVision.vdf`
- `addons/counterstrikesharp/configs/plugins/NadeSystem/NadeSystem.json`
- `addons/counterstrikesharp/plugins/NadeSystem/NadeSystem.dll`
- `addons/counterstrikesharp/plugins/BotState/BotState.dll`
- `addons/counterstrikesharp/plugins/BotAimImprover/BotAimImprover.dll`
- CounterStrikeSharp API 1.0.371
- 内置 .NET 10 runtime

已验证 ZIP 不包含：

- `vendor/`
- `/bin/Release/`
- `/obj/`
- `.git/`
- `node_modules/`
- BotVision 源码快照

## 8. 独立测试服

为避免污染正式游戏目录，创建了完整独立副本：

`E:\LBTVCS2BotEnhancer-test-server`

来源：

`D:\SteamLibrary\steamapps\common\Counter-Strike Global Offensive\game`

复制结果：

- 目录：1,242
- 文件：16,740
- 字节：68.865 GB
- 失败：0

正式游戏目录在整个测试过程中未被覆盖。

测试端口：`27025/UDP`

收工时已终止所有位于独立测试目录下的 `cs2.exe`，不存在残留测试服进程。

## 9. 真实运行测试结果

### 9.1 MetaMod 1402

结果：失败。

服务器本体可以进入地图加载流程，但 MetaMod 报 engine 26 无法加载，CounterStrikeSharp 和所有插件均未启动。

### 9.2 MetaMod 1406，全量 VDF

结果：失败。

MetaMod 的 engine 26 错误消失，但进程产生新的 access violation minidump。研究期间产生的最新 dump 位于：

`E:\LBTVCS2BotEnhancer-test-server\game\bin\win64\cs2_2026_0713_*_accessviolation.mdmp`

这证明旧 MetaMod 是一个问题，但不是唯一问题。

### 9.3 MetaMod 1406，不加载任何 VDF

结果：稳定。

服务器进程存活，内存约 814 MB，没有新增 dump。由此确认 MetaMod 1406 本体能够加载当前 engine 26；崩溃来自后续 MetaMod 插件链。

### 9.4 只恢复 CounterStrikeSharp VDF

结果：CounterStrikeSharp 1.0.371 与 .NET 10 成功启动。

关键日志：

```text
Loading hostfxr ... dotnet\host\fxr\10.0.3\hostfxr.dll
.NET Runtime Initialised.
CounterStrikeSharp.API Loaded Successfully.
```

### 9.5 BotAI 结论

BotAI 不是本次初始 access violation 的直接根因。完整日志证明它最终能够完成加载：

```text
Applied 26/40 patches.
Finished loading plugin Patches - Bot AI
```

但 7 月更新后仍有大量失效项，包括：

- `AttackState_SkipSniperSpreadCheck` 字节不匹配
- `AttackState_DodgeChance100_Always` 找不到签名
- `AttackState_RetreatOnSniper_Disable` 找不到签名
- 多个 Vision、Bomb 和 Flashbang 相关 patch 找不到签名
- `OnAudibleEvent_GlobalHearRange` 字节不匹配

因此 BotAI 当前只能应用 26/40 patch，不能视为完整兼容。上游截至 `9fd1bd6` 没有发布这些 BotAI patch 的修复。

### 9.6 关闭 BotAI 后的 C# 插件加载

为排除 BotAI，曾在测试副本中临时移动整个 BotAI 插件目录。日志确认以下插件均成功完成加载：

- BotAimImprover
- BotBuy
- BotHiderImpl
- BotRandomizer
- BotState（日志中的模块名为 `Smarter-Bot`）
- BotTaunt
- MapRotation
- NadeSystem
- RayTraceImpl（托管层）
- RoundDamageRecap

BotAimImprover 的关键日志：

```text
[BotAimImprover] Loaded (Windows). PickNewAimSpot=0x...
Finished loading plugin BotAimImprover
```

NadeSystem 的关键日志：

```text
Loaded 3703 grenades ...\NadeSystem\grenades
Finished loading plugin NadeSystem
```

这证明本次 BotAim Windows 偏移/签名能够解析，NadeSystem 构造期没有出现 native signature resolve 失败。

### 9.7 RayTrace 阻断问题

当前旧整包携带：

- `addons/RayTrace/bin/win64/RayTrace.dll`
- 大小：`817,152` 字节
- 原始 SHA256：`41B2C74A3B6D428EA110EBEE25519C7893C9CDFCFDB16E6499279DEBA185A62F`
- `addons/RayTrace/gamedata.json`
- `addons/metamod/RayTrace.vdf`
- 托管 `RayTraceImpl`

MetaMod 1406 尝试加载原生 RayTrace 时失败：

```text
RayTrace: Could not read '/addons/RayTrace/gamedata.json'. Error: Gamedata file not found.
[META] Failed to load plugin addons/RayTrace/bin/win64/RayTrace
```

磁盘上的 gamedata 文件实际存在，说明失败不是打包遗漏，而是 RayTrace 原生插件的路径处理与当前运行环境/MetaMod 1406 不兼容。

原生 RayTrace 未加载后，托管层继续初始化并抛出：

```text
System.InvalidOperationException: Nullable object must have a value.
at RayTraceImpl.NativeBridge.Initialize()
at RayTraceImpl.RayTraceImpl.OnMetamodAllPluginsLoaded()
```

使用 radare2 定位到 `RayTrace.dll` 内两个相关字符串：

- `/addons/RayTrace`，文件偏移 `0x78178`，VA `0x180079178`
- `/gamedata.json`，文件偏移 `0x782E8`，VA `0x1800792E8`

曾在测试副本中把 `/addons/RayTrace` 临时改为 `addons/RayTrace`，补丁后 SHA256 为：

`D29B0F87931956A825CD80ECB554E06BD904CF35A4AD9115C8D50F4D9BFD00D2`

测试结果：

- 从 `game/bin/win64` 作为工作目录启动时，仍报告相对路径找不到文件。
- 从 `game/csgo` 作为工作目录启动时，服务器能够监听 `27025/UDP`，但 CounterStrikeSharp 日志仍记录 RayTrace 托管桥异常。
- 因此该字符串补丁不足以证明 RayTrace 完整恢复，不能纳入发布。

收工时已经恢复测试副本中的原始 RayTrace.dll，恢复后 SHA256 为：

`41B2C74A3B6D428EA110EBEE25519C7893C9CDFCFDB16E6499279DEBA185A62F`

### 9.8 BotHider 与 BotVision

BotHider、BotVision VDF 在隔离过程中曾临时禁用。由于研究在 RayTrace 阶段停止，尚未完成以下验证：

- MetaMod 1406 + CounterStrikeSharp + RayTrace 修复后的 BotHider 单独启动
- BotVision 单独启动
- BotHider + BotVision 同时启动
- `bv_status`
- volumetric smoke 实测

初次全量 VDF 启动曾产生 access violation，因此这两项仍必须在 RayTrace 解决后重新二分验证，不能直接认定安全。

## 10. 尚未完成的玩法验证

本次没有达到以下验收门槛：

- `bot_aim head/body/mixed` 的完整实战验证
- 烟雾阻挡与 BotVision volumetric smoke 验证
- 闪光、烟、HE、燃烧弹实际投掷
- NadeSystem 投掷恢复时间窗验证
- NadeSystem 预算约束验证
- 无插件卸载、无启动崩溃的长时间运行验证

原因是 RayTrace 原生桥仍未恢复，全量 VDF 组合也尚未完成稳定性验证。

## 11. 收工清理状态

已完成：

- 终止独立测试目录下的全部 `cs2.exe`。
- 恢复测试副本原始 RayTrace.dll。
- 删除临时 RayTrace binary backup。
- 将临时移出的 BotAI 目录恢复到插件目录。
- 将以下 VDF 全部恢复：
  - `BotHider.linux.vdf`
  - `BotHider.vdf`
  - `BotVision.vdf`
  - `counterstrikesharp.vdf`
  - `RayTrace.vdf`
- 删除已清空的临时隔离目录。

保留：

- 独立测试服目录及其日志/dump，便于后续研究。
- 当前工作区源码改动。
- 最新研究 ZIP。
- 固定的 CounterStrikeSharp 与 MetaMod 官方归档。

## 12. 当前 Git 与发布状态

- 未执行 `git add`。
- 未执行 `git commit`。
- 未执行 `git push`。
- 未创建 tag。
- 未创建 GitHub Release。
- `gh auth status` 显示当前未登录 GitHub CLI。

严禁把当前研究 ZIP 作为已验证 Release 上传。

当前改动仍包括进入本次工作前的本地改动和本次兼容性研究改动。继续工作时必须先执行：

```powershell
git status --short
git diff --check
dotnet --list-sdks
```

## 13. 下一次研究的推荐顺序

1. 获取或自行构建支持当前 CS2/MetaMod 1406 的正式 RayTrace 原生插件与对应托管桥。
2. 不要直接发布今天的临时字符串 patch；应优先取得上游源码或官方兼容构建。
3. 在独立测试服按以下顺序重新二分：
   - MetaMod 1406
   - RayTrace 原生
   - CounterStrikeSharp 1.0.371 + RayTraceImpl
   - BotAimImprover + NadeSystem
   - BotHider
   - BotVision
   - BotAI
4. 对 BotAI 40 个 patch 逐项更新签名/预期字节；至少解决当前 14 个失败项。
5. 确认所有插件加载后，再执行 bot_aim、烟雾、闪光/烟/HE/燃烧弹、投掷恢复和预算测试。
6. 连续运行多轮，确认没有 access violation、native signature resolve failure 或插件卸载。
7. 重新运行完整打包脚本并重新计算 ZIP SHA256；今天的 SHA256 在任何后续改动后都会失效。
8. 只有所有门槛通过后，才允许提交、推送、打 tag 和创建 Release。

## 14. 7 月 13 日阶段性结论

今天完成了七月 BotAim/NadeSystem 源码修复、API 1.0.371、.NET 10、MetaMod engine 26 适配、构建脚本和 ZIP 静态验证，并通过独立测试服证明 BotAim 与 NadeSystem 可以完成 C# 插件加载。

同时，真实运行揭示了原方案未覆盖的三个发布阻断项：

1. MetaMod 1402 不支持 engine 26，必须升级。
2. RayTrace 原生插件无法在当前组合下读取 gamedata，导致托管桥异常。
3. BotAI 仅成功应用 26/40 patch，仍有大量七月更新后的失效签名/字节。

该结论仅描述 7 月 13 日旧 RayTrace、旧 BotHider 和未刷新 BotAI 签名时的阶段状态，已被下面的 7 月 14 日最终验证取代。

## 15. 2026-07-14 最终验证

- Steam 清单：`appmanifest_730.acf`，`buildid 24134959`。
- 最新服务器二进制：`game/csgo/bin/win64/server.dll` SHA256 `BDB81200C1FE8D2DB104210F041EAE2360A6432AC804BD8611C338A507B4A8D2`。
- 独立测试服先用 `robocopy /MIR` 与 Steam 当前 `game` 目录同步，再删除测试服第三方 `addons/overrides/backup`，只部署最终 staging。
- WithBots：进程稳定，UDP `27025` 监听；BotHider 4 个关键签名/Hook 唯一解析，1409 条身份加载；RayTraceImpl 输出 `Managed side initialized`；正式包内 11 个 CSS 插件全部完成加载。
- BotAI：PR #75 的 19 处 Windows 更新已合入；当前已不存在的 3 个旧补丁点明确禁用，其余输出 `Applied 37/37 active patches`，无 active signature/byte mismatch。
- Online：派生文件不含 `addons/metamod` 与 `overrides/botprofile.vpk`，使用 UTF-8 无 BOM；独立服 UDP `27026` 正常监听，未加载 CounterStrikeSharp/BotHider/RayTrace。
- 助手壳伪 CS2 根目录测试：已有 `addons/counterstrikesharp/configs/plugins` 配置保持原内容，新运行时、backup 和 VPK 正确安装，Online/WithBots 两次切换均与对应备份逐字节一致。
- 助手验证：`npm run verify` 通过，4 个测试文件、12 个测试全部通过；`npm run bundle:desktop` 成功，版本保持 `0.4.5`。
- 已知非阻断日志：CS2 会对 `shared/BotHiderApi.dll` 与 `shared/RayTraceApi.dll` 做原生 preload 探测并记录 caught access violation；进程持续稳定、端口正常、托管插件已加载，且没有产生新的 `.mdmp`。
- BotVision：源码和测试开关保留，正式 ZIP 不包含其 DLL/VDF；只有显式使用 `Build-FullRelease.ps1 -IncludeBotVision` 才进入测试包。

最终交付：

- `dist/LBTVCS2BotEnhancer.zip`，SHA256 `E7C93EFBB2619A85565FF1E76FAD449D508A8DF167C50C995BE835F52D0BDFA7`。
- `dist/CS2人机增强助手_0.4.5_x64-setup.exe`，SHA256 `8D89FD31D484E90AE754C3D207F686B42297CA9B4E2391D918906E75915F0BC4`。
