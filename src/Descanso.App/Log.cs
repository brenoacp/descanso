using System.IO;

namespace Descanso.App;

/// <summary>
/// Diário mínimo de erros. Um app de bandeja falha em silêncio; sem isso, um
/// toast que não aparece não deixa rastro nenhum.
/// </summary>
internal static class Log
{
    private const long MaxBytes = 512 * 1024;

    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Descanso",
        "log.txt");

    public static void Error(string context, Exception exception)
    {
        try
        {
            var directory = Path.GetDirectoryName(FilePath)!;
            Directory.CreateDirectory(directory);

            var file = new FileInfo(FilePath);
            if (file.Exists && file.Length > MaxBytes)
            {
                File.Move(FilePath, FilePath + ".old", overwrite: true);
            }

            File.AppendAllText(
                FilePath,
                $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss} [{context}] {exception}{Environment.NewLine}");
        }
        catch (Exception)
        {
            // Registrar não pode derrubar o aplicativo.
        }
    }
}
