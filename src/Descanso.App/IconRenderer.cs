using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using Descanso.Core;

namespace Descanso.App;

/// <summary>
/// Desenha o ícone da bandeja com o tempo restante do ciclo.
///
/// O ícone é um quadrado arredondado: o preenchimento teal sobe do rodapé e a
/// altura representa a fração restante (cheio no início, vazio na hora da pausa).
/// Pausado ou fora do expediente, o preenchimento fica cinza e congelado.
///
/// A conversão para <see cref="Icon"/> monta um ICO DIB de um único frame em
/// memória: <c>Bitmap.GetHicon()</c> perde o canal alfa e deixa a borda serrilhada.
/// </summary>
internal static class IconRenderer
{
    // Mesmas cores do ícone do executável (tools\make-icon.ps1), para que o
    // ícone cheio e o do arquivo sejam reconhecivelmente o mesmo.
    private static readonly Color FillTop = Color.FromArgb(255, 26, 187, 168);
    private static readonly Color FillBottom = Color.FromArgb(255, 10, 82, 104);

    // Slate intermediário: visível tanto na barra clara quanto na escura.
    private static readonly Color Track = Color.FromArgb(255, 91, 107, 114);
    private static readonly Color Idle = Color.FromArgb(255, 126, 143, 150);

    private static readonly Color Surface = Color.FromArgb(90, 255, 255, 255);
    private static readonly Color Outline = Color.FromArgb(46, 255, 255, 255);
    private static readonly Color Bars = Color.FromArgb(242, 255, 255, 255);

    public static Icon Render(TrayStatus status, int size)
    {
        using var bitmap = Draw(status, size);
        return ToIcon(bitmap);
    }

    internal static Bitmap Draw(TrayStatus status, int size)
    {
        var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);

        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        var radius = Math.Max(2f, size * 0.22f);
        using var shape = RoundedRect(0, 0, size, size, radius);

        using (var track = new SolidBrush(Track))
        {
            g.FillPath(track, shape);
        }

        var fraction = (float)Math.Clamp(status.Remaining, 0, 1);
        if (fraction > 0.01f)
        {
            var fillHeight = size * fraction;
            var top = size - fillHeight;
            var running = status.State == TrayState.Counting;
            var colorTop = running ? FillTop : Idle;
            var colorBottom = running ? FillBottom : Idle;

            using var brush = new LinearGradientBrush(
                new PointF(0, top),
                new PointF(0, size),
                colorTop,
                colorBottom);

            var state = g.Save();
            g.SetClip(shape);
            g.FillRectangle(brush, 0, top, size, fillHeight);

            // Linha de superfície: ajuda a ler o nível mesmo em 16 px.
            using (var surface = new Pen(Surface, Math.Max(1f, size * 0.04f)))
            {
                g.DrawLine(surface, 0, top + 0.5f, size, top + 0.5f);
            }

            g.Restore(state);
        }

        DrawPauseBars(g, size);

        // Contorno interno branco transparente: separa o quadrado de uma barra
        // clara sem escurecer o desenho.
        using (var outline = new Pen(Outline, Math.Max(1f, size * 0.03f)))
        using (var edge = RoundedRect(0.5f, 0.5f, size - 1, size - 1, Math.Max(1f, radius - 0.5f)))
        {
            g.DrawPath(outline, edge);
        }

        return bitmap;
    }

    private static void DrawPauseBars(Graphics g, int size)
    {
        var barWidth = size * 0.15f;
        var barHeight = size * 0.42f;
        var gap = size * 0.13f;
        var left = (size - (barWidth * 2 + gap)) / 2;
        var top = (size - barHeight) / 2;
        var barRadius = barWidth / 2;

        using var brush = new SolidBrush(Bars);
        using var first = RoundedRect(left, top, barWidth, barHeight, barRadius);
        using var second = RoundedRect(left + barWidth + gap, top, barWidth, barHeight, barRadius);

        g.FillPath(brush, first);
        g.FillPath(brush, second);
    }

    private static GraphicsPath RoundedRect(float x, float y, float width, float height, float radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;

        path.AddArc(x, y, diameter, diameter, 180, 90);
        path.AddArc(x + width - diameter, y, diameter, diameter, 270, 90);
        path.AddArc(x + width - diameter, y + height - diameter, diameter, diameter, 0, 90);
        path.AddArc(x, y + height - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();

        return path;
    }

    private static Icon ToIcon(Bitmap bitmap)
    {
        var size = bitmap.Width;
        var payload = BuildDib(bitmap);

        var stream = new MemoryStream(6 + 16 + payload.Length);
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write((ushort)0);   // reservado
            writer.Write((ushort)1);   // tipo: ícone
            writer.Write((ushort)1);   // um frame
            writer.Write((byte)(size >= 256 ? 0 : size));
            writer.Write((byte)(size >= 256 ? 0 : size));
            writer.Write((byte)0);     // cores da paleta
            writer.Write((byte)0);     // reservado
            writer.Write((ushort)1);   // planos
            writer.Write((ushort)32);  // bits por pixel
            writer.Write((uint)payload.Length);
            writer.Write(22u);         // deslocamento: 6 + 16
            writer.Write(payload);
        }

        stream.Position = 0;
        return new Icon(stream, size, size);
    }

    private static byte[] BuildDib(Bitmap bitmap)
    {
        var size = bitmap.Width;
        var data = bitmap.LockBits(
            new Rectangle(0, 0, size, size),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);

        try
        {
            var stride = data.Stride;
            var pixels = new byte[stride * size];
            Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);

            // Máscara AND: exigida pelo formato, toda zero porque a transparência
            // vem do alfa.
            var maskStride = (size + 31) / 32 * 4;
            var maskBytes = maskStride * size;

            using var stream = new MemoryStream(40 + (size * size * 4) + maskBytes);
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(40);                     // biSize
                writer.Write(size);                   // biWidth
                writer.Write(size * 2);               // biHeight: XOR + AND
                writer.Write((ushort)1);              // biPlanes
                writer.Write((ushort)32);             // biBitCount
                writer.Write(0);                      // BI_RGB
                writer.Write(size * size * 4);        // biSizeImage
                writer.Write(0);                      // biXPelsPerMeter
                writer.Write(0);                      // biYPelsPerMeter
                writer.Write(0);                      // biClrUsed
                writer.Write(0);                      // biClrImportant

                // DIB guarda as linhas de baixo para cima.
                for (var y = size - 1; y >= 0; y--)
                {
                    writer.Write(pixels, y * stride, size * 4);
                }

                writer.Write(new byte[maskBytes]);
                writer.Flush();
            }

            return stream.ToArray();
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }
}
