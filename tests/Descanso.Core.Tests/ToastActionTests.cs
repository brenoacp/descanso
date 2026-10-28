using Descanso.Core;

namespace Descanso.Core.Tests;

public class ToastActionTests
{
    [Theory]
    [InlineData("action=done", ToastAction.Done)]
    [InlineData("action=snooze", ToastAction.Snooze)]
    [InlineData("action=skip", ToastAction.Skip)]
    [InlineData("?action=done", ToastAction.Done)]
    public void Le_a_acao_do_argumento(string argument, string expected)
    {
        Assert.Equal(expected, ToastAction.Parse(argument));
    }

    [Fact]
    public void Sem_argumento_assume_clique_no_corpo()
    {
        Assert.Equal(ToastAction.Open, ToastAction.Parse(null));
        Assert.Equal(ToastAction.Open, ToastAction.Parse(""));
        Assert.Equal(ToastAction.Open, ToastAction.Parse("qualquer=coisa"));
    }

    [Fact]
    public void Convive_com_argumentos_extras()
    {
        Assert.Equal(ToastAction.Snooze, ToastAction.Parse("origem=toast&action=snooze&versao=1"));
    }

    [Fact]
    public void Decodifica_valor_escapado()
    {
        Assert.Equal("fiz a pausa", ToastAction.Parse("action=fiz%20a%20pausa"));
    }

    [Fact]
    public void Encontra_a_acao_na_linha_de_comando()
    {
        var args = new[] { "-ToastActivated", "action=snooze" };

        Assert.Equal(ToastAction.Snooze, ToastAction.Find(args));
    }

    [Fact]
    public void Linha_de_comando_sem_acao_nao_e_ativacao()
    {
        Assert.Null(ToastAction.Find(new[] { "--minimized" }));
        Assert.Null(ToastAction.Find(Array.Empty<string>()));
    }
}
