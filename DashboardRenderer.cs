using System.Globalization;

namespace G15Dashboard;

internal static class DashboardRenderer
{
    private const int WidthPx = LcdDimensions.Width;
    private const int HeightPx = LcdDimensions.Height;
    private const int BarX = 50;
    private const int BarWidth = 80;

    internal static Bitmap Render(Stats s, double fps, DateTime? timestamp = null)
    {
        var b = new Bitmap(WidthPx, HeightPx, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        try { RenderInto(b, s, fps, timestamp); return b; }
        catch { b.Dispose(); throw; }
    }

    internal static void RenderInto(Bitmap b, Stats s, double fps, DateTime? timestamp = null)
    {
        if (b.Width != LcdDimensions.Width || b.Height != LcdDimensions.Height)
            throw new ArgumentException("Das Dashboard-Bild muss 160 × 43 Pixel groß sein.", nameof(b));
        using var g = Graphics.FromImage(b);
        g.Clear(Color.Black);
        var white = Brushes.White;

        var now = timestamp ?? DateTime.Now;
        DrawText(g, now.ToString("yyyy.MM.dd", CultureInfo.InvariantCulture), 2, 1, white);
        string clockText = now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        DrawText(g, clockText, BarX + (BarWidth + 1 - TextWidth(clockText)) / 2, 1, white);
        DrawFps(g, white, fps);
        DrawPercentRow(g, white, 9, "RAM", Percentage(s.RamUsed, s.RamTotal));
        DrawPercentRow(g, white, 17, "VRAM", Percentage(s.VramUsed, s.VramTotal));
        DrawPercentRow(g, white, 25, "CPU", s.CpuLoad, s.CpuTemp);
        DrawPercentRow(g, white, 33, "GPU", s.GpuLoad, s.GpuTemp);
    }

    private static void DrawFps(Graphics g, Brush white, double fps)
    {
        if (!IsValidReading(fps)) return;
        const int rightColumnX = 134;
        const int rightColumnWidth = 24;
        DrawText(g, "FPS", rightColumnX + (rightColumnWidth - TextWidth("FPS")) / 2, 1, white);
        string fpsText = fps >= 1_000_000 ? fps.ToString("0E0", CultureInfo.InvariantCulture) : Fmt(fps, "");
        int fpsScaleX = TextWidth(fpsText) * 2 <= rightColumnWidth ? 2 : 1;
        int fpsWidth = fpsText.Length * 4 * fpsScaleX - fpsScaleX;
        DrawText(g, fpsText, rightColumnX + (rightColumnWidth - fpsWidth) / 2, 9, white, fpsScaleX, 3);
    }

    private static bool IsValidReading(double value) => double.IsFinite(value) && value >= 0;

    private static string Fmt(double value, string suffix) =>
        IsValidReading(value) ? value.ToString("0", CultureInfo.InvariantCulture) + suffix : "--";

    private static int TextWidth(string text) => Math.Max(0, text.Length * 4 - 1);

    internal static double Percentage(double used, double total) =>
        double.IsFinite(used) && double.IsFinite(total) && used >= 0 && total > 0
            ? Math.Clamp(used / total * 100, 0, 100) : -1;

    private static void DrawPercentRow(Graphics g, Brush white, int y,
        string label, double value, double? temperature = null)
    {
        DrawText(g, label, 2, y + 1, white);
        DrawText(g, Fmt(value, "%"), 22, y + 1, white);
        DrawBar(g, white, BarX, y + 2, BarWidth, value);
        if (temperature is double degrees)
        {
            string temperatureText = Fmt(degrees, "°C");
            DrawText(g, temperatureText, WidthPx - 2 - TextWidth(temperatureText), y + 1, white);
        }
    }

    private static void DrawBar(Graphics g, Brush white, int x, int y, int width, double percent)
    {
        int filled = double.IsFinite(percent) ? (int)Math.Round(Math.Clamp(percent, 0, 100) / 100 * width) : 0;
        if (filled > 0) g.FillRectangle(white, x, y, filled, 4);
        g.DrawRectangle(Pens.White, x, y, width, 4);
    }

    private static void DrawText(Graphics g, string text, int x, int y, Brush brush, int scaleX = 1, int scaleY = 1)
    {
        foreach (char ch in text)
        {
            ReadOnlySpan<byte> rows = Glyph(char.ToUpperInvariant(ch));
            for (int row = 0; row < 5; row++)
                for (int col = 0; col < 3; col++)
                    if ((rows[row] & (1 << (2 - col))) != 0)
                        g.FillRectangle(brush, x + col * scaleX, y + row * scaleY, scaleX, scaleY);
            x += 4 * scaleX;
        }
    }

    private static ReadOnlySpan<byte> Glyph(char c) => c switch
    {
        '0' => [7, 5, 5, 5, 7], '1' => [2, 6, 2, 2, 7], '2' => [7, 1, 7, 4, 7],
        '3' => [7, 1, 7, 1, 7], '4' => [5, 5, 7, 1, 1], '5' => [7, 4, 7, 1, 7],
        '6' => [7, 4, 7, 5, 7], '7' => [7, 1, 1, 1, 1], '8' => [7, 5, 7, 5, 7],
        '9' => [7, 5, 7, 1, 7], 'A' => [2, 5, 7, 5, 5], 'B' => [6, 5, 6, 5, 6],
        'C' => [3, 4, 4, 4, 3], 'D' => [6, 5, 5, 5, 6], 'E' => [7, 4, 6, 4, 7],
        'F' => [7, 4, 6, 4, 4], 'G' => [3, 4, 5, 5, 3], 'H' => [5, 5, 7, 5, 5],
        'I' => [7, 2, 2, 2, 7], 'J' => [1, 1, 1, 5, 2], 'K' => [5, 5, 6, 5, 5],
        'L' => [4, 4, 4, 4, 7], 'M' => [5, 7, 7, 5, 5], 'N' => [5, 7, 7, 7, 5],
        'O' => [2, 5, 5, 5, 2], 'P' => [6, 5, 6, 4, 4], 'Q' => [2, 5, 5, 7, 3],
        'R' => [6, 5, 6, 5, 5], 'S' => [3, 4, 2, 1, 6], 'T' => [7, 2, 2, 2, 2],
        'U' => [5, 5, 5, 5, 7], 'V' => [5, 5, 5, 5, 2], 'W' => [5, 5, 7, 7, 5],
        'X' => [5, 5, 2, 5, 5], 'Y' => [5, 5, 2, 2, 2], 'Z' => [7, 1, 2, 4, 7],
        ':' => [0, 2, 0, 2, 0], '.' => [0, 0, 0, 0, 2], '/' => [1, 1, 2, 4, 4],
        '%' => [5, 1, 2, 4, 5], '°' => [2, 5, 2, 0, 0],
        '-' => [0, 0, 7, 0, 0], ' ' => [0, 0, 0, 0, 0],
        _ => [7, 5, 5, 0, 2]
    };
}

