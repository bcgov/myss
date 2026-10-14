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
            string body = wanted.BodyFile is { Length: > 0 } file ? await File.ReadAllTextAsync(file) : wanted.Body ?? string.Empty;

            Console.WriteLine($"RAW {method} {path}");
            Console.WriteLine(body.Length == 0 ? "(no body)" : body);
            Console.WriteLine();

            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                (HttpStatusCode status, string response) = body.Length == 0
                    ? await RawIcmReader.GetAsync(icmClient, tokenService, credentials, settings, path)
                    : await RawIcmReader.SendAsync(icmClient, tokenService, credentials, settings, method, path, body);
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
