using Descanso.Core;

namespace Descanso.Core.Tests;

public class WorkingHoursTests
{
    private static WorkingHoursSettings BusinessHours() => new()
    {
        Enabled = true,
        Start = new TimeOnly(8, 0),
        End = new TimeOnly(18, 0),
        Days = new List<int> { 1, 2, 3, 4, 5 },
    };

    private static DateTimeOffset At(int month, int day, int hour, int minute = 0) =>
        new(2026, month, day, hour, minute, 0, TimeSpan.Zero);

    [Fact]
    public void Aberto_durante_o_expediente()
    {
        Assert.True(BusinessHours().IsOpen(At(10, 27, 9)));
    }

    [Theory]
    [InlineData(7, 59)]
    [InlineData(18, 0)]
    [InlineData(20, 0)]
    public void Fechado_fora_do_expediente(int hour, int minute)
    {
        Assert.False(BusinessHours().IsOpen(At(10, 27, hour, minute)));
    }

    [Fact]
    public void Fechado_no_fim_de_semana()
    {
        // 31/10/2026 é sábado, 01/11/2026 é domingo.
        Assert.False(BusinessHours().IsOpen(At(10, 31, 10)));
        Assert.False(BusinessHours().IsOpen(At(11, 1, 10)));
    }

    [Fact]
    public void Desabilitado_esta_sempre_aberto()
    {
        var hours = BusinessHours();
        hours.Enabled = false;

        Assert.True(hours.IsOpen(At(11, 1, 3)));
    }

    [Fact]
    public void Janela_noturna_atravessa_a_meia_noite()
    {
        var night = new WorkingHoursSettings
        {
            Enabled = true,
            Start = new TimeOnly(22, 0),
            End = new TimeOnly(6, 0),
            Days = new List<int> { 1 }, // segunda
        };

        Assert.True(night.IsOpen(At(10, 26, 23)));  // segunda 23:00
        Assert.True(night.IsOpen(At(10, 27, 2)));   // terça 02:00 — turno de segunda
        Assert.False(night.IsOpen(At(10, 27, 6)));  // terça 06:00 — encerrado
        Assert.False(night.IsOpen(At(10, 26, 21))); // segunda 21:00 — ainda não abriu
        Assert.False(night.IsOpen(At(10, 27, 12))); // terça meio-dia
    }

    [Fact]
    public void Fim_de_semana_pode_ser_ativado()
    {
        var hours = BusinessHours();
        hours.Days = new List<int> { 6, 7 };

        Assert.True(hours.IsOpen(At(10, 31, 10)));
        Assert.False(hours.IsOpen(At(10, 27, 10)));
    }
}
