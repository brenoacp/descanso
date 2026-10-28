using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using Descanso.Core;

namespace Descanso.App;

/// <summary>
/// Folha de prévia do ícone da bandeja: o avanço do preenchimento em tamanho
/// real e ampliado, sobre barra clara e escura.
///
/// Serve para julgar o desenho em 16 px sem depender de olhar a barra de tarefas.
/// Gerada por <c>Descanso.exe --export-icon-preview &lt;arquivo.png&gt;</c>.
/// </summary>
internal static class IconPreview
{
    private const int Width = 800;
    private const int SectionHeight = 480;

    private static readonly double[] Fractions = { 1, 0.75, 0.5, 0.25, 0 };
    private static readonly string[] Labels = { "100%", "75%", "50%", "25%", "0%" };

    public static void Write(string path)
    {
        using var image = new Bitmap(Width, SectionHeight * 2, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(image);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.Clear(Color.White);

        var y = DrawSection(g, 0, "Barra de tarefas clara", Color.FromArgb(243, 243, 243), Color.FromArgb(28, 36, 39));
        DrawSection(g, y, "Barra de tarefas escura", Color.FromArgb(32, 32, 32), Color.FromArgb(236, 236, 236));

        image.Save(path, ImageFormat.Png);
    }

    private static int DrawSection(Graphics g, int top, string title, Color background, Color foreground)
    {
        using (var fill = new SolidBrush(background))
        {
            g.FillRectangle(fill, 0, top, Width, SectionHeight);
        }

        using var titleFont = new Font("Segoe UI", 10.5f, FontStyle.Bold);
        using var captionFont = new Font("Segoe UI", 8.5f);
        using var titleBrush = new SolidBrush(foreground);
        using var captionBrush = new SolidBrush(Color.FromArgb(190, foreground));

        g.DrawString(title, titleFont, titleBrush, 20, top + 14);

        var y = top + 44;
        y = DrawRow(g, y, 24, 3, TrayState.Counting, "24 px, ampliado 3x", captionFont, captionBrush);
        y = DrawRow(g, y, 16, 4, TrayState.Counting, "16 px, ampliado 4x", captionFont, captionBrush);
        y = DrawRealSize(g, y, captionFont, captionBrush);
        DrawPaused(g, y, captionFont, captionBrush);

        return top + SectionHeight;
    }

    private static int DrawRow(
        Graphics g,
        int top,
        int size,
        int scale,
        TrayState state,
        string caption,
        Font font,
        Brush brush)
    {
        g.DrawString(caption, font, brush, 20, top);

        var imageTop = top + 20;
        var cell = size * scale;

        var interpolation = g.InterpolationMode;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;

        for (var i = 0; i < Fractions.Length; i++)
        {
            using var bitmap = IconRenderer.Draw(new TrayStatus(state, Fractions[i]), size);
            var x = 20 + (i * (cell + 16));

            g.DrawImage(bitmap, new Rectangle(x, imageTop, cell, cell));
            g.DrawString(Labels[i], font, brush, x, imageTop + cell + 2);
        }

        g.InterpolationMode = interpolation;
        g.PixelOffsetMode = PixelOffsetMode.Default;

        return imageTop + cell + 30;
    }

    private static int DrawRealSize(Graphics g, int top, Font font, Brush brush)
    {
        g.DrawString("Tamanho real", font, brush, 20, top);

        var y = top + 20;
        foreach (var size in new[] { 16, 24 })
        {
            var x = 20;
            for (var i = 0; i < Fractions.Length; i++)
            {
                using var bitmap = IconRenderer.Draw(new TrayStatus(TrayState.Counting, Fractions[i]), size);
                g.DrawImageUnscaled(bitmap, x, y);
                x += size + 14;
            }

            y += size + 8;
        }

        return y + 12;
    }

    private static void DrawPaused(Graphics g, int top, Font font, Brush brush)
    {
        g.DrawString("Pausado ou fora do expediente", font, brush, 20, top);

        var imageTop = top + 20;
        var items = new[]
        {
            (State: TrayState.Paused, Remaining: 0.5),
            (State: TrayState.OffHours, Remaining: 0.75),
        };

        const int size = 24;
        const int scale = 3;

        var interpolation = g.InterpolationMode;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;

        for (var i = 0; i < items.Length; i++)
        {
            using var bitmap = IconRenderer.Draw(new TrayStatus(items[i].State, items[i].Remaining), size);
            g.DrawImage(bitmap, new Rectangle(20 + (i * ((size * scale) + 16)), imageTop, size * scale, size * scale));
        }

        g.InterpolationMode = interpolation;
        g.PixelOffsetMode = PixelOffsetMode.Default;
    }
}
