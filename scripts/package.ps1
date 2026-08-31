[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$artifactsRoot = Join-Path $repositoryRoot 'artifacts'
$packageRoot = Join-Path $artifactsRoot 'DeskBridge-win-x64'
$appProjectPath = Join-Path $repositoryRoot 'src\DeskBridge.App\DeskBridge.App.csproj'
$appProject = [xml](Get-Content -LiteralPath $appProjectPath -Raw)
$version = [string]($appProject.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version
if ([string]::IsNullOrWhiteSpace($version)) {
    throw 'DeskBridge version is missing from DeskBridge.App.csproj.'
}
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
if ($LASTEXITCODE -ne 0) { throw 'DeskBridge.Host publish failed.' }
dotnet publish (Join-Path $repositoryRoot 'src\DeskBridge.App\DeskBridge.App.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:DebugType=None -p:DebugSymbols=false "-p:PathMap=$repositoryRoot=/_/" -o $appStage
if ($LASTEXITCODE -ne 0) { throw 'DeskBridge.App publish failed.' }
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
Get-ChildItem -LiteralPath $resolvedPackageRoot -Filter '*.pdb' -File -Recurse | Remove-Item -Force

foreach ($stage in @($hostStage, $appStage)) {
    if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
}

$installerStage = Join-Path $artifactsRoot 'installer-stage'
$resolvedInstallerStage = [System.IO.Path]::GetFullPath($installerStage)
if (-not $resolvedInstallerStage.StartsWith($resolvedArtifactsRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Refusing to clean an installer staging path outside artifacts.'
}
if (Test-Path -LiteralPath $resolvedInstallerStage) {
    Remove-Item -LiteralPath $resolvedInstallerStage -Recurse -Force
}
New-Item -ItemType Directory -Path $resolvedInstallerStage | Out-Null

$payloadPath = Join-Path $resolvedInstallerStage 'DeskBridge.payload.zip'
Compress-Archive -Path (Join-Path $resolvedPackageRoot '*') -DestinationPath $payloadPath -CompressionLevel Optimal

$installerPath = Join-Path $artifactsRoot "DeskBridge-Setup-v$version.exe"
$checksumPath = "$installerPath.sha256"
if (Test-Path -LiteralPath $installerPath) { Remove-Item -LiteralPath $installerPath -Force }
if (Test-Path -LiteralPath $checksumPath) { Remove-Item -LiteralPath $checksumPath -Force }

$setupPublish = Join-Path $resolvedInstallerStage 'publish'
dotnet publish (Join-Path $repositoryRoot 'src\DeskBridge.Setup\DeskBridge.Setup.csproj') `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false `
    "-p:InstallerPayload=$payloadPath" `
    "-p:PathMap=$repositoryRoot=/_/" `
    -o $setupPublish
if ($LASTEXITCODE -ne 0) {
    throw 'DeskBridge native setup bootstrapper publish failed.'
}
$publishedInstaller = Join-Path $setupPublish 'DeskBridge.Setup.exe'
if (-not (Test-Path -LiteralPath $publishedInstaller -PathType Leaf)) {
    throw 'DeskBridge one-file installer creation failed.'
}
Move-Item -LiteralPath $publishedInstaller -Destination $installerPath -Force

$hash = (Get-FileHash -LiteralPath $installerPath -Algorithm SHA256).Hash
[System.IO.File]::WriteAllText(
    $checksumPath,
    "$hash  $([System.IO.Path]::GetFileName($installerPath))`r`n",
    [System.Text.UTF8Encoding]::new($false)
)

Remove-Item -LiteralPath $resolvedInstallerStage -Recurse -Force

if (Test-Path -LiteralPath $resolvedPackageRoot) {
    Remove-Item -LiteralPath $resolvedPackageRoot -Recurse -Force
}
$releaseAllowList = @(
    [System.IO.Path]::GetFileName($installerPath),
    [System.IO.Path]::GetFileName($checksumPath)
)
Get-ChildItem -LiteralPath $artifactsRoot -File | Where-Object { $_.Name -notin $releaseAllowList } | ForEach-Object {
    $resolvedFile = [System.IO.Path]::GetFullPath($_.FullName)
    if (-not $resolvedFile.StartsWith($resolvedArtifactsRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove an artifact outside the repository artifacts directory: $resolvedFile"
    }
    Remove-Item -LiteralPath $resolvedFile -Force
}
Write-Host "DeskBridge one-file installer created at: $installerPath"
Write-Host "SHA256: $hash"
