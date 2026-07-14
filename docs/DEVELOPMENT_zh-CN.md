# 开发注意事项

本文档记录本仓库的维护边界和整包发布流程。重点参考 `E:\CS2-Bot-Improver_fresh` 的做法：不要从空目录重新拼完整游戏包，而是以作者发布的老整包为底座，展开后覆盖本仓库维护的插件、配置、面板、难度档和说明文件，最后重新压缩成我们的新整包。

## 仓库定位

本仓库是 `CS2-Bot-Improver` 的整包增强维护树，根目录布局基本对应玩家最终复制到 `game/csgo` 的目录结构：

- `addons/`：MetaMod、CounterStrikeSharp 插件源码、运行配置和依赖。
- `cfg/`：机器人模式、游戏模式和本项目调整过的 cfg。
- `overrides/`：不同难度/瞄准档的 `botprofile.db` 和本地化资源，发布前需要重新打进 override VPK。
- `Panel/`：Windows 面板源码和相关前端资源。
- `Commands.txt`、`README.md`：玩家向命令和安装说明，发布包内应使用本仓库版本。
- `vendor/upstream/CS2BotImprover_upstream_latest.zip`：上游/旧整包底座。它提供完整第三方运行时、目录骨架、备份文件和不在本仓库重新维护的资源。
- `vendor/counterstrikesharp/counterstrikesharp-with-runtime-windows-1.0.371.zip`：固定的 CounterStrikeSharp 1.0.371 Windows 完整运行时覆盖包。
- `vendor/metamod/mmsource-2.0.0-git1406-windows.zip`：支持当前 CS2 engine 26 的 MetaMod:Source 2.0 dev 1406 Windows 运行时覆盖包。
- `vendor/botvision/`：BotVision 上游二进制和源码快照。源码只留在仓库，不进入玩家整包。

## 核心原则

1. 新整包不是直接压缩本仓库根目录。
2. 新整包应先展开旧整包，再将本仓库的改动覆盖进去。
3. 第三方运行时和上游未改动文件优先来自旧整包，避免漏掉 CounterStrikeSharp、MetaMod、Panel 运行依赖、备份目录等完整资产。
4. 本仓库维护的源码、配置、难度档、说明文件必须覆盖旧整包中的同名内容。
5. 发布前要检查压缩包内容，而不是只看构建命令是否成功。
6. `vendor/botvision/source/CS2-Bot-Vision-v0.1.1-alpha/` 是上游源码快照，保持原样；玩家运行资产来自 `addons/BotVision/`。
7. BotVision 的运行 VDF 必须独立命名为 `addons/metamod/BotVision.vdf`，不要使用上游源码快照里的 `BotHider.vdf`，避免和本项目已有 BotHider 冲突。
8. 展开旧整包后，先将 CounterStrikeSharp 1.0.371 覆盖包写入 staging；覆盖操作不得删除旧底座的 `addons/counterstrikesharp/dotnet/` 内置 .NET 10 运行时，本仓库的 `configs/plugins/` 随后再覆盖回 staging。
9. 旧整包中的 MetaMod:Source 2.0 dev 1402 无法加载当前 engine 26；必须用固定的 dev 1406 包覆盖 `addons/metamod/bin/`，同时保留本仓库维护的 BotHider、CounterStrikeSharp 和 RayTrace VDF。BotVision 只在 `-IncludeBotVision` 测试包中启用。

## 由老整包打出新整包

建议后续补齐 `scripts/Build-FullRelease.ps1`，流程与 `E:\CS2-Bot-Improver_fresh\scripts\Build-FullRelease.ps1` 保持一致。手动执行或写脚本时按下面顺序做。

### 1. 准备旧整包底座

默认旧整包路径：

```powershell
E:\LBTVCS2BotEnhancer\vendor\upstream\CS2BotImprover_upstream_latest.zip
```

如果拿到新的作者整包，先替换这个文件，并保留一份带时间戳的备份，例如：

```powershell
Copy-Item vendor\upstream\CS2BotImprover_upstream_latest.zip vendor\upstream\CS2BotImprover_upstream_latest.zip.bak_20260614_1430
Copy-Item C:\path\to\new\CS2BotImprover.zip vendor\upstream\CS2BotImprover_upstream_latest.zip
```

不要把玩家已安装目录当作底座。玩家目录可能混有个人配置、旧 DLL、日志、缓存或半卸载残留。

### 2. 构建本仓库插件

发布前至少构建本仓库主动维护的 CounterStrikeSharp 插件。当前需要关注这些项目：

```text
addons/counterstrikesharp/plugins/BotAimImprover/BotAimImprover.csproj
addons/counterstrikesharp/plugins/BotAI/Common.csproj
addons/counterstrikesharp/plugins/BotAI/BotAI.csproj
addons/counterstrikesharp/plugins/BotBuy/BotBuy.csproj
addons/counterstrikesharp/plugins/BotHiderImpl/BotHiderImpl.csproj
addons/counterstrikesharp/plugins/BotRandomizer/BotRandomizer.csproj
addons/counterstrikesharp/plugins/BotState/BotState.csproj
addons/counterstrikesharp/plugins/NadeSystem/NadeSystem.csproj
addons/counterstrikesharp/plugins/RoundDamageRecap/RoundDamageRecap.csproj
addons/counterstrikesharp/shared/BotHiderApi/BotHiderApi.csproj
```

示例命令：

```powershell
dotnet build addons\counterstrikesharp\plugins\BotAimImprover\BotAimImprover.csproj -c Release
dotnet build addons\counterstrikesharp\plugins\BotAI\Common.csproj -c Release
dotnet build addons\counterstrikesharp\plugins\BotAI\BotAI.csproj -c Release
dotnet build addons\counterstrikesharp\plugins\BotBuy\BotBuy.csproj -c Release
dotnet build addons\counterstrikesharp\plugins\BotHiderImpl\BotHiderImpl.csproj -c Release
dotnet build addons\counterstrikesharp\plugins\BotRandomizer\BotRandomizer.csproj -c Release
dotnet build addons\counterstrikesharp\plugins\BotState\BotState.csproj -c Release
dotnet build addons\counterstrikesharp\plugins\NadeSystem\NadeSystem.csproj -c Release
dotnet build addons\counterstrikesharp\plugins\RoundDamageRecap\RoundDamageRecap.csproj -c Release
dotnet build addons\counterstrikesharp\shared\BotHiderApi\BotHiderApi.csproj -c Release
```

如果 `BotAI` 依赖 `Common`，先构建 `Common.csproj`，再构建 `BotAI.csproj`。

### 3. 展开旧整包到 staging

推荐输出目录：

```text
dist/LBTVCS2BotEnhancer/
dist/LBTVCS2BotEnhancer.zip
```

每次发布前删除旧 staging，再展开旧整包：

```powershell
Remove-Item dist\LBTVCS2BotEnhancer -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory dist\LBTVCS2BotEnhancer -Force
Expand-Archive vendor\upstream\CS2BotImprover_upstream_latest.zip -DestinationPath dist\LBTVCS2BotEnhancer
```

旧整包展开后通常已经包含 `gameinfo.gi`、`backup/`、基础 `addons/`、`cfg/`、`overrides/` 和面板可执行文件。后续步骤只覆盖我们负责的部分。

### 4. 覆盖本仓库文件

把本仓库维护的玩家向文件覆盖到 staging。不要整目录复制 `addons/`，否则会把 `.cs`、`.csproj`、`bin/`、`obj/` 这类开发文件混进发布包。

```powershell
Copy-Item README.md dist\LBTVCS2BotEnhancer\README.md -Force
Copy-Item Commands.txt dist\LBTVCS2BotEnhancer\Commands.txt -Force
Copy-Item cfg\* dist\LBTVCS2BotEnhancer\cfg -Recurse -Force
Copy-Item overrides\* dist\LBTVCS2BotEnhancer\overrides -Recurse -Force

Copy-Item addons\metamod\*.vdf dist\LBTVCS2BotEnhancer\addons\metamod\ -Force
Copy-Item addons\BotHider\*.json dist\LBTVCS2BotEnhancer\addons\BotHider\ -Force
Copy-Item addons\BotVision\* dist\LBTVCS2BotEnhancer\addons\BotVision\ -Recurse -Force
Copy-Item addons\counterstrikesharp\configs\* dist\LBTVCS2BotEnhancer\addons\counterstrikesharp\configs\ -Recurse -Force
Copy-Item addons\counterstrikesharp\plugins\NadeSystem\grenades\* dist\LBTVCS2BotEnhancer\addons\counterstrikesharp\plugins\NadeSystem\grenades\ -Recurse -Force
```

如果面板需要重新发布，先在 `Panel/` 内完成构建，再只把面板发布产物覆盖进 staging。不要把 `Panel/src`、`node_modules`、`dist` 的中间状态混进玩家整包，除非当前发布包本来就是源码包。

### 5. 覆盖插件构建产物

构建完成后，将每个插件的构建产物覆盖到 staging 的对应插件目录。BotAimImprover 与 NadeSystem 因依赖 CounterStrikeSharp.API 1.0.371，使用 `bin/Release/net10.0`；其余插件仍使用 `bin/Release/net8.0`。至少要覆盖：

```text
*.dll
*.deps.json
*.pdb
```

示例：

```powershell
Copy-Item addons\counterstrikesharp\plugins\NadeSystem\bin\Release\net10.0\NadeSystem.dll dist\LBTVCS2BotEnhancer\addons\counterstrikesharp\plugins\NadeSystem\ -Force
Copy-Item addons\counterstrikesharp\plugins\NadeSystem\bin\Release\net10.0\NadeSystem.deps.json dist\LBTVCS2BotEnhancer\addons\counterstrikesharp\plugins\NadeSystem\ -Force
Copy-Item addons\counterstrikesharp\plugins\NadeSystem\bin\Release\net10.0\NadeSystem.pdb dist\LBTVCS2BotEnhancer\addons\counterstrikesharp\plugins\NadeSystem\ -Force
```

脚本化时应像 `fresh` 项目一样维护一个插件清单，逐项校验构建产物是否存在，缺失就直接失败。

### 6. 重新生成 override VPK

`overrides/Low`、`overrides/Medium`、`overrides/High` 中的 `botprofile.db` 不应只以散文件形式留在 staging，发布前要重新打成 `botprofile.vpk` 并放回对应目录或根 `overrides` 使用位置。

参考 `E:\CS2-Bot-Improver_fresh\scripts\Rebuild-OverrideVpks.py` 的做法：脚本输入仓库根目录和 staging 的 `game/csgo` 根目录，然后用本仓库的 `botprofile.db` / localizations 生成最终 VPK。

发布校验时至少检查：

```powershell
Get-ChildItem dist\LBTVCS2BotEnhancer\overrides -Recurse -Filter botprofile.vpk
```

如果没有 `botprofile.vpk`，说明难度档很可能没有真正进入玩家可用包。

### 7. 修正 gameinfo 和不应发布的上游残留

沿用 `fresh` 项目的安全修正：

- 如果 `backup/WithBots/gameinfo.gi` 存在，确认其中包含 `DisallowPgTokens 1`。
- 将修正后的 `backup/WithBots/gameinfo.gi` 同步为 staging 根目录的 `gameinfo.gi`。
- 如果旧整包带入了本项目不希望发布的上游残留，例如冲突的旧插件、临时核心配置或测试配置，必须在 staging 中删除，而不是删除仓库源文件。

删除残留前先确认它确实不是本仓库当前需要的内容。发布清理只动 `dist/LBTVCS2BotEnhancer` 这类 staging 目录。

### 8. 压缩并命名新整包

压缩 staging 目录内部内容，而不是把 `LBTVCS2BotEnhancer` 目录本身包进去：

```powershell
Remove-Item dist\LBTVCS2BotEnhancer.zip -Force -ErrorAction SilentlyContinue
Compress-Archive -Path dist\LBTVCS2BotEnhancer\* -DestinationPath dist\LBTVCS2BotEnhancer.zip -CompressionLevel Optimal
```

最终玩家解压后应直接看到：

```text
addons/
backup/
cfg/
overrides/
gameinfo.gi
Commands.txt
README.md
```

不要让压缩包第一层变成 `dist/LBTVCS2BotEnhancer/addons/...` 或 `LBTVCS2BotEnhancer/addons/...`，否则安装说明会失效。

## 发布前检查清单

发布前至少检查这些点：

```powershell
Get-Item dist\LBTVCS2BotEnhancer.zip | Select-Object FullName,Length,LastWriteTime
Get-FileHash dist\LBTVCS2BotEnhancer.zip -Algorithm SHA256
```

并手动查看压缩包内容：

- 根目录第一层有 `addons/`、`cfg/`、`overrides/`、`gameinfo.gi`、`Commands.txt`、`README.md`。
- staging 中的 `README.md` 和 `Commands.txt` 是本仓库版本。
- 关键插件目录内的 `.dll` 时间戳来自本次构建。
- `addons/counterstrikesharp/configs` 中的配置目录没有被漏掉。
- `addons/counterstrikesharp/api/CounterStrikeSharp.API.deps.json` 包含 `CounterStrikeSharp.API/1.0.371`，且 `addons/counterstrikesharp/dotnet/` 仍存在。
- `addons/metamod/bin/win64/server.dll` 的产品版本为 `2.0.0-dev+1406`，启动日志中不再出现 `Detected engine 26 but could not load`。
- 正式包不包含 BotVision DLL/VDF；使用 `-IncludeBotVision` 时才检查 `addons/BotVision/bin/win64/BotVision.dll` 与 `addons/metamod/BotVision.vdf`。
- staging 和 zip 中没有 `vendor/`，BotVision 源码快照不能进入玩家整包。
- `overrides` 下存在本次重新生成的 `botprofile.vpk`。
- 新整包没有包含 `bin/`、`obj/`、`.git/`、`node_modules/`、脚本临时目录或旧 staging 外壳目录。

## 常见错误

- 只压缩本仓库根目录：会漏掉旧整包内的完整运行时和备份结构。
- 只替换源码不替换 DLL：玩家包仍然运行旧插件。
- 忘记重建 override VPK：`botprofile.db` 改了但游戏实际难度档不变。
- 覆盖 `addons` 时丢失 `configs/plugins` 子目录：插件可运行但默认配置、文本或 AI 参数丢失。
- 从玩家安装目录回收文件：容易把个人配置、日志、旧版本残留一起发布出去。
- 把 staging 整个目录作为 zip 第一层：玩家按 README 复制时会多一层目录。

## 后续建议

把上述流程沉淀为 `scripts/Build-FullRelease.ps1`。脚本应支持这些参数：

```powershell
param(
    [string]$Configuration = "Release",
    [string]$AuthorZipPath = "vendor\upstream\CS2BotImprover_upstream_latest.zip",
    [string]$OutputRoot = "dist",
    [string]$PackageName = "LBTVCS2BotEnhancer"
)
```

脚本需要做到：

- 缺少旧整包、插件项目或构建产物时立即失败。
- 每次发布都清空 staging 后重新展开旧整包。
- 自动构建插件并覆盖 `.dll`、`.deps.json`、`.pdb`。
- 自动复制本仓库的 `README.md`、`Commands.txt`、`cfg/`、`addons/counterstrikesharp/configs/`、`addons/BotHider/*.json`、手雷数据和必要资源；BotVision 仅由显式测试开关复制。
- 自动重建 override VPK。
- 压缩后打印 staging 路径、zip 路径、文件大小和 SHA256。
