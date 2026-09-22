namespace Icm.Api.ConsoleApp.Output
{
    using System.Text.Json;

    /// <summary>
    /// Prints the raw JSON of every ICM exchange: a request body before it is sent, and
    /// every response before the client maps it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unmodelled fields are no longer lost — <c>SiebelServiceRequest</c> catches them via
    /// <c>[JsonExtensionData]</c> and the mapped dump shows them in <c>AdditionalFields</c>.
    /// What only this handler can show is the wire exactly as it is: the outgoing body as
    /// serialized (the bus pass envelope, whose shape is what the workflow's upsert judges),
    /// and the response as it arrived — untouched JSON shape, field order and raw values,
    /// including the bodies of non-2xx answers — which is what settles a disagreement
    /// between the client's view and ICM's.
    /// </para>
    /// <para>
    /// It sits on the ICM client only, never the token client: that exchange carries the
    /// client secret out and an access token back, and printing either would put a live
    /// credential in a terminal scrollback. Bodies only, for the same reason — the
    /// Authorization header rides on every ICM request.
    /// </para>
    /// </remarks>
    internal sealed class RawWireHandler : DelegatingHandler
    {
        private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // GETs carry no body and print nothing here; their story is the response.
            if (request.Content is not null)
            {
                // Buffer before reading, so consuming the body here leaves it intact
                // for the actual send.
                await request.Content.LoadIntoBufferAsync(cancellationToken);
                string requestBody = await request.Content.ReadAsStringAsync(cancellationToken);

                Console.WriteLine(new string('=', 60));
                Console.WriteLine($"RAW  {request.Method} {request.RequestUri}");
                Console.WriteLine("     request body, as sent");
                Console.WriteLine(new string('=', 60));
                Console.WriteLine(Prettify(requestBody));
                Console.WriteLine();
            }

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
