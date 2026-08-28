[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot

Push-Location $repositoryRoot
try {
    dotnet restore .\DeskBridge.sln
    dotnet build .\DeskBridge.sln -c Release --no-restore
    dotnet test .\DeskBridge.sln -c Release --no-build

    Push-Location .\extension
    try {
        npm.cmd ci
        npm.cmd run build
        npm.cmd audit --audit-level=moderate
    }
    finally {
        Pop-Location
    }
}
finally {
    Pop-Location
}
