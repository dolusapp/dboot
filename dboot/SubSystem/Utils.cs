using System.Runtime.InteropServices;
using Serilog;
using Windows.Wdk;

using Windows.Win32.System.SystemInformation;
namespace dboot.SubSystem
{
    public static class SystemUtils
    {
        public static bool IsWindowsBuild17134OrAbove()
        {
            const int minimumBuild = 17134;
            OSVERSIONINFOW osVersionInfo = new();
            osVersionInfo.dwOSVersionInfoSize = (uint)Marshal.SizeOf<OSVERSIONINFOW>();

            if (PInvoke.RtlGetVersion(ref osVersionInfo) == 0)
            {
                // Check if the OS is Windows 10 or higher and the build number is 17134 or higher
                if (osVersionInfo.dwMajorVersion == 10 && osVersionInfo.dwBuildNumber >= minimumBuild ||
                    osVersionInfo.dwMajorVersion > 10)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Generates a unique temporary file path including both the directory and the file name without creating the file.
        /// </summary>
        /// <returns>A string representing the full path of the temporary file.</returns>
        public static string GetTempFileName()
        {
            try
            {
                string tempFilePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                return tempFilePath;
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to generate a temporary file path.");
                throw;
            }
        }


        /// <summary>
        /// Checks if a directory does not exist or if it exists but is empty.
        /// </summary>
        /// <param name="directoryPath">The path of the directory to check.</param>
        /// <returns>
        /// True if the directory does not exist or if it exists and is empty, indicating that the installation is not present.
        /// False if the directory exists and contains files or subdirectories.
        /// </returns>
        public static bool DirectoryDoesNotExistOrIsEmpty(string? directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath))
            {
                Log.Error("Directory path cannot be null or empty.");
                throw new ArgumentException("Directory path cannot be null or empty.", nameof(directoryPath));
            }

            try
            {
                // Check if the directory does not exist
                if (!Directory.Exists(directoryPath))
                {
                    return true; // Directory does not exist
                }

                // Check if the directory is empty
                bool isEmpty = Directory.GetFiles(directoryPath).Length == 0 && Directory.GetDirectories(directoryPath).Length == 0;

                return isEmpty;
            }
            catch (UnauthorizedAccessException ex)
            {
                Log.Error(ex, "Access denied to the directory: {DirectoryPath}", directoryPath);
                throw new UnauthorizedAccessException($"Access denied to the directory: {directoryPath}", ex);
            }
            catch (PathTooLongException ex)
            {
                Log.Error(ex, "The path is too long: {DirectoryPath}", directoryPath);
                throw new PathTooLongException($"The path is too long: {directoryPath}", ex);
            }
            catch (DirectoryNotFoundException ex)
            {
                Log.Error(ex, "Directory not found: {DirectoryPath}", directoryPath);
                throw new DirectoryNotFoundException($"Directory not found: {directoryPath}", ex);
            }
            catch (IOException ex)
            {
                Log.Error(ex, "An I/O error occurred while accessing the directory: {DirectoryPath}", directoryPath);
                throw new IOException($"An I/O error occurred while accessing the directory: {directoryPath}", ex);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An unexpected error occurred while checking the directory: {DirectoryPath}", directoryPath);
                throw new Exception($"An unexpected error occurred while checking the directory: {directoryPath}", ex);
            }
        }


        public static bool IsMutexHeld(string appGuid)
        {
            if (string.IsNullOrWhiteSpace(appGuid))
            {
                throw new ArgumentException("appGuid cannot be null or empty", nameof(appGuid));
            }

            string mutexId = $"Global\\{{{appGuid}}}";
            Mutex? mutex = null;

            try
            {
                // Attempt to open the mutex without trying to create it
                mutex = Mutex.OpenExisting(mutexId);

                // If we reach here, the mutex exists. Let's check if it's held.
                bool acquired = mutex.WaitOne(TimeSpan.Zero);
                if (acquired)
                {
                    // We acquired the mutex, so it exists but wasn't actually held
                    mutex.ReleaseMutex();
                    return false;
                }
                else
                {
                    // We couldn't acquire the mutex, so it's truly held by another process
                    return true;
                }
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                // The mutex doesn't exist, which means it's not held
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                // Handle the case where we don't have permission to check the mutex
                Console.WriteLine("Warning: Insufficient permissions to check the mutex.");
                return false;
            }
            catch (Exception ex)
            {
                // Log any unexpected exceptions
                Console.WriteLine($"An error occurred while checking the mutex: {ex.Message}");
                return false;
            }
            finally
            {
                // Ensure the mutex is properly disposed if we opened it
                mutex?.Dispose();
            }
        }

        public static unsafe string? GetProgramFilesPath()
        {
            const int CSIDL_PROGRAM_FILES = 0x0026; // Program Files directory
            const uint SHGFP_TYPE_CURRENT = 0;      // Get current folder path
            const int MAX_PATH = 260;               // Maximum path length

            // Create a char array to hold the path
            char[] pathChars = new char[MAX_PATH];

            // Use an unsafe block to pin the char array and get a pointer to it
            unsafe
            {
                fixed (char* pPath = pathChars)
                {


                    // Call the SHGetFolderPath function
                    var result = Windows.Win32.PInvoke.SHGetFolderPath(CSIDL_PROGRAM_FILES, default, SHGFP_TYPE_CURRENT, pPath);

                    // Check the result
                    if (result.Succeeded)
                    {
                        // Convert the char array to a string and trim any null characters
                        return new string(pathChars).TrimEnd('\0');
                        //  Console.WriteLine("Program Files directory: " + path);
                    }
                    else
                    {
                        return null;
                        // Console.WriteLine("Failed to get Program Files directory. HRESULT: " + result.Value);
                    }
                }
            }
        }
    }
}