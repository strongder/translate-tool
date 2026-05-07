using System.Windows;

namespace TranslatorApp.Windows;

public partial class TranslationPopup : Window
{
    private bool _isClosed = false;

    public TranslationPopup(string text, System.Drawing.Point mousePosition, bool isLoading = false, int autoCloseSeconds = 5)
    {
        InitializeComponent();
        TranslationText.Text = text;
        PositionNearMouse(mousePosition);
    }

    private void PositionNearMouse(System.Drawing.Point mousePos)
    {
        var screen = System.Windows.Forms.Screen.FromPoint(mousePos);
        var workArea = screen.WorkingArea;

        double left = mousePos.X + 12;
        double top = mousePos.Y + 12;

        double estimatedWidth = 400;
        double estimatedHeight = 120;

        if (left + estimatedWidth > workArea.Right)
            left = workArea.Right - estimatedWidth - 12;
        if (top + estimatedHeight > workArea.Bottom)
            top = mousePos.Y - estimatedHeight - 12;

        Left = left;
        Top = top;
    }

    public void UpdateTranslation(string text)
    {
        Dispatcher.Invoke(() =>
        {
            TranslationText.Text = text;
        });
    }

    public void ForceClose()
    {
        if (_isClosed) return;
        _isClosed = true;
        Dispatcher.Invoke(Close);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        ForceClose();
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        ForceClose();
    }
}
