[CmdletBinding()]
param(
    [string]$OutputDirectory = ""
)

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { $OutputDirectory = Join-Path $repo "artifacts" }
$manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot "dependencies.json") -Raw | ConvertFrom-Json
$upstreamZip = Join-Path $repo "vendor\upstream\CS2BotImprover_upstream_latest.zip"
$nadeBuild = Join-Path $repo "addons\counterstrikesharp\plugins\NadeSystem\bin\Release\net10.0"
$stage = Join-Path $OutputDirectory ".lbtv-release-stage"
$zip = Join-Path $OutputDirectory "LBTVCS2BotEnhancer.zip"

function Assert-Hash([string]$Path, [string]$Expected) {
    $actual = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $Expected.ToLowerInvariant()) { throw "SHA-256 mismatch for ${Path}: $actual" }
}

Assert-Hash $upstreamZip $manifest.upstream.windowsAsset.sha256
foreach ($file in @("NadeSystem.dll", "NadeSystem.deps.json")) {
    if (-not (Test-Path -LiteralPath (Join-Path $nadeBuild $file))) { throw "Missing NadeSystem Release output: $file" }
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
New-Item -ItemType Directory -Path $stage -Force | Out-Null
try {
    Expand-Archive -LiteralPath $upstreamZip -DestinationPath $stage
    $releaseRoot = Get-ChildItem -LiteralPath $stage -Directory | Select-Object -First 1
    if (-not $releaseRoot) { throw "Plus release root missing from $upstreamZip" }
    $nadeDestination = Join-Path $releaseRoot.FullName "addons\counterstrikesharp\plugins\NadeSystem"
    Copy-Item -LiteralPath (Join-Path $nadeBuild "NadeSystem.dll") -Destination $nadeDestination -Force
    Copy-Item -LiteralPath (Join-Path $nadeBuild "NadeSystem.deps.json") -Destination $nadeDestination -Force
    Remove-Item -LiteralPath (Join-Path $nadeDestination "NadeSystem.pdb") -Force -ErrorAction SilentlyContinue
    foreach ($file in @("LICENSE", "README.md", "README.zh-CN.md")) {
        Copy-Item -LiteralPath (Join-Path $repo $file) -Destination (Join-Path $releaseRoot.FullName $file) -Force
    }

    & (Join-Path $PSScriptRoot "verify-workspace.ps1") -PackageRoot $releaseRoot.FullName
    if ($LASTEXITCODE -ne 0) { throw "Package verification failed." }
    if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
    Compress-Archive -Path (Join-Path $releaseRoot.FullName "*") -DestinationPath $zip -CompressionLevel Optimal
    $hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
    Set-Content -LiteralPath (Join-Path $OutputDirectory "SHA256SUMS.txt") -Value "$hash  $([IO.Path]::GetFileName($zip))" -Encoding ascii
    Write-Host "Package complete: $zip"
    Write-Host "SHA-256: $hash"
}
finally {
    if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
}
