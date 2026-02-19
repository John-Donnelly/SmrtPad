using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using SmrtPad.Interop;
using System;
using System.Runtime.InteropServices;

namespace SmrtPad.Controls;

public sealed class NativeRichEditHost : Control
{
    private const string PartHost = "PART_Host";

    private ContentPresenter? _hostPresenter;
    private DesktopWindowXamlSource? _xamlSource;
    private IntPtr _xamlSourceHwnd;
    private IntPtr _richEditHwnd;
    private IntPtr _msfteditModule;

    public IntPtr RichEditHwnd => _richEditHwnd;

    public NativeRichEditHost()
    {
        DefaultStyleKey = typeof(NativeRichEditHost);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _hostPresenter = GetTemplateChild(PartHost) as ContentPresenter;

        EnsureXamlSource();
    }

    private void EnsureXamlSource()
    {
        if (_hostPresenter == null)
            return;

        if (_xamlSource != null)
            return;

        _xamlSource = new DesktopWindowXamlSource();
        var interop = (IDesktopWindowXamlSourceNative)_xamlSource;

        var parentHwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        interop.AttachToWindow(parentHwnd);
        interop.get_WindowHandle(out _xamlSourceHwnd);

        _hostPresenter.Content = _xamlSource;

        CreateRichEdit();
    }

    private void CreateRichEdit()
    {
        if (_richEditHwnd != IntPtr.Zero)
            return;

        _msfteditModule = Win32.LoadLibraryW("Msftedit.dll");

        // MSFTEDIT_CLASS is the v4.1 RichEdit (WordPad uses similar)
        _richEditHwnd = Win32.CreateWindowExW(
            0,
            "RICHEDIT50W",
            string.Empty,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_TABSTOP | Win32.WS_VSCROLL | Win32.ES_MULTILINE | Win32.ES_AUTOVSCROLL | Win32.ES_NOHIDESEL | Win32.ES_WANTRETURN,
            0,
            0,
            0,
            0,
            _xamlSourceHwnd,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero);

        if (_richEditHwnd == IntPtr.Zero)
            Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());

        ApplyDefaultFont();
        ResizeRichEdit();
    }

    private void ApplyDefaultFont()
    {
        var hFont = Win32.CreateFontW(
            -16,
            0,
            0,
            0,
            400,
            0,
            0,
            0,
            1,
            0,
            0,
            0,
            0,
            "Segoe UI");

        Win32.SendMessageW(_richEditHwnd, Win32.WM_SETFONT, hFont, new IntPtr(1));
    }

    private void ResizeRichEdit()
    {
        if (_richEditHwnd == IntPtr.Zero)
            return;

        var width = Math.Max(0, (int)ActualWidth);
        var height = Math.Max(0, (int)ActualHeight);
        Win32.MoveWindow(_richEditHwnd, 0, 0, width, height, true);
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => EnsureXamlSource();

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_richEditHwnd != IntPtr.Zero)
        {
            Win32.DestroyWindow(_richEditHwnd);
            _richEditHwnd = IntPtr.Zero;
        }

        if (_msfteditModule != IntPtr.Zero)
        {
            Win32.FreeLibrary(_msfteditModule);
            _msfteditModule = IntPtr.Zero;
        }

        _xamlSource = null;
        _xamlSourceHwnd = IntPtr.Zero;
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => ResizeRichEdit();

    // Minimal API used by MainWindow; more can be added as needed.
    public void ReplaceSelection(string text)
    {
        if (_richEditHwnd == IntPtr.Zero)
            return;

        var ptr = Marshal.StringToHGlobalUni(text);
        try
        {
            Win32.SendMessageW(_richEditHwnd, Win32.EM_REPLACESEL, new IntPtr(1), ptr);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    [ComImport, Guid("3cbcf1bf-2f76-4e9c-96ab-e84b37972554"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDesktopWindowXamlSourceNative
    {
        void AttachToWindow(IntPtr parentWnd);
        void get_WindowHandle(out IntPtr hwnd);
    }
}
