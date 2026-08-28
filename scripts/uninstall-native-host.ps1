[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$registryPath = 'HKCU:\Software\Google\Chrome\NativeMessagingHosts\com.deskbridge.host'
if (Test-Path -LiteralPath $registryPath) {
    Remove-Item -LiteralPath $registryPath -Recurse
    Write-Host 'DeskBridge Native Messaging registration removed.'
}
else {
    Write-Host 'DeskBridge Native Messaging registration was not present.'
}
