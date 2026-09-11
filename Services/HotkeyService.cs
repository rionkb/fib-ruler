using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using FibRuler.Interop;
using FibRuler.Models;

namespace FibRuler.Services;

public sealed class HotkeyService : IDisposable
{
    private HwndSource? _source;
    private IntPtr _windowHandle;
    private bool _editLockRegistered;
    private bool _escapeRegistered;
    private bool _toggleRegistered;
    private bool _drawRegistered;
    private bool _deleteRegistered;
    private ToggleShortcutSettings _toggleShortcut = ShortcutValidator.CreateDefault();

    public event EventHandler? EditLockHotkeyPressed;
    public event EventHandler? EscapePressed;
    public event EventHandler? ToggleHotkeyPressed;
    public event EventHandler? DrawHotkeyPressed;
    public event EventHandler? DeleteHotkeyPressed;
    public ToggleShortcutSettings ToggleShortcut => Clone(_toggleShortcut);
    public string ToggleShortcutDisplay => ShortcutValidator.Format(_toggleShortcut);

    public void SetToggleShortcut(ToggleShortcutSettings shortcut)
    {
        if (ShortcutValidator.TryParse(shortcut, out ToggleShortcutSettings parsed))
        {
            _toggleShortcut = parsed;
        }
    }

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
        if (!RegisterToggleShortcut(_toggleShortcut))
        {
            Debug.WriteLine("Fib Ruler: トグルホットキーの登録に失敗しました。設定画面から別のキーを選択してください。");
            return false;
        }

        return true;
    }

    public bool SetEditLockEnabled(bool enabled)
    {
        if (_windowHandle == IntPtr.Zero) return false;
        if (!enabled)
        {
            if (_editLockRegistered) NativeMethods.UnregisterHotKey(_windowHandle, NativeMethods.HotkeyId);
            _editLockRegistered = false;
            return true;
        }

        if (_editLockRegistered) return true;
        _editLockRegistered = NativeMethods.RegisterHotKey(_windowHandle, NativeMethods.HotkeyId,
            NativeMethods.ModControl | NativeMethods.ModAlt | NativeMethods.ModNoRepeat, NativeMethods.VirtualKeyF);
        if (!_editLockRegistered) Debug.WriteLine($"Fib Ruler: Ctrl+Alt+Fの登録に失敗しました。Win32エラー={Marshal.GetLastWin32Error()}");
        return _editLockRegistered;
    }

    public bool TryChangeToggleShortcut(ToggleShortcutSettings shortcut, out string error)
    {
        error = string.Empty;
        if (!ShortcutValidator.TryGetNative(shortcut, out uint modifiers, out uint virtualKey, out error)) return false;
        if (_windowHandle == IntPtr.Zero)
        {
            error = "ウィンドウが初期化されていません。";
            return false;
        }

        ToggleShortcutSettings previous = _toggleShortcut;
        if (_toggleRegistered) NativeMethods.UnregisterHotKey(_windowHandle, NativeMethods.ToggleHotkeyId);
        _toggleRegistered = NativeMethods.RegisterHotKey(_windowHandle, NativeMethods.ToggleHotkeyId,
            modifiers | NativeMethods.ModNoRepeat, virtualKey);
        if (!_toggleRegistered)
        {
            int win32Error = Marshal.GetLastWin32Error();
            _toggleRegistered = RegisterToggleShortcut(previous);
            error = $"ショートカットを登録できませんでした。Win32エラー={win32Error}";
            return false;
        }

        _toggleShortcut = Clone(shortcut);
        return true;
    }

    public bool SetEscapeEnabled(bool enabled)
    {
        if (_windowHandle == IntPtr.Zero) return false;
        if (!enabled)
        {
            if (_escapeRegistered) NativeMethods.UnregisterHotKey(_windowHandle, NativeMethods.EscapeHotkeyId);
            _escapeRegistered = false;
            return true;
        }

        if (_escapeRegistered) return true;
        _escapeRegistered = NativeMethods.RegisterHotKey(_windowHandle, NativeMethods.EscapeHotkeyId, 0, NativeMethods.VirtualKeyEscape);
        if (!_escapeRegistered) Debug.WriteLine($"Fib Ruler: Escホットキーの登録に失敗しました。Win32エラー={Marshal.GetLastWin32Error()}");
        return _escapeRegistered;
    }

    public bool SetEditActionsEnabled(bool enabled)
    {
        if (!enabled)
        {
            UnregisterEditActions();
            return true;
        }

        if (_windowHandle == IntPtr.Zero)
        {
            return false;
        }

        if (_drawRegistered && _deleteRegistered)
        {
            return true;
        }

        UnregisterEditActions();
        bool drawRegistered = NativeMethods.RegisterHotKey(
            _windowHandle,
            NativeMethods.DrawHotkeyId,
            NativeMethods.ModShift | NativeMethods.ModNoRepeat,
            NativeMethods.VirtualKeyA);
        if (!drawRegistered)
        {
            Debug.WriteLine($"Fib Ruler: Shift+Aの登録に失敗しました。Win32エラー={Marshal.GetLastWin32Error()}");
            return false;
        }

        bool deleteRegistered = NativeMethods.RegisterHotKey(
            _windowHandle,
            NativeMethods.DeleteHotkeyId,
            NativeMethods.ModShift | NativeMethods.ModNoRepeat,
            NativeMethods.VirtualKeyD);
        if (!deleteRegistered)
        {
            int win32Error = Marshal.GetLastWin32Error();
            NativeMethods.UnregisterHotKey(_windowHandle, NativeMethods.DrawHotkeyId);
            Debug.WriteLine($"Fib Ruler: Shift+Dの登録に失敗しました。Win32エラー={win32Error}");
            return false;
        }

        _drawRegistered = true;
        _deleteRegistered = true;
        return true;
    }

    public void Dispose()
    {
        if (_windowHandle != IntPtr.Zero)
        {
            if (_toggleRegistered) NativeMethods.UnregisterHotKey(_windowHandle, NativeMethods.ToggleHotkeyId);
            if (_editLockRegistered) NativeMethods.UnregisterHotKey(_windowHandle, NativeMethods.HotkeyId);
            if (_escapeRegistered) NativeMethods.UnregisterHotKey(_windowHandle, NativeMethods.EscapeHotkeyId);
            if (_drawRegistered) NativeMethods.UnregisterHotKey(_windowHandle, NativeMethods.DrawHotkeyId);
            if (_deleteRegistered) NativeMethods.UnregisterHotKey(_windowHandle, NativeMethods.DeleteHotkeyId);
        }

        _toggleRegistered = false;
        _editLockRegistered = false;
        _escapeRegistered = false;
        _drawRegistered = false;
        _deleteRegistered = false;
        _source?.RemoveHook(WindowMessageHook);
        _source = null;
        _windowHandle = IntPtr.Zero;
    }

    private bool RegisterToggleShortcut(ToggleShortcutSettings shortcut)
    {
        if (!ShortcutValidator.TryGetNative(shortcut, out uint modifiers, out uint virtualKey, out _)) return false;
        _toggleRegistered = NativeMethods.RegisterHotKey(_windowHandle, NativeMethods.ToggleHotkeyId,
            modifiers | NativeMethods.ModNoRepeat, virtualKey);
        return _toggleRegistered;
    }

    private IntPtr WindowMessageHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == NativeMethods.WmHotKey)
        {
            switch (wParam.ToInt32())
            {
                case NativeMethods.ToggleHotkeyId: ToggleHotkeyPressed?.Invoke(this, EventArgs.Empty); handled = true; break;
                case NativeMethods.HotkeyId: EditLockHotkeyPressed?.Invoke(this, EventArgs.Empty); handled = true; break;
                case NativeMethods.EscapeHotkeyId: EscapePressed?.Invoke(this, EventArgs.Empty); handled = true; break;
                case NativeMethods.DrawHotkeyId: DrawHotkeyPressed?.Invoke(this, EventArgs.Empty); handled = true; break;
                case NativeMethods.DeleteHotkeyId: DeleteHotkeyPressed?.Invoke(this, EventArgs.Empty); handled = true; break;
            }
        }
        return IntPtr.Zero;
    }

    private void UnregisterEditActions()
    {
        if (_windowHandle != IntPtr.Zero)
        {
            if (_drawRegistered) NativeMethods.UnregisterHotKey(_windowHandle, NativeMethods.DrawHotkeyId);
            if (_deleteRegistered) NativeMethods.UnregisterHotKey(_windowHandle, NativeMethods.DeleteHotkeyId);
        }

        _drawRegistered = false;
        _deleteRegistered = false;
    }

    private static ToggleShortcutSettings Clone(ToggleShortcutSettings shortcut) => new()
    {
        Key = shortcut.Key,
        Modifiers = shortcut.Modifiers.ToList()
    };
}
