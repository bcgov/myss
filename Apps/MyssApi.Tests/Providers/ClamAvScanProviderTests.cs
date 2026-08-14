namespace Myss.Api.Tests.Providers
{
    using System.Buffers.Binary;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.Extensions.Options;
    using Myss.Api.Configuration.Models;
    using Myss.Api.Providers;

    /// <summary>
    /// Tests for <see cref="ClamAvScanProvider"/> against an in-process fake
    /// clamd, covering the INSTREAM framing and reply parsing.
    /// </summary>
    public class ClamAvScanProviderTests
    {
        [Fact]
        public async Task Scan_OkReply_IsClean_AndFramesTheContent()
        {
            using var clamd = new FakeClamd("stream: OK\0");
            ClamAvScanProvider provider = NewProvider(clamd.Port);
            byte[] payload = Encoding.ASCII.GetBytes("hello clamd");

            VirusScanResult result = await provider.ScanAsync(new MemoryStream(payload), CancellationToken.None);

            Assert.True(result.IsClean);
            Assert.Null(result.Signature);
            Assert.Equal("zINSTREAM\0", await clamd.Command);
            Assert.Equal(payload, await clamd.ReceivedContent);
        }

        [Fact]
        public async Task Scan_FoundReply_ReportsTheSignature()
        {
            using var clamd = new FakeClamd("stream: Win.Test.EICAR_HDB-1 FOUND\0");
            ClamAvScanProvider provider = NewProvider(clamd.Port);

            VirusScanResult result = await provider.ScanAsync(
                new MemoryStream([1, 2, 3]), CancellationToken.None);

            Assert.False(result.IsClean);
            Assert.Equal("Win.Test.EICAR_HDB-1", result.Signature);
        }

        [Fact]
        public async Task Scan_ErrorReply_IsNotAVerdict()
        {
            using var clamd = new FakeClamd("INSTREAM size limit exceeded. ERROR\0");
            ClamAvScanProvider provider = NewProvider(clamd.Port);

            await Assert.ThrowsAsync<VirusScanUnavailableException>(
                () => provider.ScanAsync(new MemoryStream([1]), CancellationToken.None));
        }

        [Fact]
        public async Task Scan_NoDaemon_ThrowsUnavailable()
        {
            // Bind and close to get a port nothing is listening on.
            int freePort;
            using (var listener = new TcpListener(IPAddress.Loopback, 0))
            {
                listener.Start();
                freePort = ((IPEndPoint)listener.LocalEndpoint).Port;
            }

            ClamAvScanProvider provider = NewProvider(freePort);

            await Assert.ThrowsAsync<VirusScanUnavailableException>(
                () => provider.ScanAsync(new MemoryStream([1]), CancellationToken.None));
        }

        private static ClamAvScanProvider NewProvider(int port)
        {
            return new ClamAvScanProvider(
                NullLogger<ClamAvScanProvider>.Instance,
                Options.Create(new ClamAvConfig { Host = "127.0.0.1", Port = port, TimeoutSeconds = 10 }));
        }

        /// <summary>
        /// Single-connection fake clamd: reads the command and the chunks,
        /// then answers with a canned reply.
        /// </summary>
        private sealed class FakeClamd : IDisposable
        {
            private readonly TcpListener _listener;
            private readonly TaskCompletionSource<string> _command = new();
            private readonly TaskCompletionSource<byte[]> _content = new();

            public FakeClamd(string reply)
            {
                _listener = new TcpListener(IPAddress.Loopback, 0);
                _listener.Start();
                Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
                _ = ServeAsync(reply);
            }

            public int Port { get; }

            public Task<string> Command => _command.Task;

            public Task<byte[]> ReceivedContent => _content.Task;

            public void Dispose() => _listener.Dispose();

            private async Task ServeAsync(string reply)
            {
                using TcpClient client = await _listener.AcceptTcpClientAsync();
                NetworkStream stream = client.GetStream();

                var command = new StringBuilder();
                int b;
                while ((b = stream.ReadByte()) > 0)
                {
                    command.Append((char)b);
                }

                _command.SetResult(command.Append('\0').ToString());

                using var content = new MemoryStream();
                byte[] lengthPrefix = new byte[4];
                while (true)
                {
                    await stream.ReadExactlyAsync(lengthPrefix);
                    uint length = BinaryPrimitives.ReadUInt32BigEndian(lengthPrefix);
                    if (length == 0)
                    {
                        break;
                    }

                    byte[] chunk = new byte[length];
                    await stream.ReadExactlyAsync(chunk);
                    content.Write(chunk);
                }

                _content.SetResult(content.ToArray());
                await stream.WriteAsync(Encoding.ASCII.GetBytes(reply));
                client.Close();
            }
        }
    }
}
