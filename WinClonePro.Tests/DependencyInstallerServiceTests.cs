using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WinClonePro.Core.Services;

namespace WinClonePro.Tests;

public sealed class DependencyInstallerServiceTests
{
    [Fact]
    public async Task DownloadFileAsync_WritesFinalFileWithoutLeavingTempFileLocked()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "WinCloneProTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var destination = Path.Combine(tempRoot, "adksetup.exe");
            await using var server = await TestHttpServer.StartAsync("test-adk-payload");

            var method = typeof(DependencyInstallerService).GetMethod(
                "DownloadFileAsync",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.NotNull(method);

            var task = (Task?)method!.Invoke(
                null,
                [server.Url.AbsoluteUri, destination, new Progress<int>(_ => { }), CancellationToken.None]);

            Assert.NotNull(task);

            await task!;

            Assert.True(File.Exists(destination));
            Assert.False(File.Exists(destination + ".download"));
            Assert.Equal("test-adk-payload", await File.ReadAllTextAsync(destination));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private sealed class TestHttpServer : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly Task _serverTask;

        private TestHttpServer(TcpListener listener, Task serverTask, Uri url)
        {
            _listener = listener;
            _serverTask = serverTask;
            Url = url;
        }

        public Uri Url { get; }

        public static Task<TestHttpServer> StartAsync(string body)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();

            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var bodyBytes = Encoding.UTF8.GetBytes(body);

            var serverTask = Task.Run(async () =>
            {
                try
                {
                    using var client = await listener.AcceptTcpClientAsync();
                    await using var stream = client.GetStream();

                    using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                    string? line;
                    do
                    {
                        line = await reader.ReadLineAsync();
                    }
                    while (!string.IsNullOrEmpty(line));

                    var header = Encoding.ASCII.GetBytes(
                        $"HTTP/1.1 200 OK\r\nContent-Length: {bodyBytes.Length}\r\nContent-Type: application/octet-stream\r\nConnection: close\r\n\r\n");

                    await stream.WriteAsync(header);
                    await stream.WriteAsync(bodyBytes);
                    await stream.FlushAsync();
                }
                catch (ObjectDisposedException)
                {
                    // Listener was stopped during cleanup.
                }
                catch (SocketException)
                {
                    // Listener was stopped during cleanup.
                }
                catch (InvalidOperationException)
                {
                    // Listener was stopped during cleanup.
                }
            });

            return Task.FromResult(new TestHttpServer(listener, serverTask, new Uri($"http://127.0.0.1:{port}/")));
        }

        public async ValueTask DisposeAsync()
        {
            _listener.Stop();
            await _serverTask;
        }
    }
}
