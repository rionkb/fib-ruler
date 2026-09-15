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
    private bool _drawRegistered;
    private bool _deleteRegistered;
    private ToggleShortcutSettings _drawShortcut = ShortcutValidator.CreateDefaultDraw();
    private ToggleShortcutSettings _deleteShortcut = ShortcutValidator.CreateDefaultDelete();

    public event EventHandler? EditLockHotkeyPressed;
    public event EventHandler? EscapePressed;
    public event EventHandler? DrawHotkeyPressed;
    public event EventHandler? DeleteHotkeyPressed;
    public ToggleShortcutSettings DrawShortcut => Clone(_drawShortcut);
    public ToggleShortcutSettings DeleteShortcut => Clone(_deleteShortcut);
    public string DrawShortcutDisplay => ShortcutValidator.Format(_drawShortcut);
    public string DeleteShortcutDisplay => ShortcutValidator.Format(_deleteShortcut);

    public void SetEditShortcuts(ToggleShortcutSettings draw, ToggleShortcutSettings delete)
    {
        if (ShortcutValidator.TryParse(draw, out ToggleShortcutSettings parsedDraw)
            && ShortcutValidator.TryParse(delete, out ToggleShortcutSettings parsedDelete))
        {
            _drawShortcut = parsedDraw;
            _deleteShortcut = parsedDelete;
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

        return RegisterEditActions(_drawShortcut, _deleteShortcut);
    }

    public bool TryChangeEditShortcuts(
        ToggleShortcutSettings draw,
        ToggleShortcutSettings delete,
        out string error)
    {
        error = string.Empty;
        if (!ShortcutValidator.TryValidateEditShortcuts(draw, delete, out error))
        {
            return false;
        }

        ToggleShortcutSettings previousDraw = _drawShortcut;
        ToggleShortcutSettings previousDelete = _deleteShortcut;
        bool wasEnabled = _drawRegistered || _deleteRegistered;
        UnregisterEditActions();

        _drawShortcut = Clone(draw);
        _deleteShortcut = Clone(delete);
        if (!wasEnabled || RegisterEditActions(_drawShortcut, _deleteShortcut))
        {
            return true;
        }

        int win32Error = Marshal.GetLastWin32Error();
        _drawShortcut = previousDraw;
        _deleteShortcut = previousDelete;
        bool restored = RegisterEditActions(_drawShortcut, _deleteShortcut);
        error = restored
            ? $"ショートカットを登録できませんでした。Win32エラー={win32Error}"
            : "ショートカットを登録できず、以前のショートカットも復元できませんでした。";
        return false;
    }

    public void Dispose()
    {
        if (_windowHandle != IntPtr.Zero)
        {
            if (_editLockRegistered) NativeMethods.UnregisterHotKey(_windowHandle, NativeMethods.HotkeyId);
            if (_escapeRegistered) NativeMethods.UnregisterHotKey(_windowHandle, NativeMethods.EscapeHotkeyId);
            if (_drawRegistered) NativeMethods.UnregisterHotKey(_windowHandle, NativeMethods.DrawHotkeyId);
            if (_deleteRegistered) NativeMethods.UnregisterHotKey(_windowHandle, NativeMethods.DeleteHotkeyId);
        }

        _editLockRegistered = false;
        _escapeRegistered = false;
        _drawRegistered = false;
        _deleteRegistered = false;
        _source?.RemoveHook(WindowMessageHook);
        _source = null;
        _windowHandle = IntPtr.Zero;
    }

    private bool RegisterEditActions(ToggleShortcutSettings draw, ToggleShortcutSettings delete)
    {
        if (!ShortcutValidator.TryGetNative(draw, out uint drawModifiers, out uint drawKey, out string drawError))
        {
            Debug.WriteLine($"Fib Ruler: Drawショートカットが無効です。{drawError}");
            return false;
        }

        if (!ShortcutValidator.TryGetNative(delete, out uint deleteModifiers, out uint deleteKey, out string deleteError))
        {
            Debug.WriteLine($"Fib Ruler: Deleteショートカットが無効です。{deleteError}");
            return false;
        }

        bool drawRegistered = NativeMethods.RegisterHotKey(
            _windowHandle,
            NativeMethods.DrawHotkeyId,
            drawModifiers | NativeMethods.ModNoRepeat,
            drawKey);
        if (!drawRegistered)
        {
            Debug.WriteLine($"Fib Ruler: {ShortcutValidator.Format(draw)}の登録に失敗しました。Win32エラー={Marshal.GetLastWin32Error()}");
            return false;
        }

        bool deleteRegistered = NativeMethods.RegisterHotKey(
            _windowHandle,
            NativeMethods.DeleteHotkeyId,
            deleteModifiers | NativeMethods.ModNoRepeat,
            deleteKey);
        if (!deleteRegistered)
        {
            int win32Error = Marshal.GetLastWin32Error();
            NativeMethods.UnregisterHotKey(_windowHandle, NativeMethods.DrawHotkeyId);
            Debug.WriteLine($"Fib Ruler: {ShortcutValidator.Format(delete)}の登録に失敗しました。Win32エラー={win32Error}");
            return false;
        }

        _drawRegistered = true;
        _deleteRegistered = true;
        return true;
    }

    private IntPtr WindowMessageHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == NativeMethods.WmHotKey)
        {
            switch (wParam.ToInt32())
            {
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
