using System.Runtime.InteropServices;

namespace FibRuler.Interop;

internal static class NativeMethods
{
    internal const int GwlExStyle = -20;
    internal const int WsExTransparent = 0x00000020;
    internal const int WmHotKey = 0x0312;
    internal const uint ModAlt = 0x0001;
    internal const uint ModControl = 0x0002;
    internal const uint ModShift = 0x0004;
    internal const uint ModNoRepeat = 0x4000;
    internal const uint VirtualKeyEscape = 0x1B;
    internal const uint VirtualKeyA = 0x41;
    internal const uint VirtualKeyD = 0x44;
    internal const uint VirtualKeyF = 0x46;
    internal const int HotkeyId = 0xF1B;
    internal const int EscapeHotkeyId = 0xF1D;
    internal const int ToggleHotkeyId = 0xF1E;
    internal const int DrawHotkeyId = 0xF1F;
    internal const int DeleteHotkeyId = 0xF20;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    internal static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    internal static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);

}
