using System;
using System.Runtime.InteropServices;

namespace SmrtPad.Interop;

internal static class Win32
{
    public const int WS_CHILD = 0x40000000;
    public const int WS_VISIBLE = 0x10000000;
    public const int WS_VSCROLL = 0x00200000;
    public const int WS_HSCROLL = 0x00100000;
    public const int WS_TABSTOP = 0x00010000;

    public const int ES_MULTILINE = 0x0004;
    public const int ES_AUTOVSCROLL = 0x0040;
    public const int ES_AUTOHSCROLL = 0x0080;
    public const int ES_NOHIDESEL = 0x0100;
    public const int ES_WANTRETURN = 0x1000;

    public const int WM_SIZE = 0x0005;
    public const int WM_SETFOCUS = 0x0007;
    public const int WM_KILLFOCUS = 0x0008;
    public const int WM_COMMAND = 0x0111;
    public const int WM_SETFONT = 0x0030;

    public const int EM_SETBKGNDCOLOR = 0x0443;
    public const int EM_SETCHARFORMAT = 0x0444;
    public const int EM_SETTEXTEX = 0x0461;
    public const int EM_STREAMIN = 0x044A;
    public const int EM_STREAMOUT = 0x044B;
    public const int EM_GETTEXTEX = 0x045E;
    public const int EM_SETSEL = 0x00B1;
    public const int EM_REPLACESEL = 0x00C2;

    public const int SCF_SELECTION = 0x0001;
    public const int SCF_ALL = 0x0004;

    public const int SF_TEXT = 0x0001;
    public const int SF_RTF = 0x0002;
    public const int SFF_PLAINRTF = 0x4000;

    public const int CFM_BOLD = 0x00000001;
    public const int CFM_ITALIC = 0x00000002;
    public const int CFM_UNDERLINE = 0x00000004;
    public const int CFM_STRIKEOUT = 0x00000008;

    public const int CFE_BOLD = 0x0001;
    public const int CFE_ITALIC = 0x0002;
    public const int CFE_UNDERLINE = 0x0004;
    public const int CFE_STRIKEOUT = 0x0008;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct CHARFORMAT2W
    {
        public uint cbSize;
        public uint dwMask;
        public uint dwEffects;
        public int yHeight;
        public int yOffset;
        public int crTextColor;
        public byte bCharSet;
        public byte bPitchAndFamily;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szFaceName;
        public ushort wWeight;
        public ushort sSpacing;
        public int crBackColor;
        public int lcid;
        public uint dwReserved;
        public short sStyle;
        public short wKerning;
        public byte bUnderlineType;
        public byte bAnimation;
        public byte bRevAuthor;
        public byte bReserved1;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    public delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr CreateWindowExW(
        int exStyle,
        string className,
        string windowName,
        int style,
        int x,
        int y,
        int width,
        int height,
        IntPtr parent,
        IntPtr menu,
        IntPtr instance,
        IntPtr param);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool MoveWindow(IntPtr hWnd, int x, int y, int width, int height, bool repaint);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SendMessageW(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetFocus(IntPtr hWnd);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr LoadLibraryW(string lpLibFileName);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool FreeLibrary(IntPtr hModule);

    [DllImport("user32.dll")]
    public static extern IntPtr DefWindowProcW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr CreateFontW(
        int cHeight,
        int cWidth,
        int cEscapement,
        int cOrientation,
        int cWeight,
        uint bItalic,
        uint bUnderline,
        uint bStrikeOut,
        uint iCharSet,
        uint iOutPrecision,
        uint iClipPrecision,
        uint iQuality,
        uint iPitchAndFamily,
        string pszFaceName);
}
