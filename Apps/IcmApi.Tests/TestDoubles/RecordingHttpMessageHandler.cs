namespace Icm.Api.Tests.TestDoubles
{
    using System.Net;
    using System.Text;

    /// <summary>
    /// Captures the request Refit built and replies with a canned response, so the tests
    /// can assert on the URL, headers and body without a Siebel instance.
    /// </summary>
    internal sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string? _responseJson;

        public RecordingHttpMessageHandler(
            HttpStatusCode statusCode = HttpStatusCode.OK,
            string? responseJson = null)
        {
            _statusCode = statusCode;
            _responseJson = responseJson;
        }

        /// <summary>Gets the request that was sent.</summary>
        public HttpRequestMessage? Request { get; private set; }

        /// <summary>Gets the body that was sent, or null when there was none.</summary>
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            if (request.Content is not null)
            {
                RequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            // Refit reads the response's RequestMessage when it builds an ApiException,
            // which HttpClient sets for real handlers but a hand-rolled one must do itself.
            HttpResponseMessage response = new(_statusCode) { RequestMessage = request };
            if (_responseJson is not null)
            {
                response.Content = new StringContent(_responseJson, Encoding.UTF8, "application/json");
            }

            return response;
        }
    }
}
