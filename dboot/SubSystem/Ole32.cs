using System.Runtime.InteropServices;

namespace dboot.SubSystem
{
    public partial class ProgressDialog
    {
        // P/Invoke definition for CoCreateInstance
        internal static class Ole32
        {
            [DllImport("ole32.dll")]
            public static extern int CoCreateInstance(ref Guid rclsid, IntPtr pUnkOuter, uint dwClsContext, [In] ref Guid riid, out IntPtr ppv);
        }
    }
}
