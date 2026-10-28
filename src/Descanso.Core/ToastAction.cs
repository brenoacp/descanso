namespace Descanso.Core;

/// <summary>
/// Interpreta os argumentos que o Windows devolve quando o usuário clica em um
/// botão do toast. É a parte do fluxo de notificação que falha em silêncio, então
/// mora no núcleo e tem teste.
/// </summary>
public static class ToastAction
{
    public const string Open = "open";
    public const string Done = "done";
    public const string Snooze = "snooze";
    public const string Skip = "skip";

    /// <summary>
    /// Extrai o valor de <c>action=</c> de um argumento. Sem ação reconhecida,
    /// assume <see cref="Open"/>, que é o comportamento do clique no corpo do toast.
    /// </summary>
    public static string Parse(string? argument)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            return Open;
        }

        foreach (var pair in argument.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var key = pair[..separator].TrimStart('?', '-');
            if (key.Equals("action", StringComparison.OrdinalIgnoreCase))
            {
                var value = Uri.UnescapeDataString(pair[(separator + 1)..]);
                return value.Length == 0 ? Open : value;
            }
        }

        return Open;
    }

    /// <summary>
    /// Procura uma ação em uma lista de argumentos de linha de comando, ignorando
    /// o marcador de ativação e o <see cref="Open"/> implícito.
    /// </summary>
    public static string? Find(IEnumerable<string> arguments)
    {
        foreach (var argument in arguments)
        {
            var action = Parse(argument);
            if (!action.Equals(Open, StringComparison.OrdinalIgnoreCase))
            {
                return action;
            }
        }

        return null;
    }
}
