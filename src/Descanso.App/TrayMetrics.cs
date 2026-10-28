using System.Runtime.InteropServices;

namespace Descanso.App;

/// <summary>
/// Tamanho que a bandeja usa neste DPI. Desenhar no tamanho exato evita a
/// redução borrada que o Windows faz ao escalar um ícone maior.
/// </summary>
internal static class TrayMetrics
{
    private const int SmCxSmIcon = 49;

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    public static int IconSize => Math.Max(16, GetSystemMetrics(SmCxSmIcon));
}
