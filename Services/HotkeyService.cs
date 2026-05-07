using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace TranslatorApp.Services;

public class HotkeyService : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const int HOTKEY_ID_TEXT = 9001;   // Alt+T
    private const int HOTKEY_ID_SCREEN = 9002; // Alt+S
    private const uint MOD_ALT = 0x0001;
    private const uint VK_T = 0x54;
    private const uint VK_S = 0x53;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public event Action? HotkeyTextPressed;
    public event Action? HotkeyScreenPressed;

    private HwndSource? _messageWindow;
    private bool _registeredText = false;
    private bool _registeredScreen = false;

    public (bool text, bool screen) Register()
    {
        var parameters = new HwndSourceParameters("HotkeyReceiver")
        {
            HwndSourceHook = WndProc,
            ParentWindow = new IntPtr(-3), // HWND_MESSAGE
            Width = 0,
            Height = 0,
            PositionX = 0,
            PositionY = 0,
            WindowStyle = 0
        };

        _messageWindow = new HwndSource(parameters);
        _registeredText = RegisterHotKey(_messageWindow.Handle, HOTKEY_ID_TEXT, MOD_ALT, VK_T);
        _registeredScreen = RegisterHotKey(_messageWindow.Handle, HOTKEY_ID_SCREEN, MOD_ALT, VK_S);
        return (_registeredText, _registeredScreen);
    }

    public void Unregister()
    {
        if (_messageWindow == null) return;
        if (_registeredText)
        {
            UnregisterHotKey(_messageWindow.Handle, HOTKEY_ID_TEXT);
            _registeredText = false;
        }
        if (_registeredScreen)
        {
            UnregisterHotKey(_messageWindow.Handle, HOTKEY_ID_SCREEN);
            _registeredScreen = false;
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if (id == HOTKEY_ID_TEXT)
            {
                HotkeyTextPressed?.Invoke();
                handled = true;
            }
            else if (id == HOTKEY_ID_SCREEN)
            {
                HotkeyScreenPressed?.Invoke();
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        Unregister();
        _messageWindow?.Dispose();
    }
}
