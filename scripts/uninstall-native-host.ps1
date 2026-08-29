[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$registrySubKey = 'Software\Google\Chrome\NativeMessagingHosts\com.deskbridge.host'
$removed = $false
foreach ($registryView in @([Microsoft.Win32.RegistryView]::Registry32, [Microsoft.Win32.RegistryView]::Registry64)) {
    $baseKey = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::CurrentUser, $registryView)
    try {
        if ($baseKey.OpenSubKey($registrySubKey)) {
            $baseKey.DeleteSubKeyTree($registrySubKey, $false)
            $removed = $true
        }
    }
    finally {
        $baseKey.Dispose()
    }
}

if ($removed) {
    Write-Host 'DeskBridge Native Messaging registration removed from 32-bit and 64-bit registry views.'
} else {
    Write-Host 'DeskBridge Native Messaging registration was not present.'
}
