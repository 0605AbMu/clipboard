using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MacDesktopApp.Services;

public static class SingleInstanceService
{
    private static Socket? _serverSocket;
    private static CancellationTokenSource? _cts;
    private static string? _socketPath;

    public static string GetSocketPath()
    {
        var tempDir = Path.GetTempPath();
        var user = Environment.UserName;
        return Path.Combine(tempDir, $"clipboard_app_{user}.sock");
    }

    /// <summary>
    /// Checks if another instance is already running. If so, sends the command to it and returns true.
    /// If not, returns false.
    /// </summary>
    public static bool SendCommandIfAlreadyRunning(string command)
    {
        if (OperatingSystem.IsWindows())
        {
            return SendWindowsPipeCommand(command);
        }

        var path = GetSocketPath();
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            using var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            client.ReceiveTimeout = 1000;
            client.SendTimeout = 1000;
            client.Connect(new UnixDomainSocketEndPoint(path));

            var data = Encoding.UTF8.GetBytes(command.Trim() + "\n");
            client.Send(data);
            return true;
        }
        catch
        {
            // Socket file exists but no process is listening (stale file from previous crash)
            try
            {
                File.Delete(path);
            }
            catch { }
            return false;
        }
    }

    public static void StartServer(Action<string> onCommandReceived)
    {
        if (OperatingSystem.IsWindows())
        {
            StartWindowsPipeServer(onCommandReceived);
            return;
        }

        _socketPath = GetSocketPath();
        try
        {
            if (File.Exists(_socketPath))
            {
                File.Delete(_socketPath);
            }
        }
        catch { }

        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        try
        {
            _serverSocket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            _serverSocket.Bind(new UnixDomainSocketEndPoint(_socketPath));
            _serverSocket.Listen(10);
        }
        catch
        {
            return;
        }

        Task.Run(async () =>
        {
            byte[] buffer = new byte[256];
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var client = await _serverSocket.AcceptAsync(token);
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            using (client)
                            {
                                var buffer = new byte[256];
                                int bytesRead = await client.ReceiveAsync(buffer, token);
                                if (bytesRead > 0)
                                {
                                    var cmd = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                                    onCommandReceived(cmd);
                                }
                            }
                        }
                        catch { }
                    }, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch { }
            }
        }, token);

        AppDomain.CurrentDomain.ProcessExit += (s, e) => StopServer();
    }

    public static void StopServer()
    {
        try
        {
            _cts?.Cancel();
            _serverSocket?.Close();
            _serverSocket?.Dispose();
            if (!string.IsNullOrEmpty(_socketPath) && File.Exists(_socketPath))
            {
                File.Delete(_socketPath);
            }
        }
        catch { }
    }

    private static bool SendWindowsPipeCommand(string command)
    {
        try
        {
            using var client = new System.IO.Pipes.NamedPipeClientStream(".", "ClipboardApp_0605AbMu_Pipe", System.IO.Pipes.PipeDirection.Out);
            client.Connect(300);
            using var writer = new StreamWriter(client, Encoding.UTF8);
            writer.WriteLine(command.Trim());
            writer.Flush();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void StartWindowsPipeServer(Action<string> onCommandReceived)
    {
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var server = new System.IO.Pipes.NamedPipeServerStream("ClipboardApp_0605AbMu_Pipe", System.IO.Pipes.PipeDirection.In, 5, System.IO.Pipes.PipeTransmissionMode.Byte, System.IO.Pipes.PipeOptions.Asynchronous);
                    await server.WaitForConnectionAsync(token);
                    using var reader = new StreamReader(server, Encoding.UTF8);
                    var line = await reader.ReadLineAsync(token);
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        onCommandReceived(line.Trim());
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch { }
            }
        }, token);
    }
}
