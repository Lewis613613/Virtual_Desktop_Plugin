using System;
using System.Runtime.InteropServices;

namespace VirtualDesktopPanel;

public static class NativeMethods
{
    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_SHOWWINDOW = 0x0040;

    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_EX_APPWINDOW = 0x00040000;
    public const int WS_EX_NOACTIVATE = 0x08000000;

    [DllImport("user32.dll")]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("shell32.dll")]
    public static extern uint SHGetFolderPath(IntPtr hwndOwner, int nFolder,
        IntPtr hToken, uint dwFlags, [MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszPath);

    [DllImport("gdi32.dll")]
    public static extern bool DeleteObject(IntPtr hObject);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    public static extern int SHFileOperation(ref SHFILEOPSTRUCT FileOp);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string pFrom;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string pTo;
        public uint fFlags;
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string lpszProgressTitle;
    }

    public const uint FO_DELETE = 0x0003;
    public const uint FOF_ALLOWUNDO = 0x0040;
    public const uint FOF_NOCONFIRMATION = 0x0010;

    /// <summary>
    /// Get the bounds of the taskbar and which edge of the screen it sits on.
    /// Returns (RECT, edge) where edge is "B"=bottom, "T"=top, "L"=left, "R"=right.
    /// </summary>
    public static (RECT Bounds, char Edge) GetTaskbarBounds()
    {
        var hWnd = FindWindow("Shell_TrayWnd", null);
        if (hWnd == IntPtr.Zero)
        {
            // Fallback: assume taskbar at bottom, 48px tall
            var screen = System.Windows.Forms.Screen.PrimaryScreen;
            if (screen == null)
                return (new RECT(), 'B');

            var wa = screen.WorkingArea;
            var sb = screen.Bounds;
            var rect = new RECT
            {
                Left = sb.Left,
                Top = wa.Bottom,
                Right = sb.Right,
                Bottom = sb.Bottom
            };

            if (wa.Top > sb.Top) return (rect, 'T');
            if (wa.Left > sb.Left) return (rect, 'L');
            if (wa.Right < sb.Right) return (rect, 'R');
            return (rect, 'B');
        }

        GetWindowRect(hWnd, out RECT tbRect);

        var screen2 = System.Windows.Forms.Screen.FromHandle(hWnd);
        char edge = 'B';
        if (tbRect.Top > screen2.Bounds.Top + screen2.Bounds.Height / 2) edge = 'B';
        else if (tbRect.Bottom < screen2.Bounds.Height / 2) edge = 'T';
        else if (tbRect.Left > screen2.Bounds.Width / 2) edge = 'R';
        else if (tbRect.Right < screen2.Bounds.Width / 2) edge = 'L';

        return (tbRect, edge);
    }

    /// <summary>
    /// Get the screen that contains the taskbar.
    /// </summary>
    public static System.Windows.Forms.Screen GetTaskbarScreen()
    {
        var hWnd = FindWindow("Shell_TrayWnd", null);
        if (hWnd != IntPtr.Zero)
            return System.Windows.Forms.Screen.FromHandle(hWnd);
        return System.Windows.Forms.Screen.PrimaryScreen
               ?? System.Windows.Forms.Screen.AllScreens[0];
    }
}
