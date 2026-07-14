[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$OutputRoot = "",
    [string]$PackageName = "LBTVCS2BotEnhancer",
    [switch]$IncludeBotVision
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $projectRoot "dist"
}

$assets = @{
    Upstream = @{ Path = "vendor\upstream\CS2BotImprover_upstream_latest.zip"; Hash = "8f4350d785b07af7adf3a978ceb23f2b4b6b49cf2c6592deba97ebb7b29c5981" }
    MetaMod = @{ Path = "vendor\metamod\mmsource-2.0.0-git1406-windows.zip"; Hash = "e147d4cbe90bbd4be3264cffe2b028792165c38f49f77b21c7964be0f117b131" }
    CounterStrikeSharp = @{ Path = "vendor\counterstrikesharp\counterstrikesharp-with-runtime-windows-1.0.371.zip"; Hash = "ab66ef273dfd41379f04777c80dd3c7fcae09c59cf3c689bd40b69b95b99af6e" }
    RayTraceCss = @{ Path = "vendor\raytrace\RayTrace-CSS-API-v1.0.16.tar.gz"; Hash = "e865ca551da35af31dc70f271840d1dce932a84e1c1bd27aa5ad3191efe6e1d4" }
    RayTraceWindows = @{ Path = "vendor\raytrace\RayTrace-MM-v1.0.16-windows.tar.gz"; Hash = "020b7b49cb249793a6840af3edfaf8d07c3c8a8069c8246972d113d047429c59" }
    BotHider = @{ Path = "vendor\bothider\BotHider-windows-0.3.0.zip"; Hash = "abdcb01a2466cf674a139efa9216a737e7a70024b024b1c8064ba81dca869e5f" }
}

$stagingRoot = Join-Path $OutputRoot $PackageName
$zipPath = Join-Path $OutputRoot ($PackageName + ".zip")
$extractRoot = Join-Path $OutputRoot ".release-inputs"
$rebuildOverrideScript = Join-Path $projectRoot "scripts\Rebuild-OverrideVpks.py"

function Assert-Hash([string]$Path, [string]$Expected) {
    if (-not (Test-Path -LiteralPath $Path)) { throw "Required release input missing: $Path" }
    $actual = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $Expected.ToLowerInvariant()) {
        throw "SHA-256 mismatch for $Path. Expected $Expected, got $actual"
    }
}

function Copy-Tree([string]$Source, [string]$Destination) {
    if (-not (Test-Path -LiteralPath $Source)) { throw "Overlay source missing: $Source" }
    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    Copy-Item -Path (Join-Path $Source "*") -Destination $Destination -Recurse -Force
}

function Expand-TarGz([string]$Archive, [string]$Destination) {
    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    & tar.exe -xzf $Archive -C $Destination
    if ($LASTEXITCODE -ne 0) { throw "Failed to extract $Archive" }
}

foreach ($asset in $assets.Values) {
    $asset.FullPath = Join-Path $projectRoot $asset.Path
    Assert-Hash $asset.FullPath $asset.Hash
}

New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null
if (Test-Path -LiteralPath $extractRoot) { Remove-Item -LiteralPath $extractRoot -Recurse -Force }
New-Item -ItemType Directory -Path $extractRoot -Force | Out-Null

$upstreamExtract = Join-Path $extractRoot "upstream"
$metamodExtract = Join-Path $extractRoot "metamod"
$cssExtract = Join-Path $extractRoot "counterstrikesharp"
$rayCssExtract = Join-Path $extractRoot "raytrace-css"
$rayWindowsExtract = Join-Path $extractRoot "raytrace-windows"
$botHiderExtract = Join-Path $extractRoot "bothider"
Expand-Archive -LiteralPath $assets.Upstream.FullPath -DestinationPath $upstreamExtract
Expand-Archive -LiteralPath $assets.MetaMod.FullPath -DestinationPath $metamodExtract
Expand-Archive -LiteralPath $assets.CounterStrikeSharp.FullPath -DestinationPath $cssExtract
Expand-TarGz $assets.RayTraceCss.FullPath $rayCssExtract
Expand-TarGz $assets.RayTraceWindows.FullPath $rayWindowsExtract
Expand-Archive -LiteralPath $assets.BotHider.FullPath -DestinationPath $botHiderExtract

$rayCssRoot = Get-ChildItem -LiteralPath $rayCssExtract -Directory -Recurse |
    Where-Object { Test-Path (Join-Path $_.FullName "counterstrikesharp\plugins\RayTraceImpl") } |
    Select-Object -First 1
if (-not $rayCssRoot) { throw "RayTrace CSS payload not found" }
$rayTraceApiPath = Join-Path $rayCssRoot.FullName "counterstrikesharp\shared\RayTraceApi\RayTraceApi.dll"
Assert-Hash $rayTraceApiPath "adfe79b62ebe119ffb03f268a8e00d2ac6bf675c692f5728e843cf65592e0ca8"

$projects = @(
    @{ Path = "addons\counterstrikesharp\shared\BotHiderApi\BotHiderApi.csproj"; RayTrace = $false },
    @{ Path = "addons\counterstrikesharp\plugins\BotAimImprover\BotAimImprover.csproj"; RayTrace = $true },
    @{ Path = "addons\counterstrikesharp\plugins\BotAI\Common.csproj"; RayTrace = $false },
    @{ Path = "addons\counterstrikesharp\plugins\BotAI\BotAI.csproj"; RayTrace = $false },
    @{ Path = "addons\counterstrikesharp\plugins\BotBuy\BotBuy.csproj"; RayTrace = $false },
    @{ Path = "addons\counterstrikesharp\plugins\BotHiderImpl\BotHiderImpl.csproj"; RayTrace = $false },
    @{ Path = "addons\counterstrikesharp\plugins\BotRandomizer\BotRandomizer.csproj"; RayTrace = $false },
    @{ Path = "addons\counterstrikesharp\plugins\BotState\BotState.csproj"; RayTrace = $false },
    @{ Path = "addons\counterstrikesharp\plugins\BotTaunt\BotTaunt.csproj"; RayTrace = $false },
    @{ Path = "addons\counterstrikesharp\plugins\MapRotation\MapRotation.csproj"; RayTrace = $false },
    @{ Path = "addons\counterstrikesharp\plugins\NadeSystem\NadeSystem.csproj"; RayTrace = $true },
    @{ Path = "addons\counterstrikesharp\plugins\RoundDamageRecap\RoundDamageRecap.csproj"; RayTrace = $false }
)
foreach ($project in $projects) {
    $projectPath = Join-Path $projectRoot $project.Path
    $args = @("build", $projectPath, "-c", $Configuration)
    if ($project.RayTrace) { $args += "-p:RayTraceApiPath=$rayTraceApiPath" }
    & dotnet @args | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed for $projectPath" }
}

if (Test-Path -LiteralPath $stagingRoot) { Remove-Item -LiteralPath $stagingRoot -Recurse -Force }
New-Item -ItemType Directory -Path $stagingRoot -Force | Out-Null
$upstreamPayload = @((Get-Item $upstreamExtract)) + @(Get-ChildItem $upstreamExtract -Directory -Recurse) |
    Where-Object { (Test-Path (Join-Path $_.FullName "addons")) -and (Test-Path (Join-Path $_.FullName "cfg")) } |
    Select-Object -First 1
if (-not $upstreamPayload) { throw "Upstream v1.4.1 payload not found" }
Copy-Tree $upstreamPayload.FullName $stagingRoot

Copy-Tree (Join-Path $metamodExtract "addons") (Join-Path $stagingRoot "addons")
Copy-Tree (Join-Path $cssExtract "addons") (Join-Path $stagingRoot "addons")
Copy-Tree (Join-Path $rayCssRoot.FullName "counterstrikesharp") (Join-Path $stagingRoot "addons\counterstrikesharp")

$rayNativeRoot = @((Get-Item $rayWindowsExtract)) + @(Get-ChildItem $rayWindowsExtract -Directory -Recurse) |
    Where-Object { (Test-Path (Join-Path $_.FullName "RayTrace\bin\win64\RayTrace.dll")) -and (Test-Path (Join-Path $_.FullName "metamod\RayTrace.vdf")) } |
    Select-Object -First 1
if (-not $rayNativeRoot) { throw "RayTrace native payload not found" }
Copy-Tree (Join-Path $rayNativeRoot.FullName "RayTrace") (Join-Path $stagingRoot "addons\RayTrace")
Copy-Item -LiteralPath (Join-Path $rayNativeRoot.FullName "metamod\RayTrace.vdf") -Destination (Join-Path $stagingRoot "addons\metamod\RayTrace.vdf") -Force

$botHiderAddons = Get-ChildItem -LiteralPath $botHiderExtract -Directory -Recurse |
    Where-Object { $_.Name -eq "addons" -and (Test-Path (Join-Path $_.FullName "BotHider")) } |
    Select-Object -First 1
if (-not $botHiderAddons) { throw "BotHider 0.3.0 payload not found" }
Copy-Tree $botHiderAddons.FullName (Join-Path $stagingRoot "addons")
Remove-Item -LiteralPath (Join-Path $stagingRoot "addons\metamod\BotHider.linux.vdf") -Force -ErrorAction SilentlyContinue

Copy-Item -LiteralPath (Join-Path $projectRoot "README.md") -Destination (Join-Path $stagingRoot "README.md") -Force
Copy-Item -LiteralPath (Join-Path $projectRoot "Commands.txt") -Destination (Join-Path $stagingRoot "Commands.txt") -Force
Copy-Tree (Join-Path $projectRoot "cfg") (Join-Path $stagingRoot "cfg")
Copy-Tree (Join-Path $projectRoot "overrides") (Join-Path $stagingRoot "overrides")
Copy-Tree (Join-Path $projectRoot "addons\counterstrikesharp\configs") (Join-Path $stagingRoot "addons\counterstrikesharp\configs")
Copy-Tree (Join-Path $projectRoot "addons\BotHider") (Join-Path $stagingRoot "addons\BotHider")
Copy-Item -LiteralPath (Join-Path $projectRoot "addons\metamod\BotHider.vdf") -Destination (Join-Path $stagingRoot "addons\metamod\BotHider.vdf") -Force
Copy-Tree (Join-Path $projectRoot "addons\counterstrikesharp\plugins\NadeSystem\grenades") (Join-Path $stagingRoot "addons\counterstrikesharp\plugins\NadeSystem\grenades")

if ($IncludeBotVision) {
    Copy-Tree (Join-Path $projectRoot "addons\BotVision") (Join-Path $stagingRoot "addons\BotVision")
    Copy-Item -LiteralPath (Join-Path $projectRoot "addons\metamod\BotVision.vdf") -Destination (Join-Path $stagingRoot "addons\metamod\BotVision.vdf") -Force
}
else {
    Remove-Item -LiteralPath (Join-Path $stagingRoot "addons\BotVision") -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath (Join-Path $stagingRoot "addons\metamod\BotVision.vdf") -Force -ErrorAction SilentlyContinue
}

$pluginOutputs = @(
    @{ Project = "addons\counterstrikesharp\shared\BotHiderApi"; Framework = "net10.0"; Files = @("BotHiderApi.dll", "BotHiderApi.deps.json", "BotHiderApi.pdb") },
    @{ Project = "addons\counterstrikesharp\plugins\BotAimImprover"; Framework = "net10.0"; Files = @("BotAimImprover.dll", "BotAimImprover.deps.json", "BotAimImprover.pdb") },
    @{ Project = "addons\counterstrikesharp\plugins\BotAI"; Framework = "net8.0"; Files = @("BotAI.dll", "BotAI.deps.json", "BotAI.pdb", "Common.dll", "Common.deps.json", "Common.pdb") },
    @{ Project = "addons\counterstrikesharp\plugins\BotBuy"; Framework = "net8.0"; Files = @("BotBuy.dll", "BotBuy.deps.json", "BotBuy.pdb") },
    @{ Project = "addons\counterstrikesharp\plugins\BotHiderImpl"; Framework = "net10.0"; Files = @("BotHiderImpl.dll", "BotHiderImpl.deps.json", "BotHiderImpl.pdb") },
    @{ Project = "addons\counterstrikesharp\plugins\BotRandomizer"; Framework = "net8.0"; Files = @("BotRandomizer.dll", "BotRandomizer.deps.json", "BotRandomizer.pdb") },
    @{ Project = "addons\counterstrikesharp\plugins\BotState"; Framework = "net8.0"; Files = @("BotState.dll", "BotState.deps.json", "BotState.pdb") },
    @{ Project = "addons\counterstrikesharp\plugins\BotTaunt"; Framework = "net8.0"; Files = @("BotTaunt.dll", "BotTaunt.deps.json", "BotTaunt.pdb") },
    @{ Project = "addons\counterstrikesharp\plugins\MapRotation"; Framework = "net8.0"; Files = @("MapRotation.dll", "MapRotation.deps.json", "MapRotation.pdb") },
    @{ Project = "addons\counterstrikesharp\plugins\NadeSystem"; Framework = "net10.0"; Files = @("NadeSystem.dll", "NadeSystem.deps.json", "NadeSystem.pdb") },
    @{ Project = "addons\counterstrikesharp\plugins\RoundDamageRecap"; Framework = "net8.0"; Files = @("RoundDamageRecap.dll", "RoundDamageRecap.deps.json", "RoundDamageRecap.pdb") }
)
foreach ($item in $pluginOutputs) {
    $buildDir = Join-Path $projectRoot ($item.Project + "\bin\" + $Configuration + "\" + $item.Framework)
    $releaseDir = Join-Path $stagingRoot $item.Project
    New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null
    foreach ($file in $item.Files) {
        $source = Join-Path $buildDir $file
        if (-not (Test-Path -LiteralPath $source)) { throw "Build artifact missing: $source" }
        Copy-Item -LiteralPath $source -Destination $releaseDir -Force
    }
}

$harmonySource = Join-Path $projectRoot "addons\counterstrikesharp\plugins\BotHiderImpl\bin\$Configuration\net10.0\shared\0Harmony\0Harmony.dll"
$harmonyDestination = Join-Path $stagingRoot "addons\counterstrikesharp\shared\0Harmony"
New-Item -ItemType Directory -Path $harmonyDestination -Force | Out-Null
Copy-Item -LiteralPath $harmonySource -Destination (Join-Path $harmonyDestination "0Harmony.dll") -Force
Remove-Item -LiteralPath (Join-Path $stagingRoot "addons\counterstrikesharp\plugins\BotHiderImpl\shared") -Recurse -Force -ErrorAction SilentlyContinue

$withBotsGameinfo = Join-Path $stagingRoot "backup\WithBots\gameinfo.gi"
if (-not (Test-Path -LiteralPath $withBotsGameinfo)) { throw "WithBots gameinfo.gi missing" }
$gameinfoContent = Get-Content -LiteralPath $withBotsGameinfo -Raw
if ($gameinfoContent -notmatch "DisallowPgTokens") {
    $gameinfoContent = $gameinfoContent -replace "DisallowTokenContexts\s+1", "`$0`r`n`t`tDisallowPgTokens`t`t1"
    [IO.File]::WriteAllText($withBotsGameinfo, $gameinfoContent, [Text.UTF8Encoding]::new($false))
}
$onlineGameinfo = Join-Path $stagingRoot "backup\Online\gameinfo.gi"
$onlineContent = $gameinfoContent
$onlineContent = $onlineContent -replace '(?m)^\s*Game\s+csgo/overrides/botprofile\.vpk\s*\r?\n', ''
$onlineContent = $onlineContent -replace '(?m)^\s*Game\s+csgo/addons/metamod\s*\r?\n', ''
New-Item -ItemType Directory -Path (Split-Path -Parent $onlineGameinfo) -Force | Out-Null
[IO.File]::WriteAllText($onlineGameinfo, $onlineContent, [Text.UTF8Encoding]::new($false))
Copy-Item -LiteralPath $withBotsGameinfo -Destination (Join-Path $stagingRoot "gameinfo.gi") -Force

python $rebuildOverrideScript --repo-root $projectRoot --game-csgo $stagingRoot | Out-Host
if ($LASTEXITCODE -ne 0) { throw "Override VPK rebuild failed" }
Remove-Item -LiteralPath (Join-Path $stagingRoot "addons\counterstrikesharp\plugins\BotCosmetics") -Recurse -Force -ErrorAction SilentlyContinue

$required = @(
    "gameinfo.gi", "backup\Online\gameinfo.gi", "backup\WithBots\gameinfo.gi",
    "addons\counterstrikesharp\api\CounterStrikeSharp.API.dll",
    "addons\counterstrikesharp\plugins\RayTraceImpl\RayTraceImpl.dll",
    "addons\counterstrikesharp\shared\RayTraceApi\RayTraceApi.dll",
    "addons\RayTrace\bin\win64\RayTrace.dll", "addons\metamod\RayTrace.vdf",
    "addons\BotHider\bin\win64\BotHider.dll", "addons\BotHider\gamedata.json",
    "addons\counterstrikesharp\plugins\BotHiderImpl\BotHiderImpl.dll",
    "addons\counterstrikesharp\shared\0Harmony\0Harmony.dll",
    "addons\counterstrikesharp\plugins\BotAimImprover\BotAimImprover.dll",
    "addons\counterstrikesharp\plugins\BotAI\BotAI.dll",
    "addons\counterstrikesharp\plugins\NadeSystem\NadeSystem.dll",
    "addons\counterstrikesharp\plugins\BotState\BotState.dll",
    "addons\counterstrikesharp\plugins\BotTaunt\BotTaunt.dll",
    "addons\counterstrikesharp\plugins\MapRotation\MapRotation.dll",
    "addons\counterstrikesharp\plugins\RoundDamageRecap\RoundDamageRecap.dll",
    "overrides\botprofile.vpk", "overrides\Low\botprofile.vpk", "overrides\Medium\botprofile.vpk", "overrides\High\botprofile.vpk"
)
foreach ($relative in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $stagingRoot $relative))) { throw "Required release file missing: $relative" }
}

Assert-Hash (Join-Path $stagingRoot "addons\RayTrace\bin\win64\RayTrace.dll") "5b18a8d43acc500960368875dd8670b695b3ea3f467832bbf61fe4989ab44093"
Assert-Hash (Join-Path $stagingRoot "addons\counterstrikesharp\plugins\RayTraceImpl\RayTraceImpl.dll") "301ac4ed23ae75c220ab88f6bb9604822eac32a94285fea8d2c388e5fd4420e0"
Assert-Hash (Join-Path $stagingRoot "addons\counterstrikesharp\shared\RayTraceApi\RayTraceApi.dll") "adfe79b62ebe119ffb03f268a8e00d2ac6bf675c692f5728e843cf65592e0ca8"
Assert-Hash (Join-Path $stagingRoot "addons\BotHider\bin\win64\BotHider.dll") "9b9259dd2e22752680d944df978f92a45b2891ca92393be58b7e82ee42ffa898"

if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
Compress-Archive -Path (Join-Path $stagingRoot "*") -DestinationPath $zipPath -CompressionLevel Optimal
Remove-Item -LiteralPath $extractRoot -Recurse -Force

$zipInfo = Get-Item $zipPath
$hash = Get-FileHash $zipPath -Algorithm SHA256
Write-Host "Full release package created:"
Write-Host "  Zip: $zipPath"
Write-Host "  Size: $($zipInfo.Length)"
Write-Host "  SHA256: $($hash.Hash)"
Write-Host "  BotVision enabled: $IncludeBotVision"
