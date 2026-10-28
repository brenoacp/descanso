using System.Diagnostics;
using Microsoft.Win32;

namespace Descanso.App;

/// <summary>
/// Autostart sem administrador: uma entrada em HKCU\...\Run apontando para o exe
/// atual. Nada é instalado no sistema.
/// </summary>
internal static class Autostart
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Descanso";

    public static string Command
    {
        get
        {
            var exe = Environment.ProcessPath
                ?? Process.GetCurrentProcess().MainModule?.FileName
                ?? string.Empty;

            return $"\"{exe}\" --minimized";
        }
    }

    public static string? RegisteredCommand
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
                return key?.GetValue(ValueName) as string;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
            {
                Log.Error("autostart-read", ex);
                return null;
            }
        }
    }

    public static bool IsEnabled => !string.IsNullOrEmpty(RegisteredCommand);

    public static void Sync(bool enabled)
    {
        try
        {
            if (!enabled)
            {
                if (IsEnabled)
                {
                    Set(false);
                }

                return;
            }

            if (!string.Equals(RegisteredCommand, Command, StringComparison.OrdinalIgnoreCase))
            {
                Set(true);
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
        {
            Log.Error("autostart-sync", ex);
        }
    }

    private static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException("Não foi possível abrir a chave de inicialização.");

        if (enabled)
        {
            key.SetValue(ValueName, Command, RegistryValueKind.String);
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
