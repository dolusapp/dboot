using System.IO.Compression;
using Serilog;

namespace dboot.SubSystem
{
    public static class UpdatePatcher
    {
        public static void ExtractZip(string zipPath, string extractPath, IProgress<int> progress, CancellationToken cancellationToken)
        {
            string currentUpdaterPath = Path.GetFullPath(Environment.ProcessPath);
            string updaterName = Path.GetFileName(currentUpdaterPath);

            PurgeExistingFiles(extractPath, currentUpdaterPath);

            using var archive = ZipFile.OpenRead(zipPath);
            int totalEntries = archive.Entries.Count;

            for (int i = 0; i < totalEntries; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var entry = archive.Entries[i];
                string destinationPath = Path.GetFullPath(Path.Combine(extractPath, entry.FullName));

                if (IsUpdaterFile(destinationPath, updaterName))
                {
                    HandleUpdaterFile(entry, destinationPath, currentUpdaterPath);
                }
                else
                {
                    ExtractEntry(entry, destinationPath);
                }

                progress.Report(CalculateProgress(i + 1, totalEntries));
            }
        }

        private static void PurgeExistingFiles(string extractPath, string currentUpdaterPath)
        {
            foreach (var file in Directory.GetFiles(extractPath, "*", SearchOption.AllDirectories))
            {
                if (!file.Equals(currentUpdaterPath, StringComparison.OrdinalIgnoreCase))
                {
                    TryDeleteFile(file);
                }
            }

            foreach (var dir in Directory.GetDirectories(extractPath, "*", SearchOption.AllDirectories).Reverse())
            {
                TryDeleteDirectory(dir);
            }
        }

        private static bool IsUpdaterFile(string destinationPath, string updaterName) =>
            Path.GetFileName(destinationPath).Equals(updaterName, StringComparison.OrdinalIgnoreCase);

        private static void HandleUpdaterFile(ZipArchiveEntry entry, string destinationPath, string currentUpdaterPath)
        {
            if (destinationPath.Equals(currentUpdaterPath, StringComparison.OrdinalIgnoreCase))
            {
                PerformSelfUpdate(entry, currentUpdaterPath);
            }
            else
            {
                ExtractEntry(entry, destinationPath);
            }
        }

        private static void PerformSelfUpdate(ZipArchiveEntry entry, string currentUpdaterPath)
        {
            string tempUpdaterPath = Path.Combine(Path.GetTempPath(), $"temp_{Path.GetFileName(currentUpdaterPath)}");
            entry.ExtractToFile(tempUpdaterPath, overwrite: true);
            Log.Information("Extracted new updater to temporary location: {TempPath}", tempUpdaterPath);

            string backupUpdaterPath = Path.Combine(Path.GetDirectoryName(currentUpdaterPath)!, $"{Path.GetFileName(currentUpdaterPath)}.old");
            ReplaceCurrentUpdater(currentUpdaterPath, backupUpdaterPath, tempUpdaterPath);
        }

        private static void ReplaceCurrentUpdater(string currentUpdaterPath, string backupUpdaterPath, string tempUpdaterPath)
        {
            TryDeleteFile(backupUpdaterPath);
            File.Move(currentUpdaterPath, backupUpdaterPath);
            Log.Information("Moved current updater to backup: {BackupPath}", backupUpdaterPath);

            File.Move(tempUpdaterPath, currentUpdaterPath);
            Log.Information("Installed new updater: {UpdaterPath}", currentUpdaterPath);
        }

        private static void ExtractEntry(ZipArchiveEntry entry, string destinationPath)
        {
            if (entry.FullName.EndsWith("/"))
            {
                Directory.CreateDirectory(destinationPath);
                Log.Debug("Created directory: {Directory}", destinationPath);
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                entry.ExtractToFile(destinationPath, overwrite: true);
                Log.Debug("Extracted file: {File}", destinationPath);
            }
        }

        private static void TryDeleteFile(string file)
        {
            try
            {
                File.Delete(file);
                Log.Debug("Deleted file: {File}", file);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to delete file: {File}", file);
            }
        }

        private static void TryDeleteDirectory(string dir)
        {
            try
            {
                Directory.Delete(dir, recursive: false);
                Log.Debug("Deleted directory: {Directory}", dir);
            }
            catch (IOException)
            {
                Log.Debug("Skipped non-empty directory: {Directory}", dir);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to delete directory: {Directory}", dir);
            }
        }

        private static int CalculateProgress(int processedEntries, int totalEntries) =>
            (int)((double)processedEntries / totalEntries * 100);
    }
}
