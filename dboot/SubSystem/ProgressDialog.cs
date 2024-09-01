using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace dboot.SubSystem
{

    /// <summary>
    /// Managed wrapper for the COM ProgressDialog component. Displays a 
    /// dialog box to track the progress of a long-running operation.
    /// </summary>
    public partial class ProgressDialog : Component
    {

        const string CLSID_ProgressDialog = "{F8383852-FCD3-11d1-A6B9-006097DF5BD4}";
        const string IDD_ProgressDialog = "EBBC7C04-315E-11d2-B62F-006097DF5BD4";


        private readonly ComWrappers _comWrappers;
        private readonly nint _dialogPointer;
        IProgressDialog _nativeProgressDialog;
        string _title;
        string _cancelMessage;
        string _line1;
        string _line2;
        string _line3;
        int _maximum;
        int _value;
        bool _compactPaths;
        PROGDLG _flags;
        ProgressDialogState _state;
        bool _autoClose;
        private HWND _progressBarHandle = HWND.Null;
        private HWND _parentHandle = HWND.Null;
        private HWND _progressHost = HWND.Null;
        private HWND _dialogHost = HWND.Null;
        private HWND _cancelButtonHandle = HWND.Null;


        internal HWND Handle => _progressBarHandle;

        /// <summary>
        /// Gets or sets whether the progress dialog is automatically closed when the Value property equals or exceeds the value of Maximum.
        /// </summary>
        [DefaultValue(true), Category("Behaviour"), Description("whether the progress dialog is automatically closed when the Value property equals or exceeds the value of Maximum.")]
        public bool AutoClose
        {
            get
            {
                return _autoClose;
            }
            set
            {
                _autoClose = value;
            }
        }
        /// <summary>
        /// Gets the current state of the progress dialog.
        /// </summary>
        [ReadOnly(true), Browsable(false)]
        public ProgressDialogState State
        {
            get
            {
                return _state;
            }
        }
        /// <summary>
        /// Gets or sets the title displayed on the progress dialog.
        /// </summary>
        [DefaultValue("Working..."), Category("Appearance"), Description("Indicates the title displayed on the progress dialog.")]
        public string Title
        {
            get
            {
                return _title;
            }
            set
            {
                if (_dialogHost.IsNull) return;
                _title = value;
                if (_nativeProgressDialog != null) _nativeProgressDialog.SetTitle(_title);
            }
        }
        /// <summary>
        /// Gets or sets the message to be displayed when the user clicks the cancel button on the progress dialog.
        /// </summary>
        [DefaultValue("Aborting..."), Category("Appearance"), Description("Indicates the message to be displayed when the user clicks the cancel button on the progress dialog.")]
        public string CancelMessage
        {
            get
            {
                return _cancelMessage;
            }
            set
            {
                if (_dialogHost.IsNull) return;
                _cancelMessage = value;
                if (_nativeProgressDialog != null) _nativeProgressDialog.SetCancelMsg(_cancelMessage, -1);
            }
        }
        /// <summary>
        /// Gets or sets whether to have path strings compacted if they are too large to fit on a line.
        /// </summary>
        [DefaultValue(false), Category("Appearance"), Description("Indicates whether to have path strings compacted if they are too large to fit on a line.")]
        public bool CompactPaths
        {
            get
            {
                return _compactPaths;
            }
            set
            {
                if (_dialogHost.IsNull) return;
                bool diff = _compactPaths != value;
                _compactPaths = value;

                if (diff && _nativeProgressDialog != null)
                {
                    _nativeProgressDialog.SetLine(1, _line1, _compactPaths, nint.Zero);
                    _nativeProgressDialog.SetLine(2, _line1, _compactPaths, nint.Zero);
                    _nativeProgressDialog.SetLine(3, _line1, _compactPaths, nint.Zero);
                }
            }
        }
        /// <summary>
        /// Gets or sets the text displayed on the first line of the progress dialog.
        /// </summary>
        [DefaultValue(""), Browsable(false)]
        public string Line1
        {
            get
            {
                return _line1;
            }
            set
            {
                if (_dialogHost.IsNull) return;
                if (_state == ProgressDialogState.Stopped)
                    throw new InvalidOperationException("Timer is not running.");
                else if (_nativeProgressDialog != null)
                    _nativeProgressDialog.SetLine(1, _line1 = value, _compactPaths, nint.Zero);
            }
        }
        /// <summary>
        /// Gets or sets the text displayed on the second line of the progress dialog.
        /// </summary>
        [DefaultValue(""), Browsable(false)]
        public string Line2
        {
            get
            {
                return _line2;
            }
            set
            {
                if (_dialogHost.IsNull) return;
                if (_state == ProgressDialogState.Stopped)
                    throw new InvalidOperationException("Timer is not running.");
                else if (_nativeProgressDialog != null)
                    _nativeProgressDialog.SetLine(2, _line2 = value, _compactPaths, nint.Zero);
            }
        }
        /// <summary>
        /// Gets or sets the text displayed on the third line of the progress dialog. This property cannot be set if the ShowTimeRemaining property is set to true.
        /// </summary>
        [DefaultValue(""), Browsable(false)]
        public string Line3
        {
            get
            {
                return _line3;
            }
            set
            {
                if (_dialogHost.IsNull) return;
                if (_state == ProgressDialogState.Stopped)
                    throw new InvalidOperationException("Timer is not running.");
                else if (_nativeProgressDialog != null)
                {
                    if (ShowTimeRemaining)
                        throw new InvalidOperationException("Line3 cannot be set if ShowTimeRemaining is set to true.");
                    else
                        _nativeProgressDialog.SetLine(3, _line3 = value, _compactPaths, nint.Zero);
                }
            }
        }
        /// <summary>
        /// Gets or sets the Value property will be equal to when the operation has completed.
        /// </summary>
        [DefaultValue(100), Category("Behaviour"), Description("Indicates what the Value property will be equal to when the operation has completed.")]
        public int Maximum
        {
            get
            {
                return _maximum;
            }
            set
            {
                _maximum = value;
                if (_state != ProgressDialogState.Stopped) UpdateProgress();
            }
        }
        /// <summary>
        /// Gets or sets a value indicating the proportion of the operation has been completed.
        /// </summary>
        [DefaultValue(0), Browsable(false)]
        public int Value
        {
            get
            {
                return _value;
            }
            set
            {
                _value = value;
                if (_state != ProgressDialogState.Stopped)
                {
                    UpdateProgress();
                    if (_autoClose && _value >= _maximum) Close();
                }
            }
        }
        /// <summary>
        /// Gets or sets whether the progress dialog box will be modal to the parent window. By default, a progress dialog box is modeless.
        /// </summary>
        [DefaultValue(false), Category("Behaviour"), Description("Indicates whether the progress dialog box will be modal to the parent window. By default, a progress dialog box is modeless.")]
        public bool Modal
        {
            get
            {
                return (_flags & PROGDLG.Modal) == PROGDLG.Modal;
            }
            set
            {
                if (value)
                    _flags |= PROGDLG.Modal;
                else
                    _flags &= ~PROGDLG.Modal;
            }
        }
        /// <summary>
        /// Gets or sets whether to automatically estimate the remaining time and display the estimate on line 3.
        /// </summary>
        [DefaultValue(true), Category("Behaviour"), Description("Automatically estimate the remaining time and display the estimate on line 3.")]
        public bool ShowTimeRemaining
        {
            get
            {
                return (_flags & PROGDLG.AutoTime) == PROGDLG.AutoTime;
            }
            set
            {
                if (value)
                {
                    _flags &= ~PROGDLG.NoTime;
                    _flags |= PROGDLG.AutoTime;
                }
                else
                {
                    _flags &= ~PROGDLG.AutoTime;
                    _flags |= PROGDLG.NoTime;
                }
            }
        }
        /// <summary>
        /// Gets or sets whether to display a minimize button on the dialog box's caption bar.
        /// </summary>
        [DefaultValue(true), Category("Appearance"), Description("Display a minimize button on the dialog box's caption bar.")]
        public bool MinimizeButton
        {
            get
            {
                return (_flags & PROGDLG.NoMinimize) != PROGDLG.NoMinimize;
            }
            set
            {
                if (value)
                    _flags &= ~PROGDLG.NoMinimize;
                else
                    _flags |= PROGDLG.NoMinimize;
            }
        }
        /// <summary>
        /// Gets or sets whether to display a progress bar on the dialog box.
        /// </summary>
        [DefaultValue(true), Category("Appearance"), Description("Display a progress bar on the dialog box.")]
        public bool ProgressBar
        {
            get
            {
                return (_flags & PROGDLG.NoProgressBar) != PROGDLG.NoProgressBar;
            }
            set
            {
                if (value)
                    _flags &= ~PROGDLG.NoProgressBar;
                else
                    _flags |= PROGDLG.NoProgressBar;
            }
        }
        /// <summary>
        /// Gets or sets whether the operation can be cancelled. You should always show a cancel button unless absolutely necessary
        /// </summary>
        [DefaultValue(true), Category("Behaviour"), Description("Indicates whether the operation can be cancelled. You should always show a cancel button unless absolutely necessary.")]
        public bool CancelButton
        {
            get
            {
                return (_flags & PROGDLG.NoCancel) != PROGDLG.NoCancel;
            }
            set
            {
                if (value)
                {
                    _flags &= ~PROGDLG.NoCancel;
                }
                else
                {
                    if (Environment.OSVersion.Version.Major < 6) throw new NotSupportedException("This option is only available on Windows Vista or greater.");
                    _flags |= PROGDLG.NoCancel;
                }
            }
        }
        /// <summary>
        /// Sets the progress bar to marquee mode. This causes the progress bar to scroll horizontally, similar to a marquee display. Use this when you wish to indicate that progress is being made, but the time required for the operation is unknown.
        /// </summary>
        [DefaultValue(false), Category("Behaviour"), Description("Sets the progress bar to marquee mode.")]
        public bool Marquee
        {
            get
            {
                return (_flags & PROGDLG.MarqueeProgress) == PROGDLG.MarqueeProgress;
            }
            set
            {
                if (_progressBarHandle.IsNull) return;
                if (value)
                {
                    if (Environment.OSVersion.Version.Major < 6) throw new NotSupportedException("This option is only available on Windows Vista or greater.");
                    _flags |= PROGDLG.MarqueeProgress;
                }
                else
                {
                    _flags &= ~PROGDLG.MarqueeProgress;
                }
                if (_state != ProgressDialogState.Stopped && !_progressBarHandle.IsNull)
                {
                    int style = (int)GetWindowLongPtr(_progressBarHandle, (int)GWL.GWL_STYLE);
                    if (value)
                    {
                        style |= (int)PBS.PBS_MARQUEE;
                        SetWindowLongPtr(_progressBarHandle, (int)GWL.GWL_STYLE, (IntPtr)style);
                        PInvoke.SendMessage(_progressBarHandle, PBM_SETMARQUEE, PInvoke.MAKEWPARAM(1, 0), 0);
                    }
                    else
                    {
                        style &= ~(int)PBS.PBS_MARQUEE;
                        SetWindowLongPtr(_progressBarHandle, (int)GWL.GWL_STYLE, (IntPtr)style);
                        PInvoke.SendMessage(_progressBarHandle, PBM_SETMARQUEE, PInvoke.MAKEWPARAM(0, 0), 0);
                        // Reset the range and position
                        PInvoke.SendMessage(_progressBarHandle, PBM_SETRANGE32, 0, _maximum);
                        PInvoke.SendMessage(_progressBarHandle, PBM_SETPOS, PInvoke.MAKEWPARAM((ushort)_value, 0), 0);
                    }

                    // Force redraw
                    RECT? rect = null;
                    PInvoke.InvalidateRect(_progressBarHandle, rect, true);
                    PInvoke.UpdateWindow(_progressBarHandle);

                    // Update progress immediately if switching to normal mode
                    if (!value)
                    {
                        UpdateProgress();
                    }

                    // Update the IProgressDialog interface
                    if (_nativeProgressDialog != null)
                    {
                        _nativeProgressDialog.SetProgress((uint)_value, (uint)_maximum);
                    }
                }
            }
        }

        public void SetCancelButtonText(string newText)
        {
            if (_cancelButtonHandle.IsNull)
            {
                return;
            }
            if (PInvoke.SetWindowText(_cancelButtonHandle, newText))
            {
                // Force the Cancel button to redraw
                RECT? rect = null;
                PInvoke.InvalidateRect(_cancelButtonHandle, rect, true);
                PInvoke.UpdateWindow(_cancelButtonHandle);
            }
            else
            {
                int error = Marshal.GetLastWin32Error();
                Console.WriteLine($"Failed to set Cancel button text via WM_SETTEXT. Error code: {error}");
            }
        }

        [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
        private static partial nint GetWindowLongPtr64(nint hWnd, int nIndex);

        [LibraryImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
        private static partial int GetWindowLong32(nint hWnd, int nIndex);

        public static nint GetWindowLongPtr(nint hWnd, int nIndex)
        {
            if (IntPtr.Size == 8)
                return GetWindowLongPtr64(hWnd, nIndex);
            else
                return GetWindowLong32(hWnd, nIndex);
        }

        [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
        private static partial nint SetWindowLongPtr64(nint hWnd, int nIndex, nint dwNewLong);

        [LibraryImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
        private static partial int SetWindowLong32(nint hWnd, int nIndex, int dwNewLong);

        public static nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong)
        {
            if (IntPtr.Size == 8)
                return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
            else
                return new nint(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
        }

        public enum GWL
        {
            GWL_WNDPROC = (-4),
            GWL_HINSTANCE = (-6),
            GWL_HWNDPARENT = (-8),
            GWL_STYLE = (-16),
            GWL_EXSTYLE = (-20),
            GWL_USERDATA = (-21),
            GWL_ID = (-12)
        }

        public enum PBS
        {
            PBS_MARQUEE = (8)
        }

        /// <summary>
        /// Gets a value indicating whether the user has cancelled the operation.
        /// </summary>
        [ReadOnly(true), Browsable(false)]
        public bool HasUserCancelled
        {
            get
            {
                if (_nativeProgressDialog != null)
                    return _nativeProgressDialog.HasUserCancelled();
                else
                    return false;
            }
        }

        private byte[]? _iconData;
        private DestroyIconSafeHandle? _icon = default;

        /// <summary>
        /// Gets or sets the path to the icon file to be displayed in the titlebar of the progress dialog.
        /// </summary>
        [DefaultValue(""), Category("Appearance"), Description("The path to the icon file to be displayed in the titlebar.")]
        public byte[]? IconData
        {
            get { return _iconData; }
            set
            {
                if (_iconData != value)
                {
                    _iconData = value;
                }
            }
        }

        /// <summary>
        /// Initialises a new instance of the ProgressDialog class, using default values.
        /// </summary>
        public ProgressDialog()
        {

            _comWrappers = new StrategyBasedComWrappers();

            // Create an instance of the COM object
            _dialogPointer = CreateComObject();

            _nativeProgressDialog = (IProgressDialog)_comWrappers.GetOrCreateObjectForComInstance(_dialogPointer, CreateObjectFlags.None);

            // default/initial values
            _autoClose = true;
            _state = ProgressDialogState.Stopped;
            _maximum = 100;
            _line1 = _line2 = _line3 = string.Empty;
            _flags = PROGDLG.Normal | PROGDLG.AutoTime;
            _title = "Working...";
            _cancelMessage = "Aborting...";
        }

        public void BringToFront()
        {
            if (PInvoke.BringWindowToTop(_dialogHost))
            {
                PInvoke.SetForegroundWindow(_dialogHost);
            }
        }


        private static DestroyIconSafeHandle LoadIconFromByteArray(byte[] iconData)
        {
            if (iconData == null || iconData.Length == 0)
            {
                throw new ArgumentException("Icon data is null or empty", nameof(iconData));
            }

            try
            {
                // Check if the data starts with the correct icon header
                if (iconData.Length < 6 || iconData[0] != 0 || iconData[1] != 0 || iconData[2] != 1 || iconData[3] != 0)
                {
                    throw new ArgumentException("Invalid icon format. Expected .ico file data.");
                }

                ushort iconCount = BitConverter.ToUInt16(iconData, 4);
                Debug.WriteLine($"Icon count: {iconCount}");

                int largestIconIndex = -1;
                int largestIconSize = 0;
                int largestIconOffset = 0;

                // Parse icon directory to find the largest icon
                for (int i = 0; i < iconCount; i++)
                {
                    int entryOffset = 6 + (i * 16); // 6 bytes for header, 16 bytes per entry
                    if (entryOffset + 16 > iconData.Length) break;

                    int width = iconData[entryOffset] == 0 ? 256 : iconData[entryOffset];
                    int height = iconData[entryOffset + 1] == 0 ? 256 : iconData[entryOffset + 1];
                    int size = BitConverter.ToInt32(iconData, entryOffset + 8);
                    int offset = BitConverter.ToInt32(iconData, entryOffset + 12);

                    Debug.WriteLine($"Icon {i}: {width}x{height}, Size: {size}, Offset: {offset}");

                    if (width * height > largestIconSize)
                    {
                        largestIconSize = width * height;
                        largestIconIndex = i;
                        largestIconOffset = offset;
                    }
                }

                if (largestIconIndex == -1)
                {
                    throw new ArgumentException("No valid icon found in the data");
                }

                Debug.WriteLine($"Selected largest icon: Index {largestIconIndex}, Size {largestIconSize}, Offset {largestIconOffset}");

                // Extract the largest icon's data
                int dataSize = iconData.Length - largestIconOffset;
                byte[] resourceData = new byte[dataSize];
                Array.Copy(iconData, largestIconOffset, resourceData, 0, dataSize);

                DestroyIconSafeHandle hIcon = PInvoke.CreateIconFromResourceEx(
                    new Span<byte>(resourceData),
                    true,
                    0x00030000, // MAKELONG(3, 0)
                    default,
                    default,
                    IMAGE_FLAGS.LR_DEFAULTCOLOR);

                if (hIcon.IsInvalid)
                {
                    int error = Marshal.GetLastWin32Error();
                    throw new Exception($"Failed to create icon. Error code: {error}, Icon data size: {resourceData.Length}");
                }

                return hIcon;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating icon: {ex}");
                throw new Exception($"Error creating icon from byte array. Details: {ex.Message}", ex);
            }
        }

        private const int MAX_WAIT_TIME_MS = 10000;
        private void ConfigureWindow()
        {
            try
            {
                // Load icon if data is provided
                if (IconData is not null)
                {
                    try
                    {
                        _icon = LoadIconFromByteArray(IconData);
                        if (_icon.IsInvalid)
                        {
                            throw new Win32Exception(Marshal.GetLastWin32Error());
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error loading icon: {ex.Message}");
                        // Continue execution even if icon loading fails
                    }
                }

                // Find and setup dialog window
                HWND dialogWindow = HWND.Null;
                var startTime = DateTime.Now;

                while (true)
                {
                    dialogWindow = PInvoke.FindWindow(null, _title);

                    if (!dialogWindow.IsNull &&
                        PInvoke.IsWindowVisible(dialogWindow) &&
                        PInvoke.IsWindowEnabled(dialogWindow))
                    {

                        // Set icon if loaded successfully
                        if (_icon is not null && !_icon.IsInvalid)
                        {
                            PInvoke.SendMessage(dialogWindow, PInvoke.WM_SETICON, new WPARAM(0), new LPARAM(_icon.DangerousGetHandle()));
                            PInvoke.SendMessage(dialogWindow, PInvoke.WM_SETICON, new WPARAM(1), new LPARAM(_icon.DangerousGetHandle()));
                        }

                        _dialogHost = dialogWindow;

                        // Find progress bar handle
                        FindProgressBarAndCancelButton(dialogWindow);
                        break;
                    }

                    // Check for timeout
                    if ((DateTime.Now - startTime).TotalMilliseconds > MAX_WAIT_TIME_MS)
                    {
                        throw new TimeoutException("Failed to find or initialize the dialog window within the specified timeout.");
                    }

                    Thread.Sleep(100); // Short delay to prevent tight loop
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ConfigureWindow: {ex.Message}");
                // Consider logging the full exception details for debugging
                throw; // Re-throw the exception to be handled by the caller
            }
        }


        private unsafe void FindProgressBarAndCancelButton(HWND parentHandle)
        {
            // Step 1: Find DirectUIHWND window that is a child of the parentHandle
            HWND directUIHWNDHandle = PInvoke.FindWindowEx(parentHandle, HWND.Null, "DirectUIHWND", null);
            if (directUIHWNDHandle.IsNull)
            {
                Console.WriteLine("DirectUIHWND handle not found");
                return;
            }

            Console.WriteLine("Found DirectUIHWND handle");

            // First pass: Find the progress bar
            FindProgressBar(directUIHWNDHandle);

            // Second pass: Find the cancel button
            FindCancelButton(directUIHWNDHandle);

            if (_progressBarHandle.IsNull)
            {
                Console.WriteLine("Progress bar handle not found");
            }

            if (_cancelButtonHandle.IsNull)
            {
                Console.WriteLine("Cancel button handle not found");
            }
        }


        private unsafe void FindProgressBar(HWND directUIHWNDHandle)
        {
            HWND ctrlNotifySinkHandle = PInvoke.FindWindowEx(directUIHWNDHandle, HWND.Null, "CtrlNotifySink", null);
            while (!ctrlNotifySinkHandle.IsNull)
            {
                Console.WriteLine($"Searching for progress bar in CtrlNotifySink handle: {ctrlNotifySinkHandle.Value}");

                _progressBarHandle = PInvoke.FindWindowEx(ctrlNotifySinkHandle, HWND.Null, "msctls_progress32", null);
                if (!_progressBarHandle.IsNull)
                {
                    _parentHandle = directUIHWNDHandle;
                    _progressHost = ctrlNotifySinkHandle;
                    Console.WriteLine($"Found progress bar handle: {_progressBarHandle.Value}");
                    return;
                }

                ctrlNotifySinkHandle = PInvoke.FindWindowEx(directUIHWNDHandle, ctrlNotifySinkHandle, "CtrlNotifySink", null);
            }
        }

        private unsafe void FindCancelButton(HWND directUIHWNDHandle)
        {
            HWND ctrlNotifySinkHandle = PInvoke.FindWindowEx(directUIHWNDHandle, HWND.Null, "CtrlNotifySink", null);
            while (!ctrlNotifySinkHandle.IsNull)
            {
                Console.WriteLine($"Searching for cancel button in CtrlNotifySink handle: {ctrlNotifySinkHandle.Value}");

                HWND buttonHandle = PInvoke.FindWindowEx(ctrlNotifySinkHandle, HWND.Null, "Button", null);
                while (!buttonHandle.IsNull)
                {
                    Console.WriteLine($"Found a Button handle: {buttonHandle.Value}");

                    int textLength = PInvoke.GetWindowTextLength(buttonHandle);
                    if (textLength > 0)
                    {
                        int bufferSize = textLength + 1;
                        fixed (char* captionChars = new char[bufferSize])
                        {
                            if (PInvoke.GetWindowText(buttonHandle, captionChars, bufferSize) > 0)
                            {
                                string caption = new string(captionChars);
                                Console.WriteLine($"Button text: {caption}");

                                if (caption.Equals("Cancel", StringComparison.OrdinalIgnoreCase))
                                {
                                    _cancelButtonHandle = buttonHandle;
                                    Console.WriteLine($"Found Cancel button handle: {_cancelButtonHandle.Value}");
                                    return;
                                }
                            }
                            else
                            {
                                int error = Marshal.GetLastWin32Error();
                                if (error != 0)
                                {
                                    Console.WriteLine($"Warning: Failed to get window text. Error code: {error}");
                                }
                            }
                        }
                    }

                    buttonHandle = PInvoke.FindWindowEx(ctrlNotifySinkHandle, buttonHandle, "Button", null);
                }

                ctrlNotifySinkHandle = PInvoke.FindWindowEx(directUIHWNDHandle, ctrlNotifySinkHandle, "CtrlNotifySink", null);
            }
        }


        [Flags]
        private enum SetWindowPosFlags : uint
        {
            SWP_NOSIZE = 0x0001,
            SWP_NOMOVE = 0x0002,
            SWP_NOZORDER = 0x0004,
            SWP_NOREDRAW = 0x0008,
            SWP_NOACTIVATE = 0x0010,
            SWP_FRAMECHANGED = 0x0020,  // The frame changed: send WM_NCCALCSIZE
            SWP_SHOWWINDOW = 0x0040,
            SWP_HIDEWINDOW = 0x0080,
            SWP_NOCOPYBITS = 0x0100,
            SWP_NOOWNERZORDER = 0x0200,  // Don't do owner Z ordering
            SWP_NOSENDCHANGING = 0x0400,  // Don't send WM_WINDOWPOSCHANGING
        }


        private const int GWL_STYLE = -16;
        private const int PBS_MARQUEE = 0x08;
        private const int PBM_SETMARQUEE = 0x400 + 10;
        private const int PBM_SETRANGE = 0x0401;
        private const int PBM_SETPOS = 0x0402;
        private const int PBM_DELTAPOS = 0x0403;
        private const int PBM_SETSTEP = 0x0404;
        private const int PBM_STEPIT = 0x0405;
        private const int PBM_SETRANGE32 = 0x0406;
        private const int PBM_GETRANGE = 0x0407;
        private const int PBM_GETPOS = 0x0408;
        private const int PBM_SETBARCOLOR = 0x0409;
        private const int PBM_SETBKCOLOR = 0x2001;
        private const int PBM_GETSTEP = 0x040D;
        private const int PBM_GETBKCOLOR = 0x040E;
        private const int PBM_GETBARCOLOR = 0x040F;
        private const int PBM_SETSTATE = 0x0410;
        private const int PBM_GETSTATE = 0x0411;

        // Progress Bar States
        private const int PBST_NORMAL = 0x0001;
        private const int PBST_ERROR = 0x0002;
        private const int PBST_PAUSED = 0x0003;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(nint hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(nint hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(nint hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool InvalidateRect(nint hWnd, IntPtr lpRect, bool bErase);

        private nint CreateComObject()
        {
            Guid clsid = new Guid(CLSID_ProgressDialog);
            Guid iid = typeof(IProgressDialog).GUID;
            int hr = Ole32.CoCreateInstance(ref clsid, IntPtr.Zero, (uint)CLSCTX.CLSCTX_INPROC_SERVER, ref iid, out nint ptr);
            if (hr != 0)
            {
                Marshal.ThrowExceptionForHR(hr);
            }
            return ptr;
        }

        /// <summary>
        /// Initialises a new instance of the ProgressDialog class and adds it to the specified IContainer.
        /// </summary>
        /// <param name="container"></param>
        public ProgressDialog(IContainer container)
            : this()
        {
            container.Add(this);
        }

        /// <summary>
        /// Updates the progress displayed on the dialog box.
        /// </summary>
        private void UpdateProgress()
        {
            if (_nativeProgressDialog != null && _state != ProgressDialogState.Stopped)
            {
                _nativeProgressDialog.SetProgress((uint)_value, (uint)_maximum);

                if (!_progressBarHandle.IsNull && !Marquee)
                {
                    // without this the progress bar will NOT update after dialoging the marquee
                    PInvoke.SendMessage(_progressBarHandle, PBM_SETPOS, PInvoke.MAKEWPARAM((ushort)_value, 0), 0);
                }
            }
        }

        /// <summary>
        /// Displays the progress dialog and starts the timer.
        /// </summary>
        public void Show()
        {
            Show(null);
        }

        /// <summary>
        /// Displays the progress dialog and starts the timer.
        /// </summary>
        /// <param name="parent">The dialog box's parent window.</param>
        internal void Show(HWND? parent)
        {
            if (_state != ProgressDialogState.Stopped) throw new InvalidOperationException("Timer is already running.");

            nint handle = parent == null ? nint.Zero : parent.Value;
            _nativeProgressDialog.SetTitle(_title);
            _nativeProgressDialog.SetCancelMsg(_cancelMessage, -1);
            if (ShowTimeRemaining) _nativeProgressDialog.SetLine(3, "Estimating time remaining...", false, nint.Zero);
            _nativeProgressDialog.StartProgressDialog(handle, -1, _flags, nint.Zero);

            _value = 0;
            _state = ProgressDialogState.Running;
            _nativeProgressDialog.Timer(PDTIMER.Reset, -1);
            // Load icon and start the icon set task
            ConfigureWindow();
        }

        /// <summary>
        /// Pauses the timer on the progress dialog.
        /// </summary>
        public void Pause()
        {
            if (_state == ProgressDialogState.Stopped) throw new InvalidOperationException("Timer is not running.");
            if (_state == ProgressDialogState.Running)
            {
                _nativeProgressDialog.Timer(PDTIMER.Pause, -1);
                _state = ProgressDialogState.Paused;
            }
        }

        /// <summary>
        /// Resumes the timer on the progress dialog.
        /// </summary>
        public void Resume()
        {
            if (_state != ProgressDialogState.Paused) throw new InvalidOperationException("Timer is not paused.");
            _nativeProgressDialog.Timer(PDTIMER.Resume, -1);
            _state = ProgressDialogState.Running;
        }

        /// <summary>
        /// Stops the timer and closes the progress dialog.
        /// </summary>
        public void Close()
        {
            if (_state != ProgressDialogState.Stopped)
            {
                _nativeProgressDialog.StopProgressDialog();
                _state = ProgressDialogState.Stopped;
            }

            CleanUp();
        }

        /// <summary>
        /// Releases the RCW to the native IProgressDialog component.
        /// </summary>
        private void CleanUp()
        {
            if (_nativeProgressDialog != null)
            {
                if (_state != ProgressDialogState.Stopped)
                {
                    try
                    {
                        _nativeProgressDialog.StopProgressDialog();
                    }
                    catch { }
                }

                //   Marshal.FinalReleaseComObject(_dialogPointer);
                _nativeProgressDialog = null;
            }

            _state = ProgressDialogState.Stopped;
        }

        /// <summary>
        /// Releases the RCW to the native IProgressDialog component.
        /// </summary>
        /// <param name="disposing"></param>
        protected override void Dispose(bool disposing)
        {
            CleanUp();
            base.Dispose(disposing);
        }

        #region COM Interop

        /// <summary>
        /// Flags that control the operation of the progress dialog box.
        /// </summary>
        [Flags]
        public enum PROGDLG : uint
        {
            /// <summary>
            /// Normal progress dialog box behavior.
            /// </summary>
            Normal = 0x00000000,
            /// <summary>
            /// The progress dialog box will be modal to the window specified by hwndParent. By default, a progress dialog box is modeless.
            /// </summary>
            Modal = 0x00000001,
            /// <summary>
            /// Automatically estimate the remaining time and display the estimate on line 3.
            /// </summary>
            /// <remarks>
            /// If this flag is set, IProgressDialog::SetLine can be used only to display text on lines 1 and 2.
            /// </remarks>
            AutoTime = 0x00000002,
            /// <summary>
            /// Do not show the "time remaining" text.
            /// </summary>
            NoTime = 0x00000004,
            /// <summary>
            /// Do not display a minimize button on the dialog box's caption bar.
            /// </summary>
            NoMinimize = 0x00000008,
            /// <summary>
            /// Do not display a progress bar.
            /// </summary>
            /// <remarks>
            /// Typically, an application can quantitatively determine how much of the operation remains and periodically pass that value to IProgressDialog::SetProgress. The progress dialog box uses this information to update its progress bar. This flag is typically set when the calling application must wait for an operation to finish, but does not have any quantitative information it can use to update the dialog box.
            /// </remarks>
            NoProgressBar = 0x00000010,
            /// <summary>
            /// Sets the progress bar to marquee mode.
            /// </summary>
            /// <remarks>
            /// This causes the progress bar to scroll horizontally, similar to a marquee display. Use this when you wish to indicate that progress is being made, but the time required for the operation is unknown.
            /// </remarks>
            MarqueeProgress = 0x00000020,
            /// <summary>
            /// Do not display a cancel button.
            /// </summary>
            /// <remarks>
            /// The operation cannot be canceled. Use this only when absolutely necessary.
            /// </remarks>
            NoCancel = 0x00000040
        }

        /// <summary>
        /// Flags that indicate the action to be taken by the ProgressDialog.SetTime() method.
        /// </summary>
        public enum PDTIMER : uint
        {
            /// <summary>
            /// Resets the timer to zero. Progress will be calculated from the time this method is called.
            /// </summary>
            Reset = 0x01,
            /// <summary>
            /// Progress has been suspended.
            /// </summary>
            Pause = 0x02,
            /// <summary>
            /// Progress has been resumed.
            /// </summary>
            Resume = 0x03
        }

        [GeneratedComInterface]
        [Guid(IDD_ProgressDialog)]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public partial interface IProgressDialog
        {

            /// <summary>
            /// Starts the progress dialog box.
            /// </summary>
            /// <param name="hwndParent">A handle to the dialog box's parent window.</param>
            /// <param name="punkEnableModless">Reserved. Set to null.</param>
            /// <param name="dwFlags">Flags that control the operation of the progress dialog box.</param>
            /// <param name="pvResevered">Reserved. Set to IntPtr.Zero</param>
            public void StartProgressDialog(nint hwndParent, nint punkEnableModless, PROGDLG dwFlags, nint pvResevered);

            /// <summary>
            /// Stops the progress dialog box and removes it from the screen.
            /// </summary>
            public void StopProgressDialog();

            /// <summary>
            /// Sets the title of the progress dialog box.
            /// </summary>
            /// <param name="pwzTitle">A pointer to a null-terminated Unicode string that contains the dialog box title.</param>
            public void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pwzTitle);

            /// <summary>
            /// Specifies an Audio-Video Interleaved (AVI) clip that runs in the dialog box. Note: Note  This method is not supported in Windows Vista or later versions.
            /// </summary>
            /// <param name="hInstAnimation">An instance handle to the module from which the AVI resource should be loaded.</param>
            /// <param name="idAnimation">An AVI resource identifier. To create this value, use the MAKEINTRESOURCE macro. The control loads the AVI resource from the module specified by hInstAnimation.</param>
            public void SetAnimation(nint hInstAnimation, ushort idAnimation);

            /// <summary>
            /// Checks whether the user has canceled the operation.
            /// </summary>
            /// <returns>TRUE if the user has cancelled the operation; otherwise, FALSE.</returns>
            /// <remarks>
            /// The system does not send a message to the application when the user clicks the Cancel button.
            /// You must periodically use this function to poll the progress dialog box object to determine
            /// whether the operation has been canceled.
            /// </remarks>
            [PreserveSig]
            [return: MarshalAs(UnmanagedType.Bool)]
            public bool HasUserCancelled();

            /// <summary>
            /// Updates the progress dialog box with the current state of the operation.
            /// </summary>
            /// <param name="dwCompleted">An application-defined value that indicates what proportion of the operation has been completed at the time the method was called.</param>
            /// <param name="dwTotal">An application-defined value that specifies what value dwCompleted will have when the operation is complete.</param>
            public void SetProgress(uint dwCompleted, uint dwTotal);

            /// <summary>
            /// Updates the progress dialog box with the current state of the operation.
            /// </summary>
            /// <param name="ullCompleted">An application-defined value that indicates what proportion of the operation has been completed at the time the method was called.</param>
            /// <param name="ullTotal">An application-defined value that specifies what value ullCompleted will have when the operation is complete.</param>
            public void SetProgress64(ulong ullCompleted, ulong ullTotal);

            /// <summary>
            /// Displays a message in the progress dialog.
            /// </summary>
            /// <param name="dwLineNum">The line number on which the text is to be displayed. Currently there are three lines—1, 2, and 3. If the PROGDLG_AUTOTIME flag was included in the dwFlags parameter when IProgressDialog::StartProgressDialog was called, only lines 1 and 2 can be used. The estimated time will be displayed on line 3.</param>
            /// <param name="pwzString">A null-terminated Unicode string that contains the text.</param>
            /// <param name="fCompactPath">TRUE to have path strings compacted if they are too large to fit on a line. The paths are compacted with PathCompactPath.</param>
            /// <param name="pvResevered"> Reserved. Set to IntPtr.Zero.</param>
            /// <remarks>This function is typically used to display a message such as "Item XXX is now being processed." typically, messages are displayed on lines 1 and 2, with line 3 reserved for the estimated time.</remarks>
            public void SetLine(uint dwLineNum, [MarshalAs(UnmanagedType.LPWStr)] string pwzString, [MarshalAs(UnmanagedType.VariantBool)] bool fCompactPath, nint pvResevered);

            /// <summary>
            /// Sets a message to be displayed if the user cancels the operation.
            /// </summary>
            /// <param name="pwzCancelMsg">A pointer to a null-terminated Unicode string that contains the message to be displayed.</param>
            /// <param name="pvResevered">Reserved. Set to NULL.</param>
            /// <remarks>Even though the user clicks Cancel, the application cannot immediately call
            /// IProgressDialog::StopProgressDialog to close the dialog box. The application must wait until the
            /// next time it calls IProgressDialog::HasUserCancelled to discover that the user has canceled the
            /// operation. Since this delay might be significant, the progress dialog box provides the user with
            /// immediate feedback by clearing text lines 1 and 2 and displaying the cancel message on line 3.
            /// The message is intended to let the user know that the delay is normal and that the progress dialog
            /// box will be closed shortly.
            /// It is typically is set to something like "Please wait while ...". </remarks>
            public void SetCancelMsg([MarshalAs(UnmanagedType.LPWStr)] string pwzCancelMsg, nint pvResevered);

            /// <summary>
            /// Resets the progress dialog box timer to zero.
            /// </summary>
            /// <param name="dwTimerAction">Flags that indicate the action to be taken by the timer.</param>
            /// <param name="pvResevered">Reserved. Set to NULL.</param>
            /// <remarks>
            /// The timer is used to estimate the remaining time. It is started when your application
            /// calls IProgressDialog::StartProgressDialog. Unless your application will start immediately,
            /// it should call Timer just before starting the operation.
            /// This practice ensures that the time estimates will be as accurate as possible. This method
            /// should not be called after the first call to IProgressDialog::SetProgress.</remarks>
            public void Timer(PDTIMER dwTimerAction, nint pvResevered);
        }

        #endregion
    }

    /// <summary>
    /// Represents the various states in which the ProgressDialog component can be.
    /// </summary>
    public enum ProgressDialogState
    {
        /// <summary>
        /// The progress dialog is not showing.
        /// </summary>
        Stopped,
        /// <summary>
        /// The progress dialog is showing and the timer is running.
        /// </summary>
        Running,
        /// <summary>
        /// The progress dialog is showing and the timer is paused.
        /// </summary>
        Paused
    }
}
