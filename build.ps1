Param(
    [switch] $Release
)

$ErrorActionPreference = 'Stop'

# Options
$configuration = 'Release'
$artifactsDir = Join-Path (Resolve-Path .) 'artifacts'
$binDir = Join-Path $artifactsDir 'Bin'
$logsDir = Join-Path $artifactsDir 'Logs'

# Detection
. $PSScriptRoot\build\Get-DetectedCiVersion.ps1
$versionInfo = Get-DetectedCiVersion -Release:$Release
Update-CiServerBuildName $versionInfo.ProductVersion
Write-Host "Building using version $($versionInfo.ProductVersion)"

$dotnetArgs = @(
    '--configuration', $configuration
    '/p:RepositoryCommit=' + $versionInfo.CommitHash
    '/p:Version=' + $versionInfo.ProductVersion
    '/p:FileVersion=' + $versionInfo.FileVersion
    '/p:ContinuousIntegrationBuild=' + ($env:CI -or $env:TF_BUILD)
)

# Build
dotnet build /bl:$logsDir\build.binlog @dotnetArgs
if ($LastExitCode) { exit 1 }

# Publish
Remove-Item -Recurse -Force $binDir -ErrorAction Ignore

dotnet publish src\YouTubeDownloadTool --no-build --output $binDir /bl:$logsDir\publish.binlog @dotnetArgs
if ($LastExitCode) { exit 1 }
