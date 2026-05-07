using System.Runtime.InteropServices;
using System.Windows;

namespace TranslatorApp.Services;

public static class ClipboardService
{
    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    private const byte VK_CONTROL = 0x11;
    private const byte VK_C = 0x43;
    private const byte VK_ALT = 0x12;
    private const byte VK_SHIFT = 0x10;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    private static string _lastText = string.Empty;
    private static string _lastTranslation = string.Empty;

    public static IntPtr CaptureTargetWindow() => GetForegroundWindow();

    // Toàn bộ chạy trên một STA thread — tránh await làm mất quyền SetForegroundWindow
    public static Task<string> CopyAndGetText(IntPtr targetWindow)
    {
        var tcs = new System.Threading.Tasks.TaskCompletionSource<string>();

        var thread = new System.Threading.Thread(() =>
        {
            uint targetThreadId = 0;
            uint currentThreadId = GetCurrentThreadId();
            bool attached = false;

            try
            {
                System.Windows.Forms.Clipboard.Clear();

                if (targetWindow != IntPtr.Zero)
                {
                    // AttachThreadInput cho phép SetForegroundWindow hoạt động dù bị Windows chặn
                    targetThreadId = GetWindowThreadProcessId(targetWindow, out _);
                    if (targetThreadId != 0 && targetThreadId != currentThreadId)
                    {
                        attached = AttachThreadInput(currentThreadId, targetThreadId, true);
                    }

                    SetForegroundWindow(targetWindow);
                    System.Threading.Thread.Sleep(100);
                }

                // Release Alt + Shift phòng trường hợp còn held từ hotkey
                keybd_event(VK_ALT, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                keybd_event(VK_SHIFT, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                System.Threading.Thread.Sleep(50);

                keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
                keybd_event(VK_C, 0, 0, UIntPtr.Zero);
                System.Threading.Thread.Sleep(100);
                keybd_event(VK_C, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

                // Polling tối đa 1.5 giây
                string result = string.Empty;
                for (int i = 0; i < 30; i++)
                {
                    System.Threading.Thread.Sleep(50);
                    var text = System.Windows.Forms.Clipboard.GetText();
                    if (!string.IsNullOrEmpty(text))
                    {
                        result = text;
                        break;
                    }
                }

                tcs.SetResult(result);
            }
            catch (Exception ex)
            {
                LogService.Error("CopyAndGetText thất bại", ex);
                tcs.SetResult(string.Empty);
            }
            finally
            {
                if (attached && targetThreadId != 0)
                    AttachThreadInput(currentThreadId, targetThreadId, false);
            }
        });

        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();

        return tcs.Task;
    }

    // Trả về true nếu text đã được dịch trước đó (cache hit)
    public static bool TryGetCached(string text, out string cached)
    {
        if (text == _lastText && !string.IsNullOrEmpty(_lastTranslation))
        {
            cached = _lastTranslation;
            return true;
        }
        cached = string.Empty;
        return false;
    }

    public static void SetCache(string text, string translation)
    {
        _lastText = text;
        _lastTranslation = translation;
    }
}
