using System.Runtime.InteropServices;

namespace Descanso.App;

/// <summary>
/// Tempo desde o último uso de teclado/mouse. É o sinal que evita lembrar quem
/// já está longe da máquina.
/// </summary>
internal sealed class IdleMonitor
{
    private const int LastInputInfoSize = 8;

    [StructLayout(LayoutKind.Sequential)]
    private struct RawLastInputInfo
    {
        public uint Size;
        public uint Time;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLastInputInfo(ref RawLastInputInfo info);

    public TimeSpan IdleTime { get; private set; }

    public static TimeSpan Measure()
    {
        var info = new RawLastInputInfo { Size = LastInputInfoSize };
        if (!GetLastInputInfo(ref info))
        {
            return TimeSpan.Zero;
        }

        // Subtração em uint trata o estouro do contador de 32 bits (~49 dias).
        var milliseconds = unchecked((uint)Environment.TickCount - info.Time);
        return TimeSpan.FromMilliseconds(milliseconds);
    }

    public void Refresh() => IdleTime = Measure();
}
