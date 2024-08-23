using System.Runtime.InteropServices;
using Windows.Win32;
namespace dboot.SubSystem
{
    public static class ConsoleManager
    {
        private const int ERROR_ACCESS_DENIED = 5;
        private const int ERROR_INVALID_HANDLE = 6;

        public static bool SetupConsole()
        {
            // Try to attach to the parent process console
            if (PInvoke.AttachConsole(unchecked((uint)-1)))
            {
                return true;
            }

            int error = Marshal.GetLastWin32Error();
            return error switch
            {
                ERROR_ACCESS_DENIED => true,// We're already attached to a console
                ERROR_INVALID_HANDLE => (bool)PInvoke.AllocConsole(),// There's no console to attach to, so we'll create a new one
                _ => false,// Unexpected error or failed to allocate a new console
            };
        }

        public static void ReleaseConsole()
        {
            PInvoke.FreeConsole();
        }
    }
}