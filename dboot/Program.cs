using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using BootstrapperShared;
using dboot;
using dboot.Builder;
using dboot.Core;
using dboot.SubSystem;
using Microsoft.Win32;
using Semver;
using Serilog;
using Windows.Win32;

var isUninstall = args.Contains("--uninstall");
var debug = args.Contains("--debug");
#if DEBUG
debug = true;
#endif
var isQuiet = args.Contains("--quiet");

var builder = new BootstrapBuilder();

if (isQuiet)
{
    builder.IsQuiet();
}
builder.WithLogging("dboot.log", debug);

builder.ConfigureDialog((d) =>
{
    d.Title = Constants.AppName;
    d.Icon = Constants.IconData;
    d.AutoClose = false;
});

#if DEBUG
builder.UpdateClient("http://localhost:8080");
#else
builder.UpdateClient("https://cdn.dolus.app/");
#endif

builder.WithInstallInfo(i =>
{
    i.AppName = Constants.AppName;
    i.Publisher = Constants.Publisher;
    i.UninstallGuid = Constants.UninstallGuid;
});

builder.ConfigureInstall(i =>
{
    i.AddStep(CheckIfDolusIsRunning);
    i.AddStep(CheckSystemRequirements);
    i.AddStep(CollectSystemInformation);
    i.AddStep(FetchCatalog);
    i.AddStep(HandleUpdateOrInstall);
    i.AddStep(PerformFileInstallation);
    i.AddStep(UpdateOrCreateRegistry);
    i.AddStep(CreateShortcut);
});

builder.ConfigureUninstall(u =>
{
    u.AddStep(CollectSystemInformation);
    u.AddStep(CheckIfDolusIsRunning);
    u.AddStep(ConfirmUninstall);
    u.AddStep(RemoveFiles);
    u.AddStep(RemoveRegistry);
    u.AddStep(RemoveShortcut);
});

builder.OnPostInstall(() =>
{
    var installDirectory = Path.Combine(SystemUtils.GetProgramFilesPath()!, Constants.AppName);
    var mainFile = Path.Combine(installDirectory, $"{Constants.AppName}.exe");
    var startInfo = new ProcessStartInfo(mainFile)
    {
        UseShellExecute = true,
    };
    Process.Start(startInfo);
    return ValueTask.CompletedTask;
});
builder.OnPostUnInstall(() =>
{
    var startInfo = new ProcessStartInfo("https://dolus.app/?uninstalled=true")
    {
        UseShellExecute = true,
    };
    Process.Start(startInfo);
    return ValueTask.CompletedTask;
});

try
{
    var bootstrapper = builder.Build();
    if (!isUninstall)
    {
        return Convert.ToInt32(await bootstrapper.Install());
    }
    else
    {
        return Convert.ToInt32(await bootstrapper.Uninstall());
    }

}
finally
{
    ConsoleManager.ReleaseConsole();
}

#region Install Step Methods

ValueTask<StepResult> CheckSystemRequirements(ProgressDialog dialog, Context context, CancellationToken token)
{
    dialog.Line1 = "Checking System Requirements";
    dialog.Line2 = "Verifying Windows version...";

    if (!SystemUtils.IsWindowsBuild17134OrAbove())
    {
        dialog.Line1 = "System Requirements Not Met";
        dialog.Line2 = "Your Windows version is not supported.";
        dialog.Line3 = $"{Constants.AppName} requires Windows 10 (version 1803) or later.";
        return ValueTask.FromResult(StepResult.Abort);
    }
    return ValueTask.FromResult(StepResult.Continue);
}

ValueTask<StepResult> CheckIfDolusIsRunning(ProgressDialog dialog, Context context, CancellationToken token)
{
    dialog.Line1 = "Preparing for Installation";
    dialog.Line2 = $"Checking if {Constants.AppName} is currently running...";
    // Dolus is running
    if (SystemUtils.IsMutexHeld(Constants.DolusAppId))
    {
        if (isUninstall)
        {
            if (!isQuiet)
            {
                PInvoke.MessageBoxEx(dialog.Handle, "an error telling the user to exit Dolus and deactivate all their modules before uninstalling", "Error", Windows.Win32.UI.WindowsAndMessaging.MESSAGEBOX_STYLE.MB_OK | Windows.Win32.UI.WindowsAndMessaging.MESSAGEBOX_STYLE.MB_ICONERROR, 0);
            }
            return ValueTask.FromResult(StepResult.Stop);
        }
        return ValueTask.FromResult(StepResult.Stop);
    }

    dialog.Line2 = $"{Constants.AppName} is not currently running.";
    dialog.Line3 = "Proceeding with installation...";

    return ValueTask.FromResult(StepResult.Continue);
}

ValueTask<StepResult> CollectSystemInformation(ProgressDialog dialog, Context context, CancellationToken token)
{
    dialog.Line1 = "Collecting System Information";
    dialog.Line2 = "Checking installation directory...";

    var programFiles = SystemUtils.GetProgramFilesPath();
    if (string.IsNullOrEmpty(programFiles) || !Directory.Exists(programFiles))
    {
        dialog.Line1 = "System Error";
        dialog.Line2 = "Program Files directory is missing or invalid.";
        dialog.Line3 = "Please ensure your Windows installation is not corrupted.";
        return ValueTask.FromResult(StepResult.Abort);
    }

    dialog.Line2 = "Checking branch information...";

    var installDirectory = Path.Combine(programFiles, context.InstallOptions.AppName);
    context.AddData("install-directory", installDirectory);

    var branch = RegistryHelper.GetValue<string>(Microsoft.Win32.RegistryHive.LocalMachine, context.InstallOptions.AppRegistryPath, "Branch", "main");
    if (string.IsNullOrEmpty(branch))
    {
        dialog.Line1 = "Configuration Error";
        dialog.Line2 = "Branch information not found in the registry.";
        dialog.Line3 = $"Try reinstalling {Constants.AppName} or contact support.";
        return ValueTask.FromResult(StepResult.Abort);
    }
    context.AddData("branch", branch);

    var installedVersion = RegistryHelper.GetValue<string>(Microsoft.Win32.RegistryHive.LocalMachine, context.InstallOptions.UninstallRegistryPath, "DisplayVersion");
    if (!string.IsNullOrEmpty(installedVersion) && SemVersion.TryParse(installedVersion, SemVersionStyles.Strict, out var semVer))
    {
        context.AddData("installed-version", semVer);
    }

    bool isInstalled = !string.IsNullOrEmpty(installedVersion) && !SystemUtils.DirectoryDoesNotExistOrIsEmpty(installDirectory);
    context.AddData("is-installed", isInstalled);
    context.IsAlreadyInstalled = isInstalled;

    return ValueTask.FromResult(StepResult.Continue);
}

async ValueTask<StepResult> FetchCatalog(ProgressDialog dialog, Context context, CancellationToken token)
{
    dialog.Line1 = "Fetching Update Catalog";
    dialog.Line2 = "Connecting to update server...";

    var catalog = await context.UpdateClient.GetCatalog(token);
    var isInstalled = context.GetData<bool>("is-installed");
    if (catalog is null)
    {
        if (!isInstalled)
        {
            dialog.Line1 = "Network Error";
            dialog.Line2 = "Unable to fetch update catalog.";
            dialog.Line3 = "Check your internet connection and try again.";
            Log.Information("Aborting due to unavailable catalog");
            return StepResult.Abort;
        }
        return StepResult.Stop;
    }
    context.AddData("catalog", catalog);
    return StepResult.Continue;
}

async ValueTask<StepResult> HandleUpdateOrInstall(ProgressDialog dialog, Context context, CancellationToken token)
{
    dialog.Line1 = "Preparing Update/Install";
    dialog.Line2 = "Analyzing current installation...";

    var catalog = context.GetData<Catalog>("catalog")!;
    var isInstalled = context.GetData<bool>("is-installed");
    var branch = context.GetData<string>("branch")!;
    var branchInfo = catalog.GetBranch(branch);

    if (branchInfo is null)
    {
        dialog.Line2 = "Unable to find branch information.";
        return !isInstalled
            ? HandleCatalogUnavailable(dialog)
            : StepResult.Stop;
    }

    var currentVersion = SemVersion.Parse(branchInfo.CurrentVersion, style: SemVersionStyles.Strict);
    var currentRelease = branchInfo.GetCurrentVersionInfo();

    if (!isInstalled)
    {
        dialog.Line2 = "Preparing for fresh installation...";
        return await HandleFreshInstall(dialog, context, currentRelease, branchInfo.CurrentVersion, token);
    }

    var installedVersion = context.GetData<SemVersion>("installed-version")!;

    if (currentVersion.CompareSortOrderTo(installedVersion) <= 0)
    {
        dialog.Line2 = "Checking integrity of current installation...";
        return await HandleIntegrityCheck(dialog, context, branchInfo, installedVersion, currentRelease, token);
    }

    dialog.Line2 = "Update available. Preparing for download...";
    return await DownloadAndPrepareUpdate(dialog, context, currentRelease, token, branchInfo.CurrentVersion);
}

ValueTask<StepResult> PerformFileInstallation(ProgressDialog dialog, Context context, CancellationToken token)
{
    var updatePackagePath = context.GetData<string>("update-package");
    var isInstalled = context.GetData<bool>("is-installed");
    var installDirectory = context.GetData<string>("install-directory")!;
    var version = context.GetData<string>("update-version");

    dialog.Line1 = isInstalled ? $"Updating {Constants.AppName}" : $"Installing {Constants.AppName}";
    dialog.Line2 = "Preparing for installation...";

    if (string.IsNullOrEmpty(updatePackagePath) || !File.Exists(updatePackagePath))
    {
        dialog.Line1 = "Installation Error";
        dialog.Line2 = "Update package not found or invalid.";
        dialog.Line3 = "Try restarting the installer or contact support.";
        return ValueTask.FromResult(isInstalled ? StepResult.Stop : StepResult.Abort);
    }

    dialog.Line2 = $"Installing version {version}...";
    dialog.Marquee = false;
    dialog.Maximum = 100;

    try
    {
        var progress = new Progress<int>(percent =>
        {
            dialog.Value = percent;
        });
        if (!Directory.Exists(installDirectory))
        {
            Directory.CreateDirectory(installDirectory);
        }

        UpdatePatcher.ExtractZip(updatePackagePath, installDirectory, progress, token);

        dialog.Line2 = isInstalled ? "Update completed successfully." : "Installation completed successfully.";
        dialog.Line3 = "Finalizing...";
        return ValueTask.FromResult(StepResult.Continue);
    }
    catch (OperationCanceledException)
    {
        dialog.Line1 = "Operation Cancelled";
        dialog.Line2 = "Installation was cancelled by the user.";
        dialog.Line3 = "No changes were made to your system.";
        return ValueTask.FromResult(StepResult.Abort);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error during installation/update");
        dialog.Line1 = "Installation Error";
        dialog.Line2 = "An unexpected error occurred during installation.";
        dialog.Line3 = $"Error details: {ex.Message}";
        return ValueTask.FromResult(isInstalled ? StepResult.Stop : StepResult.Abort);
    }
    finally
    {
        if (File.Exists(updatePackagePath))
        {
            File.Delete(updatePackagePath);
        }
    }
}


async ValueTask<StepResult> UpdateOrCreateRegistry(ProgressDialog dialog, Context context, CancellationToken token)
{
    dialog.Line1 = "Finalizing Installation";
    dialog.Line2 = "Updating Windows Registry...";

    var installOptions = context.InstallOptions;
    var installDirectory = context.GetData<string>("install-directory")!;
    var version = context.GetData<string>("update-version")!;

    try
    {
        bool success = RegistryHelper.UpdateKey(RegistryHive.LocalMachine, installOptions.UninstallRegistryPath, key =>
        {
            key.SetValue("DisplayName", installOptions.AppName, RegistryValueKind.String);
            key.SetValue("DisplayVersion", version, RegistryValueKind.String);
            key.SetValue("DisplayIcon", Path.Combine(installDirectory, installOptions.DisplayIcon), RegistryValueKind.String);
            key.SetValue("InstallLocation", installDirectory, RegistryValueKind.String);
            key.SetValue("Publisher", installOptions.Publisher, RegistryValueKind.String);
            key.SetValue("UninstallString", $"\"{Path.Combine(installDirectory, "dboot.exe")}\" --uninstall", RegistryValueKind.String);
            key.SetValue("NoModify", 1, RegistryValueKind.DWord);
            key.SetValue("NoRepair", 1, RegistryValueKind.DWord);

            if (!string.IsNullOrEmpty(installOptions.HelpLink))
                key.SetValue("HelpLink", installOptions.HelpLink, RegistryValueKind.String);

            if (!string.IsNullOrEmpty(installOptions.URLInfoAbout))
                key.SetValue("URLInfoAbout", installOptions.URLInfoAbout, RegistryValueKind.String);
        });

        if (success)
        {
            dialog.Line2 = "Registry updated successfully.";
            dialog.Line3 = "Installation process complete.";
            return StepResult.Continue;
        }
        else
        {
            dialog.Line1 = "Registry Error";
            dialog.Line2 = "Failed to update Windows registry.";
            dialog.Line3 = "Try running the installer as administrator.";
            return StepResult.Abort;
        }
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Failed to update registry");
        dialog.Line1 = "Registry Error";
        dialog.Line2 = "Failed to update Windows registry.";
        dialog.Line3 = $"Error details: {ex.Message}";
        return StepResult.Abort;
    }
}

unsafe ValueTask<StepResult> CreateShortcut(ProgressDialog dialog, Context context, CancellationToken token)
{
    dialog.Line1 = "Finalizing Installation";
    dialog.Line2 = "Creating shortcut...";

    var installDirectory = context.GetData<string>("install-directory")!;

    try
    {

        // Use Windows API to get the Start Menu folder
        Guid startMenuFolderId = new("625B53C3-AB48-4EC1-BA1F-A1EF4146FC19"); // FOLDERID_StartMenu
        Windows.Win32.Foundation.HRESULT hr = PInvoke.SHGetKnownFolderPath(
            in startMenuFolderId,
            Windows.Win32.UI.Shell.KNOWN_FOLDER_FLAG.KF_FLAG_DEFAULT,
            null,
            out Windows.Win32.Foundation.PWSTR pszPath
        );

        if (hr.Failed)
        {
            throw new InvalidOperationException($"Failed to retrieve Start Menu path. Error code: {hr}");
        }

        var startMenuPath = pszPath.ToString();
        if (string.IsNullOrEmpty(startMenuPath))
        {
            throw new InvalidOperationException("Retrieved Start Menu path is empty.");
        }
        Log.Information("{poath}", startMenuPath);


        var shortcutPath = Path.Combine(startMenuPath, "Programs", $"{Constants.AppName}.lnk");
        var targetPath = Path.Combine(installDirectory, $"dboot.exe");

        // Initialize COM
        PInvoke.CoInitializeEx(default, Windows.Win32.System.Com.COINIT.COINIT_APARTMENTTHREADED);

        try
        {
            // Ensure the start menu directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(shortcutPath)!);

            using var shellLink = new ShellLinkWrapper();
            shellLink.SetTargetPath(targetPath);
            shellLink.SetDescription($"{Constants.AppName} Application");
            shellLink.SetWorkingDirectory(installDirectory);

            // Optionally set an icon if you have one
            var iconPath = Path.Combine(installDirectory, context.InstallOptions.DisplayIcon);
            if (File.Exists(iconPath))
            {
                shellLink.SetIconLocation(iconPath, 0);
            }

            shellLink.Save(shortcutPath);

            dialog.Line2 = "Shortcut created successfully.";
            Log.Information("shortcut crearted at {poath}", startMenuPath);
            return ValueTask.FromResult(StepResult.Continue);
        }
        finally
        {
            // Uninitialize COM
            PInvoke.CoUninitialize();
        }
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Failed to create shortcut");
        dialog.Line2 = "Failed to create shortcut.";
        dialog.Line3 = $"Error details: {ex.Message}";
        // last step, no need to abort
        return ValueTask.FromResult(StepResult.Abort);
    }
}

#endregion

#region Helper Methods

StepResult HandleCatalogUnavailable(ProgressDialog dialog)
{
    dialog.Line1 = "Catalog Error";
    dialog.Line2 = "Unable to fetch update catalog.";
    dialog.Line3 = "Please check your internet connection and try again.";
    return StepResult.Abort;
}

async Task<StepResult> HandleFreshInstall(ProgressDialog dialog, Context context, VersionInfo currentRelease, string version, CancellationToken token)
{
    dialog.Line1 = $"Installing {Constants.AppName}";
    dialog.Line2 = $"Downloading {Constants.AppName} v{version}...";
    return await DownloadAndPrepareUpdate(dialog, context, currentRelease, token, version);
}

async Task<StepResult> HandleIntegrityCheck(ProgressDialog dialog, Context context, BranchInfo branchInfo, SemVersion installedVersion, VersionInfo currentRelease, CancellationToken token)
{
    dialog.Line1 = $"Updating {Constants.AppName}";
    dialog.Line2 = "Verifying installation...";
    var versionInfo = branchInfo.GetVersionInfo(installedVersion.ToString());

    if (versionInfo is null)
    {
        return StepResult.Stop;
    }
    var files = versionInfo.Files;

    var installDirectory = context.GetData<string>("install-directory")!;
    var hasIntegrity = await FileIntegrityVerifier.VerifyFileIntegrity(dialog, installDirectory, files, token);

    return hasIntegrity
        ? StepResult.Stop
        : await DownloadAndPrepareUpdate(dialog, context, versionInfo, token, installedVersion.ToString(), stopOnFailure: true);
}

async Task<StepResult> DownloadAndPrepareUpdate(ProgressDialog dialog, Context context, VersionInfo release, CancellationToken token, string version, bool stopOnFailure = false)
{
    var tempPath = SystemUtils.GetTempFileName();
    var succeeded = await context.UpdateClient.DownloadZipFile(release.ReleasePath, tempPath, dialog, token);

    if (!succeeded)
    {
        dialog.Line3 = "Failed to download update";
        dialog.Marquee = false;
        return stopOnFailure ? StepResult.Stop : StepResult.Abort;
    }

    context.AddData("update-package", tempPath);
    context.AddData("update-version", version);
    dialog.Marquee = true;
    return StepResult.Continue;
}


#endregion

#region Uninstall Step Methods

ValueTask<StepResult> ConfirmUninstall(ProgressDialog dialog, Context context, CancellationToken token)
{
    if (isQuiet)
    {
        return ValueTask.FromResult(StepResult.Continue);
    }

    dialog.Line1 = $"Uninstall {Constants.AppName}";
    dialog.Line2 = "Are you sure you want to uninstall?";
    dialog.Line3 = "This will remove all files and settings.";

    var result = PInvoke.MessageBoxEx(dialog.Handle, $"Are you sure you want to uninstall {Constants.AppName}?", "Confirm Uninstall", Windows.Win32.UI.WindowsAndMessaging.MESSAGEBOX_STYLE.MB_YESNO | Windows.Win32.UI.WindowsAndMessaging.MESSAGEBOX_STYLE.MB_ICONQUESTION, 0);

    return ValueTask.FromResult(result is Windows.Win32.UI.WindowsAndMessaging.MESSAGEBOX_RESULT.IDYES ? StepResult.Continue : StepResult.Stop);
}

ValueTask<StepResult> RemoveFiles(ProgressDialog dialog, Context context, CancellationToken token)
{
    dialog.Line1 = $"Uninstalling {Constants.AppName}";
    dialog.Line2 = "Removing files...";

    var installDirectory = context.GetData<string>("install-directory")!;
    var uninstallerPath = Environment.ProcessPath!;

    try
    {
        if (Directory.Exists(installDirectory))
        {
            // Remove files except the uninstaller
            foreach (var file in Directory.GetFiles(installDirectory, "*", SearchOption.AllDirectories))
            {
                if (!file.Equals(uninstallerPath, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(file);
                }
            }

            // Remove empty subdirectories
            foreach (var dir in Directory.GetDirectories(installDirectory, "*", SearchOption.AllDirectories).Reverse())
            {
                if (!dir.Equals(Path.GetDirectoryName(uninstallerPath), StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        Directory.Delete(dir, false);
                    }
                    catch (IOException)
                    {
                        // Directory not empty, ignore
                    }
                }
            }

            // Handle the uninstaller file
            HandleUninstallerFile(uninstallerPath);

            // Schedule the installation directory for removal
            ScheduleDirectoryForRemoval(installDirectory);
        }

        dialog.Line2 = "Files removed successfully.";
        dialog.Line3 = "Remaining files and directory will be removed on next system restart.";
        return ValueTask.FromResult(StepResult.Continue);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error removing files during uninstall");
        dialog.Line1 = "Uninstall Error";
        dialog.Line2 = "Failed to remove some files.";
        dialog.Line3 = $"Error details: {ex.Message}";
        return ValueTask.FromResult(StepResult.Abort);
    }
}

void HandleUninstallerFile(string uninstallerPath)
{
    try
    {
        // Schedule the uninstaller for deletion on next system restart
        if (!PInvoke.MoveFileEx(uninstallerPath, null, Windows.Win32.Storage.FileSystem.MOVE_FILE_FLAGS.MOVEFILE_DELAY_UNTIL_REBOOT))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        Log.Information("Scheduled uninstaller for deletion on next system restart: {UninstallerPath}", uninstallerPath);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Failed to schedule uninstaller for deletion: {UninstallerPath}", uninstallerPath);
        throw;
    }
}

void ScheduleDirectoryForRemoval(string directory)
{
    try
    {
        // Schedule the directory for deletion on next system restart
        if (!PInvoke.MoveFileEx(directory, null, Windows.Win32.Storage.FileSystem.MOVE_FILE_FLAGS.MOVEFILE_DELAY_UNTIL_REBOOT))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        Log.Information("Scheduled directory for deletion on next system restart: {Directory}", directory);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Failed to schedule directory for deletion: {Directory}", directory);
        throw;
    }
}


ValueTask<StepResult> RemoveRegistry(ProgressDialog dialog, Context context, CancellationToken token)
{
    dialog.Line1 = $"Uninstalling {Constants.AppName}";
    dialog.Line2 = "Removing registry entries...";

    var installOptions = context.InstallOptions;

    try
    {
        Registry.LocalMachine.DeleteSubKeyTree(installOptions.UninstallRegistryPath, false);
        Registry.LocalMachine.DeleteSubKeyTree(installOptions.AppRegistryPath, false);

        dialog.Line2 = "Registry entries removed successfully.";
        dialog.Line3 = "Uninstall process complete.";
        return ValueTask.FromResult(StepResult.Continue);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error removing registry entries during uninstall");
        dialog.Line1 = "Uninstall Error";
        dialog.Line2 = "Failed to remove some registry entries.";
        dialog.Line3 = $"Error details: {ex.Message}";
        return ValueTask.FromResult(StepResult.Abort);
    }
}

unsafe ValueTask<StepResult> RemoveShortcut(ProgressDialog dialog, Context context, CancellationToken token)
{
    dialog.Line1 = $"Uninstalling {Constants.AppName}";
    dialog.Line2 = "Removing shortcut...";

    try
    {
        // Use Windows API to get the Start Menu folder
        Guid startMenuFolderId = new("625B53C3-AB48-4EC1-BA1F-A1EF4146FC19"); // FOLDERID_StartMenu
        Windows.Win32.Foundation.HRESULT hr = PInvoke.SHGetKnownFolderPath(
            in startMenuFolderId,
            Windows.Win32.UI.Shell.KNOWN_FOLDER_FLAG.KF_FLAG_DEFAULT,
            null,
            out Windows.Win32.Foundation.PWSTR pszPath
        );

        if (hr.Failed)
        {
            throw new InvalidOperationException($"Failed to retrieve Start Menu path. Error code: {hr}");
        }

        var startMenuPath = pszPath.ToString();
        if (string.IsNullOrEmpty(startMenuPath))
        {
            throw new InvalidOperationException("Retrieved Start Menu path is empty.");
        }

        var shortcutPath = Path.Combine(startMenuPath, "Programs", $"{Constants.AppName}.lnk");

        if (File.Exists(shortcutPath))
        {
            File.Delete(shortcutPath);
            dialog.Line2 = "Shortcut removed successfully.";
        }
        else
        {
            dialog.Line2 = "Shortcut not found.";
        }

        return ValueTask.FromResult(StepResult.Continue);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Failed to remove shortcut");
        dialog.Line2 = "Failed to remove shortcut.";
        dialog.Line3 = $"Error details: {ex.Message}";
        return ValueTask.FromResult(StepResult.Continue); // Continue anyway, as this is not critical
    }
}

#endregion