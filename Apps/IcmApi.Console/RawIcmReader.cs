namespace Icm.Api.ConsoleApp
{
    using System.Diagnostics;
    using System.Net;
    using System.Net.Http.Headers;
    using Icm.Api.ConsoleApp.Configuration;
    using Icm.Api.Models;
    using Icm.Api.Services;

    /// <summary>
    /// The calls this tool makes to ICM without going through the library — for the things
    /// the library has no typed support for, and should not grow any for: a business
    /// component's describe document, a child collection read as raw JSON.
    /// </summary>
    internal static class RawIcmReader
    {
        /// <summary>
        /// Describe mode: fetch a resource's OpenAPI document from ICM's
        /// <c>data/{resource}/describe</c> and save it — how the documents under
        /// <c>Apps/IcmApi/docs/integration/</c> are refreshed.
        /// </summary>
        /// <remarks>
        /// Written exactly as ICM sent it, not re-serialized: the document is kept as
        /// evidence of what ICM says about itself, and a round trip through a serializer
        /// would re-escape and re-order it into something ICM never said.
        /// </remarks>
        public static async Task<int> DescribeAsync(
            HttpClient icmClient,
            IOAuthTokenService tokenService,
            OAuthClientCredentials credentials,
            ConsoleSettings settings)
        {
            // Each segment escaped on its own, for the same reason as the child read: the
            // gateway answers 400 to %2F.
            string resource = string.Join(
                '/',
                settings.Describe.Resource!
                    .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(Uri.EscapeDataString));
            string path = $"{settings.Icm.BaseUrl!.TrimEnd('/')}/data/{resource}/describe";

            Console.WriteLine($"Describing {path}");
            Console.WriteLine();

            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                (HttpStatusCode status, string body) =
                    await GetAsync(icmClient, tokenService, credentials, settings, path);
                stopwatch.Stop();

                if ((int)status is < 200 or > 299)
                {
                    return Program.Fail(
                        stopwatch,
                        $"ICM returned {(int)status} {status}.",
                        "The resource is passed through as written; it is the two path segments after data/, "
                        + "e.g. ServiceRequest/ServiceRequest.",
                        responseBody: body);
                }

                Console.WriteLine($"{(int)status} {status} in {stopwatch.ElapsedMilliseconds} ms, {body.Length} characters.");

                if (string.IsNullOrWhiteSpace(settings.Describe.OutputFile))
                {
                    Console.WriteLine();
                    Console.WriteLine(Program.PrettyJson(body));
                    return 0;
                }

                string file = Path.GetFullPath(settings.Describe.OutputFile);
                await File.WriteAllTextAsync(file, body);
                Console.WriteLine($"Written to {file}");
                return 0;
            }
            catch (OperationCanceledException exception)
            {
                stopwatch.Stop();
                return Program.Fail(
                    stopwatch,
                    $"ICM did not answer within {settings.Icm.TimeoutSeconds} seconds.",
                    exception.Message);
            }
            catch (HttpRequestException exception)
            {
                stopwatch.Stop();
                return Program.Fail(stopwatch, "Could not reach ICM.", exception.Message);
            }
            catch (IOException exception)
            {
                stopwatch.Stop();
                return Program.Fail(stopwatch, "ICM answered, but the document could not be written.", exception.Message);
            }
        }

        /// <summary>
        /// One authenticated GET outside the library. Same two identifying headers as
        /// every library call.
        /// </summary>
        public static async Task<(HttpStatusCode Status, string Body)> GetAsync(
            HttpClient icmClient,
            IOAuthTokenService tokenService,
            OAuthClientCredentials credentials,
            ConsoleSettings settings,
            string path)
        {
            using HttpRequestMessage request = new(HttpMethod.Get, path);
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer", await tokenService.GetTokenAsync(credentials));
            if (!string.IsNullOrWhiteSpace(settings.Icm.TrustedUserName))
            {
                request.Headers.Add("X-ICM-TrustedUserName", settings.Icm.TrustedUserName);
            }

            using HttpResponseMessage response = await icmClient.SendAsync(request);
            return (response.StatusCode, await response.Content.ReadAsStringAsync());
        }
    }
}
