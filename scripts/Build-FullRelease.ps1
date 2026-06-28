[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$AuthorZipPath = "",
    [string]$OutputRoot = "",
    [string]$PackageName = "LBTVCS2BotEnhancer"
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($AuthorZipPath)) {
    $AuthorZipPath = Join-Path $projectRoot "vendor\upstream\CS2BotImprover_upstream_latest.zip"
}
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $projectRoot "dist"
}

$stagingRoot = Join-Path $OutputRoot $PackageName
$zipPath = Join-Path $OutputRoot ($PackageName + ".zip")
$rebuildOverrideScript = Join-Path $projectRoot "scripts\Rebuild-OverrideVpks.py"

$projects = @(
    "addons\counterstrikesharp\shared\BotHiderApi\BotHiderApi.csproj",
    "addons\counterstrikesharp\plugins\BotAimImprover\BotAimImprover.csproj",
    "addons\counterstrikesharp\plugins\BotAI\Common.csproj",
    "addons\counterstrikesharp\plugins\BotAI\BotAI.csproj",
    "addons\counterstrikesharp\plugins\BotBuy\BotBuy.csproj",
    "addons\counterstrikesharp\plugins\BotHiderImpl\BotHiderImpl.csproj",
    "addons\counterstrikesharp\plugins\BotRandomizer\BotRandomizer.csproj",
    "addons\counterstrikesharp\plugins\BotState\BotState.csproj",
    "addons\counterstrikesharp\plugins\BotTaunt\BotTaunt.csproj",
    "addons\counterstrikesharp\plugins\MapRotation\MapRotation.csproj",
    "addons\counterstrikesharp\plugins\NadeSystem\NadeSystem.csproj",
    "addons\counterstrikesharp\plugins\RoundDamageRecap\RoundDamageRecap.csproj"
)

$pluginOutputs = @(
    @{ Project = "addons\counterstrikesharp\shared\BotHiderApi"; Files = @("BotHiderApi.dll", "BotHiderApi.deps.json", "BotHiderApi.pdb") },
    @{ Project = "addons\counterstrikesharp\plugins\BotAimImprover"; Files = @("BotAimImprover.dll", "BotAimImprover.deps.json", "BotAimImprover.pdb") },
    @{ Project = "addons\counterstrikesharp\plugins\BotAI"; Files = @("BotAI.dll", "BotAI.deps.json", "BotAI.pdb", "Common.dll", "Common.deps.json", "Common.pdb") },
    @{ Project = "addons\counterstrikesharp\plugins\BotBuy"; Files = @("BotBuy.dll", "BotBuy.deps.json", "BotBuy.pdb") },
    @{ Project = "addons\counterstrikesharp\plugins\BotHiderImpl"; Files = @("BotHiderImpl.dll", "BotHiderImpl.deps.json", "BotHiderImpl.pdb", "shared\0Harmony\0Harmony.dll") },
    @{ Project = "addons\counterstrikesharp\plugins\BotRandomizer"; Files = @("BotRandomizer.dll", "BotRandomizer.deps.json", "BotRandomizer.pdb") },
    @{ Project = "addons\counterstrikesharp\plugins\BotState"; Files = @("BotState.dll", "BotState.deps.json", "BotState.pdb") },
    @{ Project = "addons\counterstrikesharp\plugins\BotTaunt"; Files = @("BotTaunt.dll", "BotTaunt.deps.json", "BotTaunt.pdb") },
    @{ Project = "addons\counterstrikesharp\plugins\MapRotation"; Files = @("MapRotation.dll", "MapRotation.deps.json", "MapRotation.pdb") },
    @{ Project = "addons\counterstrikesharp\plugins\NadeSystem"; Files = @("NadeSystem.dll", "NadeSystem.deps.json", "NadeSystem.pdb") },
    @{ Project = "addons\counterstrikesharp\plugins\RoundDamageRecap"; Files = @("RoundDamageRecap.dll", "RoundDamageRecap.deps.json", "RoundDamageRecap.pdb") }
)

function Copy-ItemRecursive([string]$Source, [string]$Destination) {
    if (Test-Path -LiteralPath $Source) {
        if (-not (Test-Path -LiteralPath $Destination)) {
            New-Item -ItemType Directory -Path $Destination -Force | Out-Null
        }
        Copy-Item -Path (Join-Path $Source "*") -Destination $Destination -Recurse -Force
    }
}

if (-not (Test-Path -LiteralPath $AuthorZipPath)) {
    throw "Author release zip not found: $AuthorZipPath"
}

New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null

foreach ($relativeProject in $projects) {
    $projectPath = Join-Path $projectRoot $relativeProject
    if (-not (Test-Path -LiteralPath $projectPath)) {
        throw "Project not found: $projectPath"
    }

    dotnet build $projectPath -c $Configuration | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed for: $projectPath"
    }
}

if (Test-Path -LiteralPath $stagingRoot) {
    Remove-Item -LiteralPath $stagingRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $stagingRoot -Force | Out-Null

Expand-Archive -LiteralPath $AuthorZipPath -DestinationPath $stagingRoot

if (-not (Test-Path -LiteralPath $rebuildOverrideScript)) {
    throw "Override rebuild script not found: $rebuildOverrideScript"
}

$withBotsGameinfo = Join-Path $stagingRoot "backup\WithBots\gameinfo.gi"
if (Test-Path -LiteralPath $withBotsGameinfo) {
    $gameinfoContent = Get-Content -LiteralPath $withBotsGameinfo -Raw
    if ($gameinfoContent -notmatch 'DisallowPgTokens') {
        if ($gameinfoContent -match 'DisallowTokenContexts\s+1') {
            $gameinfoContent = $gameinfoContent -replace 'DisallowTokenContexts\s+1', "`$0`r`n`t`tDisallowPgTokens`t`t1"
        }
        else {
            $gameinfoContent += "`r`n`t`tDisallowPgTokens`t`t1`r`n"
        }
        Set-Content -LiteralPath $withBotsGameinfo -Value $gameinfoContent -Encoding UTF8
    }
    Copy-Item -LiteralPath $withBotsGameinfo -Destination (Join-Path $stagingRoot "gameinfo.gi") -Force
}

python $rebuildOverrideScript --repo-root $projectRoot --game-csgo $stagingRoot | Out-Host
if ($LASTEXITCODE -ne 0) {
    throw "Override VPK rebuild failed for staging root: $stagingRoot"
}

$stagingBotCosmetics = Join-Path $stagingRoot "addons\counterstrikesharp\plugins\BotCosmetics"
if (Test-Path -LiteralPath $stagingBotCosmetics) {
    Remove-Item -LiteralPath $stagingBotCosmetics -Recurse -Force
}

Copy-Item -LiteralPath (Join-Path $projectRoot "README.md") -Destination (Join-Path $stagingRoot "README.md") -Force
Copy-Item -LiteralPath (Join-Path $projectRoot "Commands.txt") -Destination (Join-Path $stagingRoot "Commands.txt") -Force
Copy-Item -Path (Join-Path $projectRoot "cfg\*") -Destination (Join-Path $stagingRoot "cfg") -Recurse -Force
Copy-ItemRecursive (Join-Path $projectRoot "overrides") (Join-Path $stagingRoot "overrides")
Copy-ItemRecursive (Join-Path $projectRoot "addons\counterstrikesharp\configs") (Join-Path $stagingRoot "addons\counterstrikesharp\configs")
Copy-ItemRecursive (Join-Path $projectRoot "addons\metamod") (Join-Path $stagingRoot "addons\metamod")
Copy-ItemRecursive (Join-Path $projectRoot "addons\BotHider") (Join-Path $stagingRoot "addons\BotHider")
Copy-ItemRecursive (Join-Path $projectRoot "addons\counterstrikesharp\plugins\NadeSystem\grenades") (Join-Path $stagingRoot "addons\counterstrikesharp\plugins\NadeSystem\grenades")

foreach ($item in $pluginOutputs) {
    $buildOutputDir = Join-Path $projectRoot ($item.Project + "\bin\" + $Configuration + "\net8.0")
    $releasePluginDir = Join-Path $stagingRoot $item.Project

    if (-not (Test-Path -LiteralPath $buildOutputDir)) {
        throw "Build output not found: $buildOutputDir"
    }

    if (-not (Test-Path -LiteralPath $releasePluginDir)) {
        New-Item -ItemType Directory -Path $releasePluginDir -Force | Out-Null
    }

    foreach ($file in $item.Files) {
        $source = Join-Path $buildOutputDir $file
        if (-not (Test-Path -LiteralPath $source)) {
            throw "Expected build artifact not found: $source"
        }
        $destination = Join-Path $releasePluginDir $file
        $destinationDir = Split-Path -Parent $destination
        if (-not (Test-Path -LiteralPath $destinationDir)) {
            New-Item -ItemType Directory -Path $destinationDir -Force | Out-Null
        }
        Copy-Item -LiteralPath $source -Destination $destination -Force
    }
}

$requiredPaths = @(
    "addons",
    "cfg",
    "overrides",
    "gameinfo.gi",
    "Commands.txt",
    "README.md",
    "addons\counterstrikesharp\plugins\BotTaunt\BotTaunt.dll",
    "addons\counterstrikesharp\configs\plugins\BotTaunt\BotTaunt.json",
    "addons\counterstrikesharp\configs\plugins\BotTaunt\Taunts.json",
    "addons\counterstrikesharp\plugins\MapRotation\MapRotation.dll",
    "addons\counterstrikesharp\plugins\RoundDamageRecap\RoundDamageRecap.dll",
    "addons\counterstrikesharp\plugins\NadeSystem\NadeSystem.dll",
    "overrides\botprofile.vpk",
    "overrides\Low\botprofile.vpk",
    "overrides\Medium\botprofile.vpk",
    "overrides\High\botprofile.vpk"
)

foreach ($relative in $requiredPaths) {
    $full = Join-Path $stagingRoot $relative
    if (-not (Test-Path -LiteralPath $full)) {
        throw "Required release file missing: $relative"
    }
}

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}
Compress-Archive -Path (Join-Path $stagingRoot "*") -DestinationPath $zipPath -CompressionLevel Optimal

$zipInfo = Get-Item $zipPath
$hash = Get-FileHash $zipPath -Algorithm SHA256

Write-Host ""
Write-Host "Full release package created:"
Write-Host "  Staging: $stagingRoot"
Write-Host "  Zip:     $zipPath"
Write-Host "  Size:    $($zipInfo.Length)"
Write-Host "  SHA256:  $($hash.Hash)"
