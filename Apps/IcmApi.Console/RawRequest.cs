namespace Icm.Api.ConsoleApp
{
    using System.Diagnostics;
    using System.Net;
    using Icm.Api.ConsoleApp.Configuration;
    using Icm.Api.Models;
    using Icm.Api.Services;

    /// <summary>
    /// Raw mode: one authenticated request of any method to any <c>data/</c> path with a
    /// body from a file, printed as ICM answered — for measuring a shape the library does
    /// not (yet) have a contract for, such as ICM's documented "add a prospect" payload.
    /// </summary>
    internal static class RawRequest
    {
        /// <summary>Sends the request. <b>A POST or PUT writes to the target ICM.</b></summary>
        public static async Task<int> RunAsync(
            HttpClient icmClient,
            IOAuthTokenService tokenService,
            OAuthClientCredentials credentials,
            ConsoleSettings settings)
        {
            RawSettings wanted = settings.Raw;
            HttpMethod method = HttpMethod.Parse(wanted.Method);
            string path = $"{settings.Icm.BaseUrl!.TrimEnd('/')}/{wanted.Path!.TrimStart('/')}";
            string body;
            try
            {
                body = wanted.BodyFile is { Length: > 0 } file ? await File.ReadAllTextAsync(file) : wanted.Body ?? string.Empty;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                Console.Error.WriteLine($"Raw:BodyFile '{wanted.BodyFile}' could not be read: {exception.Message}");
                return 1;
            }

            Console.WriteLine($"RAW {method} {path}");
            Console.WriteLine(body.Length == 0 ? "(no body)" : body);
            Console.WriteLine();

            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                // The method is the configured one whatever the body: a bodyless DELETE
                // must still delete, not read. Only a GET takes the GET helper, so it goes
                // out without a Content-Type the gateway could object to.
                (HttpStatusCode status, string response) = method == HttpMethod.Get
                    ? await RawIcmReader.GetAsync(icmClient, tokenService, credentials, settings, path)
                    : await RawIcmReader.SendAsync(icmClient, tokenService, credentials, settings, method, path, body.Length == 0 ? null : body);
                stopwatch.Stop();
                Console.WriteLine($"{(int)status} {status} in {stopwatch.ElapsedMilliseconds} ms");
                Console.WriteLine(response.Length == 0 ? "(empty body)" : Program.PrettyJson(response));
                return (int)status is >= 200 and <= 299 ? 0 : 2;
            }
            catch (HttpRequestException exception)
            {
                stopwatch.Stop();
                return Program.Fail(stopwatch, "Could not reach ICM.", exception.Message);
            }
            catch (OperationCanceledException exception)
            {
                stopwatch.Stop();
                return Program.Fail(stopwatch, $"ICM did not answer within {settings.Icm.TimeoutSeconds} seconds.", exception.Message);
            }
        }

        /// <summary>Writes the mode's lines of the run header.</summary>
        /// <param name="settings">The settings.</param>
        public static void WriteHeader(ConsoleSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            Console.WriteLine($"  Request      {settings.Raw.Method} {settings.Raw.Path}");
            Console.WriteLine($"  Body         {settings.Raw.BodyFile ?? (settings.Raw.Body is null ? "(none)" : "(inline)")}");
        }
    }
}
