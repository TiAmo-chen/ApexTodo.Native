using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace ApexTodo.Windows.Services;

public class HotkeyService : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const int HOTKEY_ID_CAPTURE = 9001;
    private const int HOTKEY_ID_TOGGLE_MOUSE = 9002;

    private HwndSource? _source;
    private IntPtr _windowHandle;

    public event Action? OnCaptureTriggered;
    public event Action? OnToggleMouseThrough;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public void Register(Window window)
    {
        var helper = new WindowInteropHelper(window);
        _windowHandle = helper.Handle;
        _source = HwndSource.FromHwnd(_windowHandle);
        _source?.AddHook(HwndHook);

        // Ctrl+Shift+A for capture
        RegisterHotKey(_windowHandle, HOTKEY_ID_CAPTURE, MOD_CTRL | MOD_SHIFT, 0x41);
        // Ctrl+Shift+Z for toggle mouse through
        RegisterHotKey(_windowHandle, HOTKEY_ID_TOGGLE_MOUSE, MOD_CTRL | MOD_SHIFT, 0x5A);
    }

    public void Unregister()
    {
        _source?.RemoveHook(HwndHook);
        UnregisterHotKey(_windowHandle, HOTKEY_ID_CAPTURE);
        UnregisterHotKey(_windowHandle, HOTKEY_ID_TOGGLE_MOUSE);
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            var id = wParam.ToInt32();
            if (id == HOTKEY_ID_CAPTURE)
            {
                OnCaptureTriggered?.Invoke();
                handled = true;
            }
            else if (id == HOTKEY_ID_TOGGLE_MOUSE)
            {
                OnToggleMouseThrough?.Invoke();
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    private const uint MOD_CTRL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;

    public void Dispose()
    {
        Unregister();
    }
}
