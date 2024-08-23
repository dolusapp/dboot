using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using static dboot.SubSystem.ProgressDialog;

namespace dboot.SubSystem
{
    public partial class ShellLinkWrapper : IDisposable
    {
        private static readonly ComWrappers _comWrappers = new StrategyBasedComWrappers();
        private readonly nint _shellLinkPointer;
        private readonly IShellLinkW _shellLink;
        private readonly nint _persistFilePointer;
        private readonly IPersistFile _persistFile;
        private bool _disposed = false;

        public ShellLinkWrapper()
        {
            _shellLinkPointer = CreateComObject(CLSID_ShellLink);
            _shellLink = (IShellLinkW)_comWrappers.GetOrCreateObjectForComInstance(_shellLinkPointer, CreateObjectFlags.None);

            _persistFilePointer = GetInterfacePointer(_shellLinkPointer, typeof(IPersistFile).GUID);
            _persistFile = (IPersistFile)_comWrappers.GetOrCreateObjectForComInstance(_persistFilePointer, CreateObjectFlags.None);
        }

        private static nint GetInterfacePointer(nint unknown, Guid iid)
        {
            int hr = Marshal.QueryInterface(unknown, ref iid, out nint ptr);
            if (hr != 0)
            {
                Marshal.ThrowExceptionForHR(hr);
            }
            return ptr;
        }

        public void SetTargetPath(string targetPath)
        {
            _shellLink.SetPath(targetPath);
        }


        public void SetIconLocation(string iconPath, int iconIndex)
        {
            _shellLink.SetIconLocation(iconPath, iconIndex);
        }


        public void SetDescription(string description)
        {
            _shellLink.SetDescription(description);
        }

        public void SetWorkingDirectory(string workingDirectory)
        {
            _shellLink.SetWorkingDirectory(workingDirectory);
        }

        public void Save(string shortcutPath)
        {
            _persistFile.Save(shortcutPath, true);
        }

        private static nint CreateComObject(string clsid)
        {
            Guid classGuid = new Guid(clsid);
            Guid interfaceGuid = typeof(IShellLinkW).GUID;
            int hr = Ole32.CoCreateInstance(ref classGuid, nint.Zero, (uint)CLSCTX.CLSCTX_INPROC_SERVER, ref interfaceGuid, out nint ptr);
            if (hr != 0)
            {
                Marshal.ThrowExceptionForHR(hr);
            }
            return ptr;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed state (managed objects)
                }

                // Free unmanaged resources (unmanaged objects) and override finalizer
                Marshal.Release(_persistFilePointer);
                Marshal.Release(_shellLinkPointer);
                _disposed = true;
            }
        }

        ~ShellLinkWrapper()
        {
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        private const string CLSID_ShellLink = "00021401-0000-0000-C000-000000000046";

        [GeneratedComInterface]
        [Guid("000214F9-0000-0000-C000-000000000046")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public partial interface IShellLinkW
        {
            void GetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile, int cch, nint pfd, uint fFlags);
            void GetIDList(out nint ppidl);
            void SetIDList(nint pidl);
            void GetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName, int cch);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir, int cch);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs, int cch);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out short pwHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int cch, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
            void Resolve(nint hwnd, uint fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }

        [GeneratedComInterface]
        [Guid("0000010b-0000-0000-C000-000000000046")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public partial interface IPersistFile
        {
            void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
            void IsDirty();
            void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
            void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
            void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        }
    }
}