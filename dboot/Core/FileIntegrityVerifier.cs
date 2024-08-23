using System.Security.Cryptography;
using BootstrapperShared;
using dboot.SubSystem;
using Serilog;

namespace dboot.Core
{
    public static class FileIntegrityVerifier
    {
        public static async Task<bool> VerifyFileIntegrity(ProgressDialog dialog, string installDirectory, List<CatalogFile> files, CancellationToken cancellationToken)
        {
            dialog.Line1 = "Updating Dolus";
            dialog.Line2 = "Verifying installation...";
            dialog.Marquee = true;
            dialog.Maximum = 100;

            if (files == null || !files.Any())
            {
                Log.Error("No files found in the catalog for integrity verification.");
                return false;
            }

            int totalFiles = files.Count;
            int processedFiles = 0;

            foreach (var file in files)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return false;
                }

                string fullPath = Path.Combine(installDirectory, file.Path);

                if (!File.Exists(fullPath))
                {
                    Log.Warning("File not found: {FilePath}", fullPath);
                    dialog.Line3 = $"Missing file: {file.Path}";
                    return false;
                }

                string calculatedHash = await CalculateFileHashAsync(fullPath, cancellationToken);

                if (calculatedHash != file.Hash)
                {
                    Log.Error("Hash mismatch for file: {FilePath}", fullPath);
                    dialog.Line3 = $"Integrity check failed: {file.Path}";
                    return false;
                }

                processedFiles++;
                int percentage = (int)((double)processedFiles / totalFiles * 100);
                dialog.Value = percentage;
                dialog.Line3 = $"Verified {processedFiles}/{totalFiles} files";
            }

            dialog.Line2 = "Installation verified successfully.";
            dialog.Line3 = $"All {totalFiles} files are intact.";

            return true;
        }

        private static async Task<string> CalculateFileHashAsync(string filePath, CancellationToken cancellationToken)
        {
            using var sha256 = SHA256.Create();
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);

            byte[] hashBytes = await sha256.ComputeHashAsync(stream, cancellationToken);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
    }
}