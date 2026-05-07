using System.Drawing;
using System.Drawing.Imaging;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TranslatorApp.Windows;

public partial class ScreenOverlay : Window
{
    private System.Windows.Point _startPoint;
    private bool _isDragging = false;

    // Vùng cần chụp (tọa độ vật lý trên màn hình) — App.xaml.cs chụp sau khi dialog đóng
    public System.Drawing.Rectangle CaptureRegion { get; private set; }

    private int _screenOffsetX;
    private int _screenOffsetY;

    public ScreenOverlay()
    {
        InitializeComponent();

        // Span toàn bộ virtual screen (tất cả màn hình ghép lại)
        // Phải set trước InitializeComponent xong nhưng sau khi Loaded để WPF đã layout
        Loaded += (_, _) => SpanAllScreens();

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
            }
        };
    }

    private void SpanAllScreens()
    {
        // System.Windows.Forms.SystemInformation.VirtualScreen = tổng bounds tất cả màn hình
        var vr = System.Windows.Forms.SystemInformation.VirtualScreen;

        // Lấy DPI scale của màn hình chính để chuyển pixel → WPF unit
        var dpi = VisualTreeHelper.GetDpi(this);
        double scaleX = dpi.DpiScaleX;
        double scaleY = dpi.DpiScaleY;

        Left   = vr.Left   / scaleX;
        Top    = vr.Top    / scaleY;
        Width  = vr.Width  / scaleX;
        Height = vr.Height / scaleY;

        // Lưu offset để tính tọa độ CopyFromScreen chính xác
        _screenOffsetX = vr.Left;
        _screenOffsetY = vr.Top;

        // Căn hint vào giữa phía trên
        Canvas.SetLeft(HintBorder, (Width - 320) / 2);
        Canvas.SetTop(HintBorder, 20);
    }

    private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        _startPoint = e.GetPosition(RootCanvas);
        _isDragging = true;

        Canvas.SetLeft(SelectionRect, _startPoint.X);
        Canvas.SetTop(SelectionRect, _startPoint.Y);
        SelectionRect.Width = 0;
        SelectionRect.Height = 0;
        SelectionRect.Visibility = Visibility.Visible;

        RootCanvas.CaptureMouse();
    }

    private void Canvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isDragging) return;

        var current = e.GetPosition(RootCanvas);
        var x = Math.Min(current.X, _startPoint.X);
        var y = Math.Min(current.Y, _startPoint.Y);
        var w = Math.Abs(current.X - _startPoint.X);
        var h = Math.Abs(current.Y - _startPoint.Y);

        Canvas.SetLeft(SelectionRect, x);
        Canvas.SetTop(SelectionRect, y);
        SelectionRect.Width = w;
        SelectionRect.Height = h;
    }

    private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging) return;
        _isDragging = false;
        RootCanvas.ReleaseMouseCapture();

        var endPoint = e.GetPosition(RootCanvas);

        // Tọa độ WPF (logical pixel) → tọa độ vật lý trên màn hình
        var dpi = VisualTreeHelper.GetDpi(this);
        double scaleX = dpi.DpiScaleX;
        double scaleY = dpi.DpiScaleY;

        var x = (int)(Math.Min(_startPoint.X, endPoint.X) * scaleX) + _screenOffsetX;
        var y = (int)(Math.Min(_startPoint.Y, endPoint.Y) * scaleY) + _screenOffsetY;
        var w = (int)(Math.Abs(endPoint.X - _startPoint.X) * scaleX);
        var h = (int)(Math.Abs(endPoint.Y - _startPoint.Y) * scaleY);

        if (w < 10 || h < 10)
        {
            // DialogResult = false tự đóng dialog
            DialogResult = false;
            return;
        }

        // Lưu vùng cần chụp, App.xaml.cs sẽ chụp sau khi dialog đóng
        CaptureRegion = new System.Drawing.Rectangle(x, y, w, h);
        DialogResult = true; // tự đóng window
    }
}
