using System.Runtime.InteropServices;

namespace FibRuler.Interop;

internal static class NativeMethods
{
    internal const int GwlExStyle = -20;
    internal const int WsExTransparent = 0x00000020;
    internal const int WmHotKey = 0x0312;
    internal const uint ModAlt = 0x0001;
    internal const uint ModControl = 0x0002;
    internal const uint ModNoRepeat = 0x4000;
    internal const uint VirtualKeyEscape = 0x1B;
    internal const uint VirtualKeyF = 0x46;
    internal const int HotkeyId = 0xF1B;
    internal const int EscapeHotkeyId = 0xF1D;
    internal const int ToggleHotkeyId = 0xF1E;

    internal const int WhKeyboardLl = 13;
    internal const int WmKeyDown = 0x0100;
    internal const int WmKeyUp = 0x0101;
    internal const int WmSysKeyDown = 0x0104;
    internal const int WmSysKeyUp = 0x0105;
    internal const uint VirtualKeyLShift = 0xA0;
    internal const uint VirtualKeyRShift = 0xA1;

    [StructLayout(LayoutKind.Sequential)]
    internal struct LowLevelKeyboardHookStruct
    {
        internal uint VkCode;
        internal uint ScanCode;
        internal uint Flags;
        internal uint Time;
        internal UIntPtr DwExtraInfo;
    }

    internal delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    internal static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    internal static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr SetWindowsHookEx(
        int idHook,
        LowLevelKeyboardProc callback,
        IntPtr hMod,
        uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    internal static extern IntPtr CallNextHookEx(
        IntPtr hhk,
        int nCode,
        IntPtr wParam,
        IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);

}
