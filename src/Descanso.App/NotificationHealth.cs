using Microsoft.Win32;

namespace Descanso.App;

/// <summary>
/// Verificação de melhor esforço: se o Windows registrou o app com as notificações
/// desligadas, avisa na tela de configuração. Se a chave não existir, não conclui nada.
/// </summary>
internal static class NotificationHealth
{
    private const string SettingsKey =
        @"Software\Microsoft\Windows\CurrentVersion\Notifications\Settings";

    public static bool IsDisabled()
    {
        try
        {
            using var root = Registry.CurrentUser.OpenSubKey(SettingsKey);
            if (root is null)
            {
                return false;
            }

            foreach (var name in root.GetSubKeyNames())
            {
                if (name.IndexOf("Descanso", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                using var app = root.OpenSubKey(name);
                if (app?.GetValue("Enabled") is int enabled && enabled == 0)
                {
                    return true;
                }
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
        {
            Log.Error("notification-health", ex);
        }

        return false;
    }
}
