namespace dboot.SubSystem;


public partial class ProgressDialog
{
    [Flags]
    internal enum CLSCTX : uint
    {
        CLSCTX_INPROC_SERVER = 0x1,
    }
}
