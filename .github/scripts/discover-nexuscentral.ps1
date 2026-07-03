param(
    [string]$ReleaseTargetsPath = "updates/release-targets.json"
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$targetsPath = Join-Path $repoRoot $ReleaseTargetsPath

if (-not (Test-Path $targetsPath)) {
    throw "Release targets file was not found: $targetsPath"
}

$releaseTargets = Get-Content $targetsPath -Raw | ConvertFrom-Json
$versionDirs = @(
    Get-ChildItem -Path $repoRoot -Directory -Filter "NexusCentral-V*" |
        Where-Object {
            (Test-Path (Join-Path $_.FullName "ARCHITECTURE.md")) -and
            ((Get-ChildItem -Path (Join-Path $_.FullName "Desktop") -Filter "*.csproj" -File -ErrorAction SilentlyContinue).Count -gt 0) -and
            ((Get-ChildItem -Path (Join-Path $_.FullName "Tests") -Filter "*.csproj" -File -ErrorAction SilentlyContinue).Count -gt 0)
        } |
        Sort-Object Name |
        ForEach-Object { $_.Name }
)

if ($versionDirs.Count -eq 0) {
    throw "No buildable NexusCentral-V* lanes were found."
}

$targets = @($releaseTargets.targets)
if ($targets.Count -eq 0) {
    throw "No release targets were found in $ReleaseTargetsPath."
}

$versionJson = ConvertTo-Json $versionDirs -Compress
$targetJson = ConvertTo-Json $targets -Compress

if ($env:GITHUB_OUTPUT) {
    "version_dirs=$versionJson" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8
    "targets=$targetJson" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8
    "dotnet_sdk=$($releaseTargets.dotnetSdk)" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8
    "configuration=$($releaseTargets.configuration)" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8
    "self_contained=$($releaseTargets.selfContained.ToString().ToLowerInvariant())" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8
    "artifact_retention_days=$($releaseTargets.artifactRetentionDays)" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8
}

Write-Host "Buildable version dirs: $($versionDirs -join ', ')"
Write-Host "Targets: $($targets.rid -join ', ')"
