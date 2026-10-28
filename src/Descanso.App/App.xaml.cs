using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace Descanso.App;

public partial class App : Application
{
    private AppHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

        // Ferramenta de desenvolvimento: gera a folha de prévia do ícone e sai.
        if (TryExportIconPreview(e.Args))
        {
            Shutdown();
            return;
        }

        var instance = SingleInstance.TryAcquirePrimary();
        if (instance is null)
        {
            // Já existe uma instância (inclusive quando o Windows inicia o exe para
            // entregar o clique em um botão do toast): repassa e encerra.
            SingleInstance.SendToPrimary(e.Args);
            Shutdown();
            return;
        }

        _host = new AppHost(instance);
        _host.Start(e.Args);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }

    private static bool TryExportIconPreview(string[] args)
    {
        var index = Array.FindIndex(
            args,
            a => string.Equals(a, "--export-icon-preview", StringComparison.OrdinalIgnoreCase));

        if (index < 0)
        {
            return false;
        }

        var path = index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal)
            ? Path.GetFullPath(args[index + 1])
            : Path.Combine(Path.GetTempPath(), "descanso-tray-icons.png");

        var folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }

        IconPreview.Write(path);
        return true;
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error("dispatcher", e.Exception);
        MessageBox.Show(
            $"Ocorreu um erro inesperado e o Descanso será encerrado.\n\n{e.Exception.Message}\n\nDetalhes em {Log.FilePath}",
            "Descanso",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            Log.Error("dominio", ex);
        }
    }
}
