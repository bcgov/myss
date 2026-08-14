namespace Myss.Api.Providers
{
    using System;
    using System.Buffers.Binary;
    using System.IO;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Myss.Api.Configuration.Models;

    /// <summary>
    /// Scans content against clamd using its INSTREAM command over TCP:
    /// send <c>zINSTREAM\0</c>, then length-prefixed chunks, then a zero
    /// chunk; clamd replies <c>stream: OK</c> or
    /// <c>stream: &lt;signature&gt; FOUND</c>. The protocol is tiny, so it's
    /// implemented here rather than pulling in a wrapper package.
    /// </summary>
    public class ClamAvScanProvider : IVirusScanProvider
    {
        // 128 KiB is well under clamd's chunk limit and keeps memory flat.
        private const int ChunkSize = 128 * 1024;

        private readonly ILogger<ClamAvScanProvider> _logger;
        private readonly ClamAvConfig _config;

        /// <summary>
        /// Initializes a new instance of the <see cref="ClamAvScanProvider"/> class.
        /// </summary>
        /// <param name="logger">Injected Logger Provider.</param>
        /// <param name="config">Injected ClamAV connection settings.</param>
        public ClamAvScanProvider(ILogger<ClamAvScanProvider> logger, IOptions<ClamAvConfig> config)
        {
            _logger = logger;
            _config = config.Value;
        }

        /// <inheritdoc/>
        public async Task<VirusScanResult> ScanAsync(Stream content, CancellationToken cancellationToken)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(_config.TimeoutSeconds));

            string reply;
            try
            {
                reply = await StreamToClamdAsync(content, timeout.Token);
            }
            catch (Exception ex) when (ex is SocketException or IOException or OperationCanceledException)
            {
                // If the caller cancelled, let that surface as a cancellation
                // rather than a scanner outage.
                cancellationToken.ThrowIfCancellationRequested();
                throw new VirusScanUnavailableException(
                    $"clamd at {_config.Host}:{_config.Port} could not be reached or timed out.", ex);
            }

            return ParseReply(reply);
        }

        private async Task<string> StreamToClamdAsync(Stream content, CancellationToken cancellationToken)
        {
            using var client = new TcpClient();
            await client.ConnectAsync(_config.Host, _config.Port, cancellationToken);
            await using NetworkStream stream = client.GetStream();

            // The "z" prefix asks for null-terminated framing, which the reply
            // uses too.
            await stream.WriteAsync(Encoding.ASCII.GetBytes("zINSTREAM\0"), cancellationToken);

            byte[] buffer = new byte[ChunkSize];
            byte[] lengthPrefix = new byte[4];
            int read;
            while ((read = await content.ReadAsync(buffer, cancellationToken)) > 0)
            {
                BinaryPrimitives.WriteUInt32BigEndian(lengthPrefix, (uint)read);
                await stream.WriteAsync(lengthPrefix, cancellationToken);
                await stream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }

            BinaryPrimitives.WriteUInt32BigEndian(lengthPrefix, 0);
            await stream.WriteAsync(lengthPrefix, cancellationToken);

            using var reply = new MemoryStream();
            while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                reply.Write(buffer, 0, read);
            }

            return Encoding.ASCII.GetString(reply.ToArray()).TrimEnd('\0', '\n', ' ');
        }

        private VirusScanResult ParseReply(string reply)
        {
            if (reply.EndsWith("OK", StringComparison.Ordinal))
            {
                return new VirusScanResult(IsClean: true);
            }

            if (reply.EndsWith("FOUND", StringComparison.Ordinal))
            {
                // e.g. "stream: Win.Test.EICAR_HDB-1 FOUND" -> keep the middle.
                string signature = reply
                    .Replace("stream:", string.Empty, StringComparison.Ordinal)
                    .Replace("FOUND", string.Empty, StringComparison.Ordinal)
                    .Trim();
                _logger.LogWarning("ClamAV flagged uploaded content: {Signature}", signature);
                return new VirusScanResult(IsClean: false, signature);
            }

            // Anything else ("INSTREAM size limit exceeded. ERROR", an empty
            // reply, ...) is not a verdict.
            throw new VirusScanUnavailableException($"Unexpected clamd reply: '{reply}'.");
        }
    }
}
