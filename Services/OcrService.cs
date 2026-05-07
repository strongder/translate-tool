using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace TranslatorApp.Services;

public static class OcrService
{
    public static async Task<string> ExtractTextAsync(Bitmap bitmap)
    {
        try
        {
            var engine = OcrEngine.TryCreateFromUserProfileLanguages()
                      ?? OcrEngine.TryCreateFromLanguage(new Language("en"));

            if (engine == null)
            {
                LogService.Error("Không khởi tạo được OcrEngine");
                return string.Empty;
            }

            using var ms = new MemoryStream();
            bitmap.Save(ms, ImageFormat.Png);
            ms.Position = 0;

            var decoder = await BitmapDecoder.CreateAsync(ms.AsRandomAccessStream());
            var softwareBitmap = await decoder.GetSoftwareBitmapAsync();

            // OCR yêu cầu Bgra8 hoặc Gray8
            if (softwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8)
            {
                softwareBitmap = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8);
            }

            var result = await engine.RecognizeAsync(softwareBitmap);

            var text = string.Join(" ", result.Lines.Select(l => l.Text));
            LogService.Info($"OCR: {text.Length} chars từ {bitmap.Width}x{bitmap.Height}px");
            return text;
        }
        catch (Exception ex)
        {
            LogService.Error("OCR thất bại", ex);
            return string.Empty;
        }
    }
}
