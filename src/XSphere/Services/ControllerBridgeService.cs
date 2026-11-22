using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace XSphere.Services;

public sealed class ControllerBridgeService : IDisposable
{
    private readonly TcpListener _listener =
        new(IPAddress.Loopback, 47650);

    private CancellationTokenSource? _cancellation;
    private Task? _serverTask;

    public event Action<uint[]>? ControllerStateReceived;

    public void Start()
    {
        if (_serverTask != null)
            return;

        _cancellation = new CancellationTokenSource();
        _listener.Start();

        _serverTask = RunAsync(_cancellation.Token);

        Console.WriteLine("[XSphere] Controller bridge listening on port 47650");
    }

    private async Task RunAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                using var client = await _listener.AcceptTcpClientAsync(token);
                using var reader = new StreamReader(client.GetStream());

                Console.WriteLine("[XSphere] Windows controller bridge connected");

                while (!token.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync(token);

                    if (line == null)
                        break;

                    var parts = line.Split(',');

                    if (parts.Length == 8)
                    {
                        var values = new uint[8];
                        bool valid = true;

                        for (int i = 0; i < values.Length; i++)
                        {
                            if (!uint.TryParse(parts[i], out values[i]))
                            {
                                valid = false;
                                break;
                            }
                        }

                        if (valid)
                            ControllerStateReceived?.Invoke(values);
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
                $"[XSphere] Controller bridge error: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _cancellation?.Cancel();
        _listener.Stop();
        _cancellation?.Dispose();
    }
}
