using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace ArcadeHub.Services;

public sealed class ControllerBridgeService : IDisposable
{
    private readonly TcpListener _listener =
        new(IPAddress.Loopback, 47650);

    private CancellationTokenSource? _cancellation;
    private Task? _serverTask;

    public event Action<uint, uint>? ButtonsReceived;

    public void Start()
    {
        if (_serverTask != null)
            return;

        _cancellation = new CancellationTokenSource();
        _listener.Start();

        _serverTask = RunAsync(_cancellation.Token);

        Console.WriteLine("[ArcadeHub] Controller bridge listening on port 47650");
    }

    private async Task RunAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                using var client = await _listener.AcceptTcpClientAsync(token);
                using var reader = new StreamReader(client.GetStream());

                Console.WriteLine("[ArcadeHub] Windows controller bridge connected");

                while (!token.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync(token);

                    if (line == null)
                        break;

                    var parts = line.Split(',');

                    if (parts.Length == 2 &&
                        uint.TryParse(parts[0], out var buttons) &&
                        uint.TryParse(parts[1], out var pov))
                    {
                        ButtonsReceived?.Invoke(buttons, pov);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (SocketException ex) when (token.IsCancellationRequested)
        {
            _ = ex;
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"[ArcadeHub] Controller bridge error: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _cancellation?.Cancel();
        _listener.Stop();
        _cancellation?.Dispose();
    }
}
