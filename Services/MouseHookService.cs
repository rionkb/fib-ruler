using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using FibRuler.Interop;

namespace FibRuler.Services;

public sealed class MouseHookService : IDisposable
{
    private readonly NativeMethods.LowLevelMouseProc _hookProcedure;
    private IntPtr _hookHandle;
    private int _drawingModeEnabled;
    private int _gestureActive;
    private bool _disposed;

    public MouseHookService()
    {
        _hookProcedure = HookCallback;
    }

    public event EventHandler<MouseHookEventArgs>? DrawingStarted;

    public event EventHandler<MouseHookEventArgs>? DrawingMoved;

    public event EventHandler<MouseHookEventArgs>? DrawingCompleted;

    public bool Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_hookHandle != IntPtr.Zero)
        {
            return true;
        }

        _hookHandle = NativeMethods.SetWindowsHookEx(
            NativeMethods.WhMouseLl,
            _hookProcedure,
            NativeMethods.GetModuleHandle(null),
            0);
        if (_hookHandle == IntPtr.Zero)
        {
            Debug.WriteLine($"Fib Ruler: マウスフック登録に失敗しました。Win32エラー={Marshal.GetLastWin32Error()}");
            return false;
        }

        return true;
    }

    public void SetDrawingModeEnabled(bool enabled)
    {
        if (_disposed)
        {
            return;
        }

        Volatile.Write(ref _drawingModeEnabled, enabled ? 1 : 0);
        if (!enabled)
        {
            Volatile.Write(ref _gestureActive, 0);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Volatile.Write(ref _drawingModeEnabled, 0);
        Volatile.Write(ref _gestureActive, 0);

        if (_hookHandle != IntPtr.Zero)
        {
            if (!NativeMethods.UnhookWindowsHookEx(_hookHandle))
            {
                Debug.WriteLine($"Fib Ruler: マウスフック解除に失敗しました。Win32エラー={Marshal.GetLastWin32Error()}");
            }

            _hookHandle = IntPtr.Zero;
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int message = wParam.ToInt32();
            bool gestureActive = Volatile.Read(ref _gestureActive) != 0;

            if (message == NativeMethods.WmLButtonDown
                && Volatile.Read(ref _drawingModeEnabled) != 0
                && !gestureActive
                && IsShiftPressed()
                && Interlocked.CompareExchange(ref _gestureActive, 1, 0) == 0)
            {
                Raise(DrawingStarted);
                return new IntPtr(1);
            }

            if (gestureActive && message == NativeMethods.WmMouseMove)
            {
                Raise(DrawingMoved);
                return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
            }

            if (gestureActive && message == NativeMethods.WmLButtonUp)
            {
                Volatile.Write(ref _gestureActive, 0);
                Raise(DrawingCompleted);
                return new IntPtr(1);
            }
        }

        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private void Raise(EventHandler<MouseHookEventArgs>? handler)
    {
        if (handler is null || !NativeMethods.GetCursorPos(out NativeMethods.NativePoint cursor))
        {
            return;
        }

        handler(this, new MouseHookEventArgs(new Point(cursor.X, cursor.Y)));
    }

    private static bool IsShiftPressed() =>
        (NativeMethods.GetAsyncKeyState(NativeMethods.VirtualKeyShift) & 0x8000) != 0;
}

public sealed class MouseHookEventArgs(Point screenPosition) : EventArgs
{
    public Point ScreenPosition { get; } = screenPosition;
}
