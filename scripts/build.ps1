[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot

Push-Location $repositoryRoot
try {
    dotnet restore .\DeskBridge.sln
    if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed.' }
    dotnet build .\DeskBridge.sln -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed.' }
    dotnet test .\DeskBridge.sln -c Release --no-build
    if ($LASTEXITCODE -ne 0) { throw 'dotnet test failed.' }

    Push-Location .\extension
    try {
        npm.cmd ci
        if ($LASTEXITCODE -ne 0) { throw 'npm ci failed.' }
        npm.cmd run build
        if ($LASTEXITCODE -ne 0) { throw 'npm build failed.' }
        npm.cmd audit --audit-level=moderate
        if ($LASTEXITCODE -ne 0) { throw 'npm audit failed.' }
    }
    finally {
        Pop-Location
    }
}
finally {
    Pop-Location
}
