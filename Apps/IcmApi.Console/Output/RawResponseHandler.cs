namespace Icm.Api.ConsoleApp.Output
{
    using System.Text.Json;

    /// <summary>
    /// Prints the raw JSON of every ICM response before the client maps it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The mapped dump can only show fields the library models; anything ICM sends that
    /// <c>SiebelServiceRequest</c> has no property for is discarded silently by the
    /// deserializer. This handler sits underneath all of that and shows what actually
    /// arrived — which is the only way to know whether a missing field is missing from ICM
    /// or missing from the model.
    /// </para>
    /// <para>
    /// It sits on the ICM client only, never the token client: that response carries an
    /// access token, and printing it would put a live credential in a terminal scrollback.
    /// </para>
    /// </remarks>
    internal sealed class RawResponseHandler : DelegatingHandler
    {
        private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage response = await base.SendAsync(request, cancellationToken);

            Console.WriteLine(new string('=', 60));
            Console.WriteLine($"RAW  {request.Method} {request.RequestUri}");
            Console.WriteLine($"     {(int)response.StatusCode} {response.StatusCode}");
            Console.WriteLine(new string('=', 60));

            if (response.Content is null)
            {
                Console.WriteLine("(no body)");
                Console.WriteLine();
                return response;
            }

            // Buffer before reading, so consuming the body here does not leave Refit with
            // an exhausted stream.
            await response.Content.LoadIntoBufferAsync(cancellationToken);
            string body = await response.Content.ReadAsStringAsync(cancellationToken);

            Console.WriteLine(Prettify(body));
            Console.WriteLine();
            return response;
        }

        /// <summary>
        /// Indents the JSON so a wide Siebel record is readable. A body that is not JSON —
        /// an error page, say — is printed as it came rather than swallowed.
        /// </summary>
        private static string Prettify(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return "(empty body)";
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(body);
                return JsonSerializer.Serialize(document.RootElement, Indented);
            }
            catch (JsonException)
            {
                return body;
            }
        }
    }
}
