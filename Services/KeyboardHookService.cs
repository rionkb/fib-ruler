using System.Diagnostics;
using System.Runtime.InteropServices;
using FibRuler.Interop;

namespace FibRuler.Services;

public sealed class KeyboardHookService : IDisposable
{
    private const int HookAction = 0;
    private const int MaximumTapMilliseconds = 350;

    private readonly NativeMethods.LowLevelKeyboardProc _hookCallback;
    private IntPtr _hookHandle;
    private bool _leftShiftDown;
    private bool _rightShiftDown;
    private bool _tapCancelled;
    private long _shiftDownTimestamp;

    public KeyboardHookService()
    {
        _hookCallback = HookCallback;
    }

    public event EventHandler? ShiftTapped;

    public bool Start()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            return true;
        }

        IntPtr moduleHandle = NativeMethods.GetModuleHandle(null);
        _hookHandle = NativeMethods.SetWindowsHookEx(
            NativeMethods.WhKeyboardLl,
            _hookCallback,
            moduleHandle,
            0);
        if (_hookHandle == IntPtr.Zero)
        {
            Debug.WriteLine($"Fib Ruler: Shift検出用キーボードフックの登録に失敗しました。Win32エラー={Marshal.GetLastWin32Error()}");
            return false;
        }

        return true;
    }

    public void Dispose()
    {
        if (_hookHandle == IntPtr.Zero)
        {
            return;
        }

        if (!NativeMethods.UnhookWindowsHookEx(_hookHandle))
        {
            Debug.WriteLine($"Fib Ruler: Shift検出用キーボードフックの解除に失敗しました。Win32エラー={Marshal.GetLastWin32Error()}");
        }

        _hookHandle = IntPtr.Zero;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode == HookAction)
        {
            try
            {
                int message = wParam.ToInt32();
                NativeMethods.LowLevelKeyboardHookStruct keyboardData =
                    Marshal.PtrToStructure<NativeMethods.LowLevelKeyboardHookStruct>(lParam);
                ProcessKeyboardMessage(message, keyboardData.VkCode);
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"Fib Ruler: キーボードフック処理に失敗しました。{exception}");
            }
        }

        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private void ProcessKeyboardMessage(int message, uint virtualKey)
    {
        bool isLeftShift = virtualKey == NativeMethods.VirtualKeyLShift;
        bool isRightShift = virtualKey == NativeMethods.VirtualKeyRShift;
        bool isShift = isLeftShift || isRightShift;
        bool isKeyDown = message is NativeMethods.WmKeyDown or NativeMethods.WmSysKeyDown;
        bool isKeyUp = message is NativeMethods.WmKeyUp or NativeMethods.WmSysKeyUp;

        if (isShift && isKeyDown)
        {
            bool wasShiftDown = _leftShiftDown || _rightShiftDown;
            if (isLeftShift)
            {
                _leftShiftDown = true;
            }

            if (isRightShift)
            {
                _rightShiftDown = true;
            }

            if (!wasShiftDown)
            {
                _tapCancelled = false;
                _shiftDownTimestamp = Stopwatch.GetTimestamp();
            }
            else
            {
                _tapCancelled = true;
            }

            return;
        }

        if (isShift && isKeyUp)
        {
            if (isLeftShift)
            {
                _leftShiftDown = false;
            }

            if (isRightShift)
            {
                _rightShiftDown = false;
            }

            if (!_leftShiftDown && !_rightShiftDown)
            {
                long elapsedMilliseconds = (long)Stopwatch.GetElapsedTime(_shiftDownTimestamp).TotalMilliseconds;
                bool isTap = !_tapCancelled && elapsedMilliseconds <= MaximumTapMilliseconds;
                _tapCancelled = false;
                if (isTap)
                {
                    RaiseShiftTapped();
                }
            }

            return;
        }

        if ((_leftShiftDown || _rightShiftDown) && isKeyDown)
        {
            _tapCancelled = true;
        }
    }

    private void RaiseShiftTapped()
    {
        try
        {
            ShiftTapped?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Fib Ruler: Shift単押し通知に失敗しました。{exception}");
        }
    }
}
