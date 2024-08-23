using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace cbuild
{
    /// <summary>
    /// Provides functionality to validate digital signatures of files across different platforms.
    /// </summary>
    /// <remarks>
    /// This class offers methods to check if a file is digitally signed, supporting both Windows-specific 
    /// and cross-platform verification approaches. It uses WinVerifyTrust on Windows and PeNet for 
    /// cross-platform verification of Portable Executable (PE) files.
    /// </remarks>
    public partial class FileSignatureValidator
    {
        /// <summary>
        /// Validates whether the specified file is digitally signed.
        /// </summary>
        /// <remarks>
        /// This method checks for digital signatures on both Windows and non-Windows platforms.
        /// On Windows, it uses the WinVerifyTrust function.
        /// On other platforms, it uses PeNet to check for Authenticode signatures in PE files.
        /// </remarks>
        /// <param name="filePath">The full path to the file to be checked for a digital signature.</param>
        /// <returns>
        /// <c>true</c> if the file is digitally signed and the signature is valid; otherwise, <c>false</c>.
        /// </returns>
        /// <exception cref="FileNotFoundException">
        /// Thrown when the specified file does not exist at the given path.
        /// </exception>
        public static bool IsFileSigned(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("The specified file does not exist.", filePath);
            }

            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return IsFileSignedCrossPlatform(filePath);
                }
                else
                {
                    return IsFileSignedCrossPlatform(filePath);
                }
            }
            catch (CryptographicException)
            {
                // If a CryptographicException is thrown, the file is not signed or the signature is invalid
                return false;
            }
        }

        /// <summary>
        /// Checks if a file is digitally signed using Windows-specific APIs.
        /// </summary>
        /// <remarks>
        /// This method uses the WinVerifyTrust function to verify the digital signature of a file on Windows.
        /// It's called internally by <see cref="IsFileSigned"/> when running on Windows platforms.
        /// </remarks>
        /// <param name="filePath">The full path to the file to be checked for a digital signature.</param>
        /// <returns>
        /// <c>true</c> if the file is digitally signed and the signature is valid according to Windows trust verification; otherwise, <c>false</c>.
        /// </returns>
        private unsafe static bool IsFileSignedWindows(string filePath)
        {
            var fileInfo = new WINTRUST_FILE_INFO
            {
                cbStruct = (uint)Marshal.SizeOf(typeof(WINTRUST_FILE_INFO)),
                pcwszFilePath = filePath,
                hFile = IntPtr.Zero,
                pgKnownSubject = IntPtr.Zero
            };

            var guidAction = new Guid(WINTRUST_ACTION_GENERIC_VERIFY_V2);

            var trustData = new WINTRUST_DATA
            {
                cbStruct = (uint)Marshal.SizeOf(typeof(WINTRUST_DATA)),
                dwUIChoice = WTD_UI_NONE,
                fdwRevocationChecks = WTD_REVOKE_NONE,
                dwUnionChoice = WTD_CHOICE_FILE,
                pFile = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(WINTRUST_FILE_INFO)))
            };

            Marshal.StructureToPtr(fileInfo, trustData.pFile, false);

            try
            {
                uint result = WinVerifyTrust(IntPtr.Zero, ref guidAction, ref trustData);
                return result == 0;
            }
            finally
            {
                Marshal.FreeHGlobal(trustData.pFile);
            }
        }

        /// <summary>
        /// Checks if a file is digitally signed using a cross-platform approach.
        /// </summary>
        /// <remarks>
        /// This method uses PeNet to check for Authenticode signatures in PE (Portable Executable) files.
        /// It works on non-Windows platforms and provides a basic level of signature verification.
        /// It's called internally by <see cref="IsFileSigned"/> when running on non-Windows platforms.
        /// </remarks>
        /// <param name="filePath">The full path to the file to be checked for a digital signature.</param>
        /// <returns>
        /// <c>true</c> if the file is a valid PE file with an Authenticode signature that is trusted; otherwise, <c>false</c>.
        /// </returns>
        private static bool IsFileSignedCrossPlatform(string filePath)
        {
            if (!PeNet.PeFile.TryParse(filePath, out var pe))
            {
                return false;
            }
            if (pe is null)
            {
                return false;
            }
            return pe.IsAuthenticodeSigned && pe.IsTrustedAuthenticodeSignature && pe.HasValidAuthenticodeCertChain(true);
        }

        [LibraryImport("wintrust.dll", SetLastError = false)]
        private static partial uint WinVerifyTrust(
        IntPtr hwnd,
        ref Guid pgActionID,
        ref WINTRUST_DATA pWVTData
    );

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WINTRUST_FILE_INFO
        {
            public uint cbStruct;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string pcwszFilePath;
            public IntPtr hFile;
            public IntPtr pgKnownSubject;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WINTRUST_DATA
        {
            public uint cbStruct;
            public IntPtr pPolicyCallbackData;
            public IntPtr pSIPClientData;
            public uint dwUIChoice;
            public uint fdwRevocationChecks;
            public uint dwUnionChoice;
            public IntPtr pFile;
            public uint dwStateAction;
            public IntPtr hWVTStateData;
            public IntPtr pwszURLReference;
            public uint dwProvFlags;
            public uint dwUIContext;
        }

        private const uint WTD_UI_NONE = 2;
        private const uint WTD_REVOKE_NONE = 0;
        private const uint WTD_CHOICE_FILE = 1;

        private const string WINTRUST_ACTION_GENERIC_VERIFY_V2 =
            "{00AAC56B-CD44-11d0-8CC2-00C04FC295EE}";
    }
}