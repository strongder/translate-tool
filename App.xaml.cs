using System.Windows;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Extensions.Configuration;
using TranslatorApp.Services;
using TranslatorApp.Windows;

namespace TranslatorApp;

public partial class App : System.Windows.Application
{
    private NotifyIcon? _trayIcon;
    private HotkeyService? _hotkeyService;
    private GptService? _gptService;
    private IConfiguration? _config;

    private bool _isTranslating = false;
    private TranslationPopup? _currentPopup;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        _gptService = new GptService(_config);

        LogService.Info("App khởi động");
        SetupTrayIcon();
        SetupHotkey();
    }

    private void SetupTrayIcon()
    {
        _trayIcon = new NotifyIcon
        {
            Visible = true,
            Text = "Translator Tool (Alt+T / Alt+S)",
            Icon = SystemIcons.Information
        };

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("Thoát", null, (_, _) =>
        {
            _trayIcon.Visible = false;
            Shutdown();
        });

        _trayIcon.ContextMenuStrip = contextMenu;
        _trayIcon.DoubleClick += (_, _) =>
        {
            System.Windows.MessageBox.Show(
                "Translator Tool đang chạy.\n\n" +
                "• Alt+T  — Bôi đen text rồi nhấn để dịch\n" +
                "• Alt+S  — Kéo chọn vùng màn hình để dịch (OCR)",
                "Translator Tool",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        };
    }

    private void SetupHotkey()
    {
        _hotkeyService = new HotkeyService();
        _hotkeyService.HotkeyTextPressed += OnTextHotkeyPressed;
        _hotkeyService.HotkeyScreenPressed += OnScreenHotkeyPressed;

        var (text, screen) = _hotkeyService.Register();

        if (!text)
            LogService.Error("Không thể đăng ký hotkey Alt+T");
        else
            LogService.Info("Hotkey Alt+T đăng ký thành công");

        if (!screen)
            LogService.Error("Không thể đăng ký hotkey Alt+S");
        else
            LogService.Info("Hotkey Alt+S đăng ký thành công");

        if (!text && !screen)
        {
            System.Windows.MessageBox.Show(
                "Không thể đăng ký hotkey Alt+T và Alt+S.\nCó thể đang bị chiếm bởi ứng dụng khác.",
                "Lỗi Hotkey", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // ── Alt+T: dịch text bôi đen ──────────────────────────────────────────
    private async void OnTextHotkeyPressed()
    {
        var targetWindow = ClipboardService.CaptureTargetWindow();
        LogService.Info($"Hotkey kích hoạt | target hwnd={targetWindow}");

        if (_isTranslating) { LogService.Info("Bỏ qua — đang dịch"); return; }
        _isTranslating = true;

        try
        {
            var text = await ClipboardService.CopyAndGetText(targetWindow);
            LogService.Info($"Clipboard text: \"{(string.IsNullOrWhiteSpace(text) ? "(rỗng)" : text[..Math.Min(60, text.Length)])}\"");

            if (string.IsNullOrWhiteSpace(text)) return;

            await ShowTranslationAsync(text);
        }
        catch (Exception ex)
        {
            LogService.Error("Lỗi trong OnTextHotkeyPressed", ex);
            _currentPopup?.UpdateTranslation($"[Lỗi dịch] {ex.Message}");
        }
        finally
        {
            _isTranslating = false;
        }
    }

    // ── Alt+S: chụp vùng màn hình → OCR → dịch ───────────────────────────
    private async void OnScreenHotkeyPressed()
    {
        LogService.Info("Alt+S kích hoạt — mở overlay chụp màn hình");

        if (_isTranslating) { LogService.Info("Bỏ qua — đang dịch"); return; }
        _isTranslating = true;

        try
        {
            var overlay = new ScreenOverlay();
            bool captured = overlay.ShowDialog() == true;

            if (!captured || overlay.CaptureRegion.IsEmpty)
            {
                LogService.Info("Chụp màn hình bị hủy hoặc vùng quá nhỏ");
                return;
            }

            // Overlay đã đóng — chụp bitmap an toàn
            await Task.Delay(80); // chờ overlay biến mất hoàn toàn
            System.Drawing.Bitmap? bmp = null;
            try
            {
                var r = overlay.CaptureRegion;
                bmp = new System.Drawing.Bitmap(r.Width, r.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using var g = System.Drawing.Graphics.FromImage(bmp);
                g.CopyFromScreen(r.X, r.Y, 0, 0, r.Size, System.Drawing.CopyPixelOperation.SourceCopy);
            }
            catch (Exception ex)
            {
                LogService.Error("Chụp màn hình thất bại", ex);
                return;
            }

            var mousePos = System.Windows.Forms.Cursor.Position;
            _currentPopup?.ForceClose();
            _currentPopup = new TranslationPopup("Đang nhận dạng chữ...", mousePos, isLoading: true);
            _currentPopup.Show();

            var ocrText = await OcrService.ExtractTextAsync(bmp);
            bmp.Dispose();

            LogService.Info($"OCR text: \"{(string.IsNullOrWhiteSpace(ocrText) ? "(rỗng)" : ocrText[..Math.Min(60, ocrText.Length)])}\"");

            if (string.IsNullOrWhiteSpace(ocrText))
            {
                _currentPopup.UpdateTranslation("[Không nhận dạng được chữ trong vùng đã chọn]");
                return;
            }

            _currentPopup.UpdateTranslation("Đang dịch...");
            var translated = await _gptService!.TranslateAsync(ocrText);
            LogService.Info($"Kết quả dịch: \"{translated[..Math.Min(60, translated.Length)]}\"");
            _currentPopup.UpdateTranslation(translated);
        }
        catch (Exception ex)
        {
            LogService.Error("Lỗi trong OnScreenHotkeyPressed", ex);
            _currentPopup?.UpdateTranslation($"[Lỗi] {ex.Message}");
        }
        finally
        {
            _isTranslating = false;
        }
    }

    // ── Helper chung ──────────────────────────────────────────────────────
    private async Task ShowTranslationAsync(string text)
    {
        var mousePos = System.Windows.Forms.Cursor.Position;
        LogService.Info($"Mouse pos: {mousePos.X},{mousePos.Y}");

        _currentPopup?.ForceClose();
        _currentPopup = new TranslationPopup("Đang dịch...", mousePos, isLoading: true);
        _currentPopup.Show();
        LogService.Info("Popup hiển thị");

        var translated = await _gptService!.TranslateAsync(text);
        LogService.Info($"Kết quả dịch: \"{translated[..Math.Min(60, translated.Length)]}\"");
        _currentPopup.UpdateTranslation(translated);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        _hotkeyService?.Unregister();
        _hotkeyService?.Dispose();
        base.OnExit(e);
    }
}
