using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace DeskBridge.Setup;

internal sealed record InstallProgress(string Message, int Percentage);
internal sealed record InstallResult(string InstallRoot, string ExtensionPath);

internal sealed class InstallerService
{
    private const string ProductVersion = "1.2.1";
    private const string ExtensionId = "chhimbcahcjjpggdlahimdcaohaaehhm";
    private const string PayloadResource = "DeskBridge.Setup.Payload.zip";

    private readonly string _installRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Programs",
        "DeskBridge");

    public Task<InstallResult> InstallAsync(IProgress<InstallProgress> progress, CancellationToken cancellationToken) =>
        Task.Run(() => Install(progress, cancellationToken), cancellationToken);

    private InstallResult Install(IProgress<InstallProgress> progress, CancellationToken cancellationToken)
    {
        string stagingRoot = Path.Combine(Path.GetTempPath(), $"DeskBridge-install-{Environment.ProcessId}-{Guid.NewGuid():N}");
        string operationId = Guid.NewGuid().ToString("N");
        string pendingRoot = _installRoot + $".installing-{operationId}";
        string previousRoot = _installRoot + $".previous-{operationId}";
        EnsureChildPath(stagingRoot, Path.GetTempPath());
        try
        {
            progress.Report(new("Preparing verified application files...", 18));
            Directory.CreateDirectory(stagingRoot);
            ExtractPayload(stagingRoot, cancellationToken);

            progress.Report(new("Installing DeskBridge for this Windows account...", 46));
            StopInstalledProcesses();
            string programsRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs");
            EnsureChildPath(_installRoot, programsRoot);
            EnsureChildPath(pendingRoot, programsRoot);
            EnsureChildPath(previousRoot, programsRoot);
            CopyDirectory(stagingRoot, pendingRoot, cancellationToken);

            bool previousInstallMoved = false;
            try
            {
                if (Directory.Exists(_installRoot))
                {
                    Directory.Move(_installRoot, previousRoot);
                    previousInstallMoved = true;
                }
                Directory.Move(pendingRoot, _installRoot);

                progress.Report(new("Registering the protected Chrome connection...", 70));
                RegisterNativeHost();
                RegisterInstalledApplication();
                File.WriteAllText(Path.Combine(_installRoot, "installed-version.txt"), ProductVersion, new UTF8Encoding(false));
            }
            catch (Exception installError)
            {
                try
                {
                    if (Directory.Exists(_installRoot)) Directory.Delete(_installRoot, true);
                    if (previousInstallMoved && Directory.Exists(previousRoot)) Directory.Move(previousRoot, _installRoot);
                }
                catch (Exception restoreError)
                {
                    throw new AggregateException(
                        "DeskBridge could not finish the upgrade or restore the previous installation automatically.",
                        installError,
                        restoreError);
                }
                throw;
            }

            TryDeleteDirectory(previousRoot);

            string extensionPath = Path.Combine(_installRoot, "extension");
            progress.Report(new("Finalizing the installed application...", 92));
            progress.Report(new("DeskBridge is installed.", 100));
            return new(_installRoot, extensionPath);
        }
        finally
        {
            try
            {
                if (Directory.Exists(stagingRoot)) Directory.Delete(stagingRoot, true);
                TryDeleteDirectory(pendingRoot);
            }
            catch
            {
                // A temporary file can remain briefly while Windows releases a handle.
            }
        }
    }

    private static void ExtractPayload(string stagingRoot, CancellationToken cancellationToken)
    {
        using Stream payload = Assembly.GetExecutingAssembly().GetManifestResourceStream(PayloadResource)
            ?? throw new InvalidDataException("The installer payload is missing. Download DeskBridge Setup again.");
        using var archive = new ZipArchive(payload, ZipArchiveMode.Read, false);
        if (archive.Entries.Count == 0) throw new InvalidDataException("The installer payload is empty.");

        string resolvedRoot = Path.GetFullPath(stagingRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string destination = Path.GetFullPath(Path.Combine(stagingRoot, entry.FullName));
            if (!destination.StartsWith(resolvedRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The installer payload contains an unsafe path.");

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(destination);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            entry.ExtractToFile(destination, true);
        }
    }

    private void StopInstalledProcesses()
    {
        string resolvedRoot = Path.GetFullPath(_installRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        foreach (string processName in new[] { "DeskBridge.App", "DeskBridge.Host" })
        {
            foreach (Process process in Process.GetProcessesByName(processName))
            {
                using (process)
                {
                    try
                    {
                        string? path = process.MainModule?.FileName;
                        if (path is not null && path.StartsWith(resolvedRoot, StringComparison.OrdinalIgnoreCase))
                        {
                            process.Kill(true);
                            process.WaitForExit(5000);
                        }
                    }
                    catch
                    {
                        // Ignore processes whose executable path Windows does not allow this user to inspect.
                    }
                }
            }
        }

        foreach (string processName in new[] { "DeskBridge.App", "DeskBridge.Host" })
        {
            if (Process.GetProcessesByName(processName).Any(process =>
            {
                using (process)
                {
                    try
                    {
                        string? path = process.MainModule?.FileName;
                        return path is not null && path.StartsWith(resolvedRoot, StringComparison.OrdinalIgnoreCase);
                    }
                    catch
                    {
                        return false;
                    }
                }
            }))
            {
                throw new IOException("Close DeskBridge and try the installer again.");
            }
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
        catch
        {
            // A prior version can remain briefly if Explorer or security software still holds a handle.
            // It is outside the active installation and can be removed by a later maintenance pass.
        }
    }

    private void RegisterNativeHost()
    {
        string hostPath = Path.Combine(_installRoot, "DeskBridge.Host.exe");
        if (!File.Exists(hostPath)) throw new FileNotFoundException("The installed native host is missing.", hostPath);

        string nativeDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DeskBridge",
            "native-host");
        Directory.CreateDirectory(nativeDirectory);
        string manifestPath = Path.Combine(nativeDirectory, "com.deskbridge.host.json");
        var manifest = new
        {
            name = "com.deskbridge.host",
            description = "DeskBridge Native Messaging Host",
            path = hostPath,
            type = "stdio",
            allowed_origins = new[] { $"chrome-extension://{ExtensionId}/" }
        };
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));

        const string registrySubKey = @"Software\Google\Chrome\NativeMessagingHosts\com.deskbridge.host";
        foreach (RegistryView registryView in new[] { RegistryView.Registry32, RegistryView.Registry64 })
        {
            using RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, registryView);
            using RegistryKey hostKey = baseKey.CreateSubKey(registrySubKey, true);
            hostKey.SetValue(string.Empty, manifestPath, RegistryValueKind.String);
        }
    }

    private void RegisterInstalledApplication()
    {
        string appPath = Path.Combine(_installRoot, "DeskBridge.App.exe");
        string startMenuDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft",
            "Windows",
            "Start Menu",
            "Programs",
            "DeskBridge");
        Directory.CreateDirectory(startMenuDirectory);
        CreateShortcut(Path.Combine(startMenuDirectory, "DeskBridge.lnk"), appPath);
        CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "DeskBridge.lnk"), appPath);

        using RegistryKey uninstallKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\DeskBridge", true);
        string uninstallScript = Path.Combine(_installRoot, "scripts", "uninstall-native-host.ps1");
        string uninstallCommand = $"powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"{uninstallScript}\" -RemoveInstalledFiles";
        uninstallKey.SetValue("DisplayName", "DeskBridge");
        uninstallKey.SetValue("DisplayVersion", ProductVersion);
        uninstallKey.SetValue("Publisher", "TDUmii");
        uninstallKey.SetValue("InstallLocation", _installRoot);
        uninstallKey.SetValue("DisplayIcon", appPath);
        uninstallKey.SetValue("UninstallString", uninstallCommand);
        uninstallKey.SetValue("NoModify", 1, RegistryValueKind.DWord);
        uninstallKey.SetValue("NoRepair", 1, RegistryValueKind.DWord);
    }

    private static void CreateShortcut(string shortcutPath, string targetPath)
    {
        Type shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new PlatformNotSupportedException("Windows shortcut support is unavailable.");
        object shell = Activator.CreateInstance(shellType)!;
        try
        {
            dynamic shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath })!;
            shortcut.TargetPath = targetPath;
            shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath)!;
            shortcut.IconLocation = $"{targetPath},0";
            shortcut.Description = "DeskBridge - verified ChatGPT Web file agent";
            shortcut.Save();
            Marshal.FinalReleaseComObject(shortcut);
        }
        finally
        {
            Marshal.FinalReleaseComObject(shell);
        }
    }

    public IReadOnlyList<string> OpenInstalledApplications(string extensionPath)
    {
        var warnings = new List<string>();
        string installRoot = Path.GetDirectoryName(extensionPath)!;
        TryLaunch(
            new ProcessStartInfo(Path.Combine(installRoot, "DeskBridge.App.exe")) { UseShellExecute = true },
            "Open DeskBridge from its desktop shortcut.",
            warnings);

        string[] chromeCandidates =
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "Application", "chrome.exe")
        };
        string? chromePath = chromeCandidates.FirstOrDefault(File.Exists);
        if (chromePath is not null)
            TryLaunch(
                new ProcessStartInfo(chromePath, "chrome://extensions") { UseShellExecute = true },
                "Open chrome://extensions manually.",
                warnings);
        else
            warnings.Add("Google Chrome was not found. Open chrome://extensions after Chrome is installed.");

        TryLaunch(
            new ProcessStartInfo("explorer.exe", $"\"{extensionPath}\"") { UseShellExecute = true },
            $"Open the extension folder manually: {extensionPath}",
            warnings);
        return warnings;
    }

    private static void TryLaunch(ProcessStartInfo startInfo, string recovery, ICollection<string> warnings)
    {
        try
        {
            Process.Start(startInfo);
        }
        catch
        {
            warnings.Add(recovery);
        }
    }

    private static void CopyDirectory(string sourceRoot, string destinationRoot, CancellationToken cancellationToken)
    {
        foreach (string directory in Directory.EnumerateDirectories(sourceRoot, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Path.Combine(destinationRoot, Path.GetRelativePath(sourceRoot, directory)));
        }
        foreach (string file in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string destination = Path.Combine(destinationRoot, Path.GetRelativePath(sourceRoot, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, true);
        }
    }

    private static void EnsureChildPath(string candidate, string root)
    {
        string resolvedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string resolvedCandidate = Path.GetFullPath(candidate);
        if (!resolvedCandidate.StartsWith(resolvedRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Unsafe installer path: {resolvedCandidate}");
    }
}
