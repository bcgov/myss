namespace Icm.Api.Tests.Repositories
{
    using System.Net;
    using Icm.Api.Models;
    using Icm.Api.Repositories;
    using Icm.Api.Tests.TestDoubles;
    using Refit;

    /// <summary>
    /// What the repository throws when the transport fails, pinned against the
    /// real Refit rather than assumed. A host in front of this library maps each
    /// case to a different status (unreachable, timeout, upstream error), so a
    /// Refit upgrade that changed the wrapping would otherwise change those
    /// answers silently.
    /// </summary>
    public class BusPassRepositoryTransportTests
    {
        private static readonly Uri BaseAddress = new("https://icm.test.invalid/gov/v1.0");
        private static readonly DateTimeOffset Now = new(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);

        private static readonly BusPassApplication Application = new()
        {
            RequestType = BusPassRequestType.NewApplication,
            LastName = "Example",
        };

        [Fact]
        public async Task AConnectionFailure_IsAnApiRequestExceptionWrappingIt()
        {
            using var client = new HttpClient(new ThrowingHandler(new HttpRequestException("Connection refused")))
            {
                BaseAddress = BaseAddress,
            };
            var repository = new BusPassRepository(client, timeProvider: new FakeTimeProvider(Now));

            ApiRequestException ex = await Assert.ThrowsAsync<ApiRequestException>(
                () => repository.SubmitAsync("tok", Application));

            Assert.IsType<HttpRequestException>(ex.InnerException);
        }

        [Fact]
        public async Task TheClientsOwnTimeout_IsAnApiRequestExceptionWrappingACancellation()
        {
            using var client = new HttpClient(new HangingHandler())
            {
                BaseAddress = BaseAddress,
                Timeout = TimeSpan.FromMilliseconds(50),
            };
            var repository = new BusPassRepository(client, timeProvider: new FakeTimeProvider(Now));

            ApiRequestException ex = await Assert.ThrowsAsync<ApiRequestException>(
                () => repository.SubmitAsync("tok", Application));

            // HttpClient reports its own timeout as a cancellation nobody asked for,
            // with the timeout underneath it.
            OperationCanceledException cancelled = Assert.IsAssignableFrom<OperationCanceledException>(ex.InnerException);
            Assert.IsType<TimeoutException>(cancelled.InnerException);
        }

        [Fact]
        public async Task AFailureStatus_IsAnApiExceptionWithThatStatus()
        {
            using var client = new HttpClient(new RecordingHttpMessageHandler(HttpStatusCode.Forbidden, """{"ERROR":"SBL-DAT-00825"}"""))
            {
                BaseAddress = BaseAddress,
            };
            var repository = new BusPassRepository(client, timeProvider: new FakeTimeProvider(Now));

            ApiException ex = await Assert.ThrowsAsync<ApiException>(
                () => repository.SubmitAsync("tok", Application));

            Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
        }

        [Fact]
        public async Task ACallersOwnCancellation_IsNotWrapped()
        {
            using var client = new HttpClient(new HangingHandler()) { BaseAddress = BaseAddress };
            var repository = new BusPassRepository(client, timeProvider: new FakeTimeProvider(Now));
            using var cancellation = new CancellationTokenSource();
            await cancellation.CancelAsync();

            Exception ex = await Assert.ThrowsAnyAsync<Exception>(
                () => repository.SubmitAsync("tok", Application, cancellation.Token));

            Assert.IsAssignableFrom<OperationCanceledException>(ex);
        }

        private sealed class ThrowingHandler : HttpMessageHandler
        {
            private readonly Exception _exception;

            public ThrowingHandler(Exception exception) => _exception = exception;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
                throw _exception;
        }

        private sealed class HangingHandler : HttpMessageHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
        }
    }
}
