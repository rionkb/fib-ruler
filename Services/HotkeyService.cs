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
    private bool _editLockRegistered;
    private bool _escapeRegistered;

    public event EventHandler? EditLockHotkeyPressed;

    public event EventHandler? EscapePressed;

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

        _editLockRegistered = NativeMethods.RegisterHotKey(
            _windowHandle,
            NativeMethods.HotkeyId,
            NativeMethods.ModControl | NativeMethods.ModAlt | NativeMethods.ModNoRepeat,
            NativeMethods.VirtualKeyF);

        if (!_editLockRegistered)
        {
            int error = Marshal.GetLastWin32Error();
            Debug.WriteLine($"Fib Ruler: Ctrl+Alt+Fの登録に失敗しました。Win32エラー={error}");
        }

        return _editLockRegistered;
    }

    public bool SetEscapeEnabled(bool enabled)
    {
        if (_windowHandle == IntPtr.Zero)
        {
            return false;
        }

        if (!enabled)
        {
            if (_escapeRegistered)
            {
                if (!NativeMethods.UnregisterHotKey(_windowHandle, NativeMethods.EscapeHotkeyId))
                {
                    Debug.WriteLine($"Fib Ruler: Escホットキー解除に失敗しました。Win32エラー={Marshal.GetLastWin32Error()}");
                }

                _escapeRegistered = false;
            }

            return true;
        }

        if (_escapeRegistered)
        {
            return true;
        }

        _escapeRegistered = NativeMethods.RegisterHotKey(
            _windowHandle,
            NativeMethods.EscapeHotkeyId,
            0,
            NativeMethods.VirtualKeyEscape);
        if (!_escapeRegistered)
        {
            Debug.WriteLine($"Fib Ruler: 描画キャンセル用Escの登録に失敗しました。Win32エラー={Marshal.GetLastWin32Error()}");
        }

        return _escapeRegistered;
    }

    public void Dispose()
    {
        if (_editLockRegistered)
        {
            if (!NativeMethods.UnregisterHotKey(_windowHandle, NativeMethods.HotkeyId))
            {
                Debug.WriteLine($"Fib Ruler: ホットキー解除に失敗しました。Win32エラー={Marshal.GetLastWin32Error()}");
            }
            _editLockRegistered = false;
        }

        if (_escapeRegistered)
        {
            if (!NativeMethods.UnregisterHotKey(_windowHandle, NativeMethods.EscapeHotkeyId))
            {
                Debug.WriteLine($"Fib Ruler: Escホットキー解除に失敗しました。Win32エラー={Marshal.GetLastWin32Error()}");
            }

            _escapeRegistered = false;
        }

        _source?.RemoveHook(WindowMessageHook);
        _source = null;
        _windowHandle = IntPtr.Zero;
    }

    private IntPtr WindowMessageHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == NativeMethods.WmHotKey)
        {
            switch (wParam.ToInt32())
            {
                case NativeMethods.HotkeyId:
                    EditLockHotkeyPressed?.Invoke(this, EventArgs.Empty);
                    handled = true;
                    break;
                case NativeMethods.EscapeHotkeyId:
                    EscapePressed?.Invoke(this, EventArgs.Empty);
                    handled = true;
                    break;
            }
        }

        return IntPtr.Zero;
    }
}
