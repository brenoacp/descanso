using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;

namespace Descanso.App;

/// <summary>
/// Garante uma instância só. O Windows inicia um processo novo para entregar
/// cliques em botões de toast, então a segunda instância precisa repassar os
/// argumentos para a primeira e sair.
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    private const string MutexName = @"Local\Descanso.SingleInstance.v1";
    private const string PipeName = "Descanso.Activation.v1";
    private const char Separator = '\u001f';

    private readonly Mutex _mutex;
    private CancellationTokenSource? _cts;
    private Task? _listener;

    private SingleInstance(Mutex mutex) => _mutex = mutex;

    /// <summary>Retorna a instância primária, ou null se outra já estiver rodando.</summary>
    public static SingleInstance? TryAcquirePrimary()
    {
        var mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (createdNew)
        {
            return new SingleInstance(mutex);
        }

        mutex.Dispose();
        return null;
    }

    public static bool SendToPrimary(string[] arguments)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
                client.Connect(300);

                using var writer = new StreamWriter(client, new UTF8Encoding(false)) { AutoFlush = true };
                writer.WriteLine(string.Join(Separator, arguments));
                return true;
            }
            catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException)
            {
                // A instância primária pode estar entre dois ciclos do pipe.
                Thread.Sleep(120);
            }
        }

        return false;
    }

    public void Listen(Action<string[]> onArguments)
    {
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _listener = Task.Run(() => ListenLoop(onArguments, token));
    }

    private static async Task ListenLoop(Action<string[]> onArguments, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.In,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(token).ConfigureAwait(false);

                using var reader = new StreamReader(server, Encoding.UTF8);
                var line = await reader.ReadLineAsync(token).ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(line))
                {
                    var arguments = line.Split(Separator, StringSplitOptions.RemoveEmptyEntries);
                    onArguments(arguments);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                try
                {
                    await Task.Delay(150, CancellationToken.None).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();

        try
        {
            _listener?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // Encerrando de qualquer forma.
        }

        _cts?.Dispose();

        try
        {
            _mutex.ReleaseMutex();
        }
        catch (ApplicationException)
        {
            // O mutex pertence a outra thread — liberar é opcional no shutdown.
        }

        _mutex.Dispose();
    }
}
