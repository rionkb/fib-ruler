using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using FibRuler.Interop;

namespace FibRuler.Services;

public static class WindowInteropService
{
    public static void SetClickThrough(Window window, bool enabled)
    {
        IntPtr handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            throw new InvalidOperationException("WPFウィンドウのハンドルを取得できませんでした。");
        }

        IntPtr currentStyle = NativeMethods.GetWindowLongPtr(handle, NativeMethods.GwlExStyle);
        int error = Marshal.GetLastWin32Error();
        if (currentStyle == IntPtr.Zero && error != 0)
        {
            throw new Win32Exception(error, "ウィンドウスタイルを取得できませんでした。");
        }

        long style = currentStyle.ToInt64();
        long transparentFlag = NativeMethods.WsExTransparent;
        long updatedStyle = enabled ? style | transparentFlag : style & ~transparentFlag;
        if (updatedStyle == style)
        {
            return;
        }

        IntPtr result = NativeMethods.SetWindowLongPtr(
            handle,
            NativeMethods.GwlExStyle,
            new IntPtr(updatedStyle));
        if (result == IntPtr.Zero && Marshal.GetLastWin32Error() != 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "ウィンドウスタイルを更新できませんでした。");
        }
    }
}
