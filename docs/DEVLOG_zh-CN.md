# 开发记录

## 2026-06-14

- 以当前仓库 `RoundDamageRecap` 为基线合入 fresh `1.2.1` 增量逻辑，没有整文件替换。
- `OnPlayerHurt` 仍优先使用 CS2 提供的真人直接 attacker；缺失 attacker 时才回退到近期道具爆炸或投掷记录，降低重复记账风险。
- 新增 `lbtv_difficulty` 命令，复用同一个 `BuildDifficultyMessage()`，避免回合播报和手动查询输出不一致。
- 已执行 `dotnet build addons\counterstrikesharp\plugins\RoundDamageRecap\RoundDamageRecap.csproj -c Release`，结果为 0 警告、0 错误。
