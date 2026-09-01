using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using FibRuler.Interop;

namespace FibRuler.Services;

public sealed class HotkeyService : IDisposable
{
    private HwndSource? _source;
    private IntPtr _windowHandle;
    private bool _registered;

    public event EventHandler? HotkeyPressed;

    public bool Register(Window window)
    {
        _windowHandle = new WindowInteropHelper(window).Handle;
        if (_windowHandle == IntPtr.Zero)
        {
            Debug.WriteLine("Fib Ruler: ホットキー登録失敗。ウィンドウハンドルがありません。");
            return false;
        }

        _source = HwndSource.FromHwnd(_windowHandle);
        _source?.AddHook(WindowMessageHook);

        _registered = NativeMethods.RegisterHotKey(
            _windowHandle,
            NativeMethods.HotkeyId,
            NativeMethods.ModControl | NativeMethods.ModAlt | NativeMethods.ModNoRepeat,
            NativeMethods.VirtualKeyF);

        if (!_registered)
        {
            int error = Marshal.GetLastWin32Error();
            Debug.WriteLine($"Fib Ruler: Ctrl+Alt+Fの登録に失敗しました。Win32エラー={error}");
            _source?.RemoveHook(WindowMessageHook);
        }

        return _registered;
    }

    public void Dispose()
    {
        if (_registered)
        {
            if (!NativeMethods.UnregisterHotKey(_windowHandle, NativeMethods.HotkeyId))
            {
                Debug.WriteLine($"Fib Ruler: ホットキー解除に失敗しました。Win32エラー={Marshal.GetLastWin32Error()}");
            }
            _registered = false;
        }

        _source?.RemoveHook(WindowMessageHook);
        _source = null;
        _windowHandle = IntPtr.Zero;
    }

    private IntPtr WindowMessageHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == NativeMethods.WmHotKey && wParam.ToInt32() == NativeMethods.HotkeyId)
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }

        return IntPtr.Zero;
    }
}
