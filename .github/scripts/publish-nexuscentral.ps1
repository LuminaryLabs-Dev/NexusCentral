param(
    [Parameter(Mandatory = $true)]
    [string]$VersionDir,

    [Parameter(Mandatory = $true)]
    [string]$Runtime,

    [string]$Configuration = "Release",

    [bool]$SelfContained = $true,

    [string]$OutputRoot = "artifacts"
)

$ErrorActionPreference = "Stop"

function Get-DirectorySizeBytes {
    param([Parameter(Mandatory = $true)][string]$Path)

    $sum = 0L
    Get-ChildItem -Path $Path -Recurse -File | ForEach-Object {
        $sum += $_.Length
    }
    return $sum
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$versionRoot = Join-Path $repoRoot $VersionDir
$manifestPath = Join-Path $versionRoot "app.manifest.json"

if (-not (Test-Path $versionRoot)) {
    throw "Version directory was not found: $versionRoot"
}

$project = Get-ChildItem -Path (Join-Path $versionRoot "Desktop") -Filter "*.csproj" -File | Select-Object -First 1
$testProject = Get-ChildItem -Path (Join-Path $versionRoot "Tests") -Filter "*.csproj" -File | Select-Object -First 1

if (-not $project) {
    throw "Desktop project was not found under $VersionDir/Desktop."
}

if (-not $testProject) {
    throw "Test project was not found under $VersionDir/Tests."
}

$appVersion = "0.1.0-dev"
if (Test-Path $manifestPath) {
    $manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
    if ($manifest.productVersion) {
        $appVersion = [string]$manifest.productVersion
    }
}

if ($env:NEXUSCENTRAL_RELEASE_VERSION) {
    $appVersion = $env:NEXUSCENTRAL_RELEASE_VERSION
} elseif ($env:GITHUB_REF_NAME -and $env:GITHUB_REF_NAME.StartsWith("v")) {
    $appVersion = $env:GITHUB_REF_NAME.Substring(1)
}

$publishRoot = Join-Path $repoRoot $OutputRoot
$publishDir = Join-Path $publishRoot "publish/$VersionDir/$Runtime"
$archiveDir = Join-Path $publishRoot "archives"
$manifestDir = Join-Path $publishRoot "manifests"

Remove-Item -Path $publishDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $publishDir, $archiveDir, $manifestDir | Out-Null

dotnet restore $project.FullName --runtime $Runtime
dotnet restore $testProject.FullName
dotnet build $project.FullName --configuration $Configuration --runtime $Runtime --no-restore
dotnet test $testProject.FullName --configuration $Configuration --no-restore
dotnet publish $project.FullName `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained $SelfContained `
    --no-restore `
    -p:Version=$appVersion `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    --output $publishDir

$archiveBaseName = "nexuscentral-$VersionDir-$appVersion-$Runtime"
$archivePath = Join-Path $archiveDir "$archiveBaseName.zip"
Remove-Item -Path $archivePath -Force -ErrorAction SilentlyContinue
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $archivePath -Force

$publishedBytes = Get-DirectorySizeBytes -Path $publishDir
$archiveBytes = (Get-Item $archivePath).Length
$archiveHash = (Get-FileHash -Path $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()

$sizeManifest = [ordered]@{
    schemaVersion = 1
    appId = "com.nexuscentral.desktop"
    versionDir = $VersionDir
    appVersion = $appVersion
    runtime = $Runtime
    configuration = $Configuration
    selfContained = $SelfContained
    publishedBytes = $publishedBytes
    archiveBytes = $archiveBytes
    archiveSha256 = $archiveHash
    archiveName = (Split-Path $archivePath -Leaf)
}

$sizeManifestPath = Join-Path $manifestDir "$archiveBaseName.size.json"
$sizeManifest | ConvertTo-Json -Depth 6 | Set-Content -Path $sizeManifestPath -Encoding utf8

Write-Host "Published: $publishDir"
Write-Host "Archive: $archivePath"
Write-Host "Published bytes: $publishedBytes"
Write-Host "Archive bytes: $archiveBytes"
Write-Host "SHA256: $archiveHash"
