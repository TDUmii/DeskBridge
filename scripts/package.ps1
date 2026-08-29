[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$artifactsRoot = Join-Path $repositoryRoot 'artifacts'
$packageRoot = Join-Path $artifactsRoot 'DeskBridge-win-x64'
$resolvedArtifactsRoot = [System.IO.Path]::GetFullPath($artifactsRoot).TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
$resolvedPackageRoot = [System.IO.Path]::GetFullPath($packageRoot)
if (-not $resolvedPackageRoot.StartsWith($resolvedArtifactsRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Refusing to clean a package path outside the repository artifacts directory.'
}

if (Test-Path -LiteralPath $resolvedPackageRoot) {
    Remove-Item -LiteralPath $resolvedPackageRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $resolvedPackageRoot | Out-Null

& (Join-Path $PSScriptRoot 'build.ps1')

$hostStage = Join-Path $artifactsRoot 'host-stage'
$appStage = Join-Path $artifactsRoot 'app-stage'
foreach ($stage in @($hostStage, $appStage)) {
    $resolvedStage = [System.IO.Path]::GetFullPath($stage)
    if (-not $resolvedStage.StartsWith($resolvedArtifactsRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw 'Refusing to clean a staging path outside artifacts.'
    }
    if (Test-Path -LiteralPath $resolvedStage) { Remove-Item -LiteralPath $resolvedStage -Recurse -Force }
}

dotnet publish (Join-Path $repositoryRoot 'src\DeskBridge.Host\DeskBridge.Host.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:DebugType=None -p:DebugSymbols=false "-p:PathMap=$repositoryRoot=/_/" -o $hostStage
dotnet publish (Join-Path $repositoryRoot 'src\DeskBridge.App\DeskBridge.App.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:DebugType=None -p:DebugSymbols=false "-p:PathMap=$repositoryRoot=/_/" -o $appStage
Copy-Item -Path (Join-Path $hostStage '*') -Destination $resolvedPackageRoot -Recurse -Force
Copy-Item -Path (Join-Path $appStage '*') -Destination $resolvedPackageRoot -Recurse -Force
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'extension\dist') -Destination (Join-Path $resolvedPackageRoot 'extension') -Recurse
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'README.md') -Destination $resolvedPackageRoot
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'LICENSE') -Destination $resolvedPackageRoot
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'THIRD_PARTY_NOTICES.md') -Destination $resolvedPackageRoot
$releaseScripts = Join-Path $resolvedPackageRoot 'scripts'
New-Item -ItemType Directory -Path $releaseScripts | Out-Null
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'scripts\install-native-host.ps1') -Destination $releaseScripts
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'scripts\uninstall-native-host.ps1') -Destination $releaseScripts
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'Install-DeskBridge.cmd') -Destination $resolvedPackageRoot
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'Uninstall-DeskBridge.cmd') -Destination $resolvedPackageRoot
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'INSTALL.txt') -Destination $resolvedPackageRoot
Get-ChildItem -LiteralPath $resolvedPackageRoot -Filter '*.pdb' -File -Recurse | Remove-Item -Force

foreach ($stage in @($hostStage, $appStage)) {
    if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
}
Write-Host "DeskBridge package created at: $resolvedPackageRoot"
