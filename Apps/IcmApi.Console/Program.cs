namespace Icm.Api.ConsoleApp
{
    using System.Diagnostics;
    using System.Globalization;
    using Icm.Api.ConsoleApp.Configuration;
    using Icm.Api.ConsoleApp.Output;
    using Icm.Api.Models;
    using Icm.Api.Repositories;
    using Icm.Api.Services;
    using Microsoft.Extensions.Configuration;
    using Refit;

    /// <summary>
    /// A functional test for the ICM Service Request query, run by hand.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Everything in <c>IcmApi.Tests</c> runs against canned responses — deliberately, so
    /// the suite is fast and needs no credentials. That leaves exactly one class of
    /// question open: whether the assumptions this client is built on hold against a real
    /// ICM. This program is how you find out. It reads its settings, gets a token, runs one
    /// search, and prints what came back.
    /// </para>
    /// <para>
    /// The two things worth watching in the output are the <c>UnparsedValues</c> warning,
    /// which says ICM sent a date in a shape the client does not recognise, and an empty
    /// result, which is usually <c>ViewMode</c> rather than an empty database.
    /// </para>
    /// </remarks>
    public static class Program
    {
        /// <summary>Runs the query.</summary>
        /// <param name="args">
        /// Configuration overrides in <c>--Key:Path=value</c> form, e.g.
        /// <c>--Query:PageSize=1</c>.
        /// </param>
        /// <returns>0 on success, 1 on a configuration problem, 2 on a failed call.</returns>
        public static async Task<int> Main(string[] args)
        {
            // A plain console app gets no IConfiguration of its own - the automatic one
            // belongs to the generic host, which this does not use. Built by hand instead,
            // lowest priority first.
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)

                // The user-secret store (see UserSecretsId in the csproj), which is where
                // the client secret goes - it lives under the user profile, not in the
                // repository, so there is nothing to gitignore and nothing to leak.
                //
                // Added unconditionally rather than only in Development, which is what the
                // generic host would do: a console app's environment defaults to
                // Production, so a Development-only registration here would silently load
                // nothing and every run would fail on a placeholder.
                // (the assembly overload, because Program is a static class)
                .AddUserSecrets(typeof(Program).Assembly, optional: true)

                // Icm_Icm__Auth__ClientSecret=... for a shared machine or CI, where the
                // per-user secret store is the wrong place. Same prefix idea as MyssApi's
                // Myss_.
                .AddEnvironmentVariables(prefix: "Icm_")

                // Highest priority: one-off overrides like --Query:PageSize=1.
                .AddCommandLine(args)
                .Build();

            ConsoleSettings settings = configuration.Get<ConsoleSettings>() ?? new ConsoleSettings();

            if (!settings.TryValidate(out IReadOnlyList<string>? problems))
            {
                Console.Error.WriteLine("The settings are not usable yet:");
                foreach (string problem in problems)
                {
                    Console.Error.WriteLine($"  - {problem}");
                }

                Console.Error.WriteLine();
                Console.Error.WriteLine("Set them from Apps/IcmApi.Console with, for example:");
                Console.Error.WriteLine();
                Console.Error.WriteLine("  dotnet user-secrets set \"Icm:Auth:ClientSecret\" \"…\"");
                Console.Error.WriteLine();
                Console.Error.WriteLine(
                    "Non-secret settings can go straight into appsettings.json. Secrets should not: "
                    + "that file is committed.");
                return 1;
            }

            return await RunAsync(settings);
        }

        private static async Task<int> RunAsync(ConsoleSettings settings)
        {
            OAuthClientCredentials credentials = new()
            {
                TokenUrl = new Uri(settings.Icm.Auth.ResolveTokenUrl()!),
                ClientId = settings.Icm.Auth.ClientId,
                ClientSecret = settings.Icm.Auth.ClientSecret,
                Scopes = settings.Icm.Auth.Scopes.Count == 0 ? null : settings.Icm.Auth.Scopes,
            };

            bool raw = string.Equals(settings.Output, "raw", StringComparison.OrdinalIgnoreCase);

            // HttpClient takes ownership of the handler chain and disposes it, which is
            // what the `using` on the client covers.
            using HttpMessageHandler icmHandler = raw
                ? new RawResponseHandler { InnerHandler = new HttpClientHandler() }
                : new HttpClientHandler();
            using HttpClient icmClient = new(icmHandler, disposeHandler: false);
            icmClient.BaseAddress = new Uri(settings.Icm.BaseUrl!);
            icmClient.Timeout = TimeSpan.FromSeconds(settings.Icm.TimeoutSeconds);

            using OAuthTokenRepository tokenRepository = new();
            using OAuthTokenService tokenService = new(tokenRepository);
            IServiceRequestService serviceRequests = new ServiceRequestService(
                new ServiceRequestRepository(icmClient, settings.Icm.TrustedUserName),
                tokenService,
                credentials);

            // The same HttpClient on purpose: same host, same raw-response handler, so
            // --Output=raw shows the workflow's untouched answer too.
            IBusPassService busPass = new BusPassService(
                new BusPassRepository(icmClient, settings.Icm.TrustedUserName),
                tokenService,
                credentials);

            WriteHeader(settings, credentials);

            // The two calls are made in two stages on purpose. Going straight to the search
            // works — the service fetches its own token — but then a rejected credential
            // and a rejected ICM request arrive as the same exception type from the same
            // line, and the tool reports "ICM returned 401" for a request that never
            // reached ICM. Asking for the token first makes the attribution exact. It costs
            // nothing: the service reuses the cached token rather than asking again.
            if (await AuthenticateAsync(tokenService, credentials) is { } authFailure)
            {
                return authFailure;
            }

            if (settings.IsBusPassMode)
            {
                return await SubmitBusPassAsync(busPass, serviceRequests, settings);
            }

            int searchResult = await SearchAsync(serviceRequests, settings);
            if (searchResult != 0 || string.IsNullOrWhiteSpace(settings.Query.ServiceRequestKey))
            {
                return searchResult;
            }

            return await GetOneAsync(serviceRequests, settings);
        }

        /// <summary>
        /// Bus pass mode: submit through the workflow, then find and read back the
        /// service request it created — the closest thing to an integration test that can
        /// be run by hand.
        /// </summary>
        /// <remarks>
        /// The read-back cannot ask for the SR by its number (the business component does
        /// not expose it as a searchable field), so it searches recent Bus Pass SRs and
        /// matches <c>Service Request Number</c> against the returned
        /// <c>ApplicationNumber</c> — which also verifies the assumption that the two are
        /// the same number.
        /// </remarks>
        private static async Task<int> SubmitBusPassAsync(
            IBusPassService busPass, IServiceRequestService serviceRequests, ConsoleSettings settings)
        {
            BusPassApplication application = settings.BusPass.ToApplication();
            Console.WriteLine(
                $"Submitting bus pass request ({application.RequestType}, "
                + $"{application.FirstName} {application.LastName})...");
            Console.WriteLine();

            Stopwatch stopwatch = Stopwatch.StartNew();
            BusPassResult result;
            try
            {
                result = await busPass.SubmitAsync(application);
                stopwatch.Stop();
            }
            catch (ApiException exception)
            {
                stopwatch.Stop();
                return Fail(
                    stopwatch,
                    $"ICM returned {(int)exception.StatusCode} {exception.StatusCode}.",
                    Explain(exception),
                    responseBody: exception.Content);
            }
            catch (ApiRequestException exception)
            {
                stopwatch.Stop();
                return FailUnreachable(stopwatch, exception, "ICM");
            }
            catch (IcmResponseException exception)
            {
                stopwatch.Stop();
                return Fail(
                    stopwatch, "ICM reported success but the response was not usable.", exception.Message);
            }

            Console.WriteLine($"Workflow answered in {stopwatch.ElapsedMilliseconds} ms:");
            Console.WriteLine($"  ApplicationNumber  {result.ApplicationNumber ?? "(empty)"}");
            Console.WriteLine($"  Status             {result.Status ?? "(empty)"}");
            Console.WriteLine($"  First / Last Name  {result.FirstName ?? "-"} / {result.LastName ?? "-"}");
            Console.WriteLine($"  Error Code         {result.ErrorCode ?? "(empty)"}");
            Console.WriteLine($"  Error Message      {result.ErrorMessage ?? "(empty)"}");
            foreach ((string key, System.Text.Json.JsonElement value) in result.AdditionalFields)
            {
                // An unexpected key here means the out-args carry names the contract does
                // not model - exactly the mismatch the SR API had, caught as data.
                Console.WriteLine($"  UNMODELLED         {key} = {value.GetRawText()}");
            }

            Console.WriteLine();

            if (!string.IsNullOrWhiteSpace(result.ErrorCode))
            {
                Console.Error.WriteLine(
                    "The workflow reported a business rejection. The submission may still have "
                    + "created an error service request (SR Sub Type 'Error - Web') worth looking at.");
                return 2;
            }

            if (string.IsNullOrWhiteSpace(result.ApplicationNumber))
            {
                Console.Error.WriteLine(
                    "No error code, but no application number either - nothing to read back. "
                    + "Check the UNMODELLED lines above for where the number actually landed.");
                return 2;
            }

            return await ReadBackBusPassAsync(serviceRequests, settings, result.ApplicationNumber);
        }

        /// <summary>Finds the created SR by its number among recent Bus Pass SRs and reads it whole.</summary>
        private static async Task<int> ReadBackBusPassAsync(
            IServiceRequestService serviceRequests, ConsoleSettings settings, string applicationNumber)
        {
            // Yesterday rather than today: ICM's Created is server-local time, and a
            // midnight boundary should not make the read-back miss its own submission.
            string since = DateTime.Now.AddDays(-1).ToString("MM/dd/yyyy", CultureInfo.InvariantCulture);
            Console.WriteLine(new string('-', 60));
            Console.WriteLine($"Reading back: searching Bus Pass SRs created since {since} "
                + $"for number {applicationNumber}");
            Console.WriteLine(new string('-', 60));
            Console.WriteLine();

            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                // Paged rather than first-100-only: ICM ignores sort requests on this
                // endpoint, so on a busy day the just-created record can land on any
                // page and stopping early would misreport it as missing.
                const int pageSize = 100;
                ServiceRequest? created = null;
                int seen = 0;

                for (int startRow = 0; ; startRow += pageSize)
                {
                    ServiceRequestPage page = await serviceRequests.SearchAsync(new ServiceRequestQuery
                    {
                        SearchSpec = $"[SR Type] = \"Bus Pass\" AND [Created] >= \"{since}\"",
                        ViewMode = settings.Query.ViewMode ?? "Organization",
                        PageSize = pageSize,
                        StartRowNum = startRow,
                    });

                    seen += page.Items.Count;
                    created = page.Items.FirstOrDefault(item =>
                        string.Equals(item.ServiceRequestNumber, applicationNumber, StringComparison.Ordinal));

                    if (created is not null || page.Items.Count < pageSize)
                    {
                        break;
                    }
                }

                if (created?.Id is null)
                {
                    stopwatch.Stop();
                    Console.Error.WriteLine(
                        $"FAILED: {seen} recent Bus Pass SR(s), none numbered "
                        + $"{applicationNumber} ({stopwatch.ElapsedMilliseconds} ms).");
                    Console.Error.WriteLine(
                        "Either ApplicationNumber is not the SR number after all, or the record "
                        + "is not visible in this ViewMode. The ICM UI can settle which.");
                    return 2;
                }

                ServiceRequest? record = await serviceRequests.GetAsync(
                    created.Id, settings.Query.ToReadOptions());
                stopwatch.Stop();

                Console.WriteLine(
                    $"Created service request found and read back in {stopwatch.ElapsedMilliseconds} ms.");
                Console.WriteLine();
                ServiceRequestPrinter.Write(
                    new ServiceRequestPage { Items = [record ?? created] }, full: true);
                return 0;
            }
            catch (ApiException exception)
            {
                stopwatch.Stop();
                return Fail(
                    stopwatch,
                    $"ICM returned {(int)exception.StatusCode} {exception.StatusCode}.",
                    Explain(exception),
                    responseBody: exception.Content);
            }
            catch (ApiRequestException exception)
            {
                stopwatch.Stop();
                return FailUnreachable(stopwatch, exception, "ICM");
            }
            catch (IcmResponseException exception)
            {
                stopwatch.Stop();
                return Fail(
                    stopwatch, "ICM reported success but the response was not usable.", exception.Message);
            }
        }

        /// <summary>Stage three: read one named record, to check a specific case by hand.</summary>
        private static async Task<int> GetOneAsync(
            IServiceRequestService serviceRequests, ConsoleSettings settings)
        {
            string serviceRequestKey = settings.Query.ServiceRequestKey!;
            Console.WriteLine();
            Console.WriteLine(new string('-', 60));
            Console.WriteLine($"Reading service request {serviceRequestKey}");
            Console.WriteLine(new string('-', 60));
            Console.WriteLine();

            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                // The same visibility and field settings as the search: without them the
                // read silently used ICM's defaults, which made a ViewMode experiment on
                // this step do nothing at all.
                ServiceRequest? record = await serviceRequests.GetAsync(
                    serviceRequestKey, settings.Query.ToReadOptions());
                stopwatch.Stop();

                if (record is null)
                {
                    Console.WriteLine($"Not found ({stopwatch.ElapsedMilliseconds} ms).");
                    Console.WriteLine();
                    Console.WriteLine(
                        "ICM reports \"no such record\" and \"not yours to see\" the same way, so this "
                        + "is either a wrong row id or a visibility question — try widening "
                        + "Query:ViewMode before assuming the record is gone.");
                    return 0;
                }

                Console.WriteLine($"Found in {stopwatch.ElapsedMilliseconds} ms.");
                Console.WriteLine();
                ServiceRequestPrinter.Write(new ServiceRequestPage { Items = [record] }, full: true);
                return 0;
            }
            catch (ApiException exception)
            {
                stopwatch.Stop();
                return Fail(
                    stopwatch,
                    $"ICM returned {(int)exception.StatusCode} {exception.StatusCode}.",
                    Explain(exception),
                    responseBody: exception.Content);
            }
            catch (ApiRequestException exception)
            {
                stopwatch.Stop();
                return FailUnreachable(stopwatch, exception, "ICM");
            }
            catch (IcmResponseException exception)
            {
                stopwatch.Stop();
                return Fail(
                    stopwatch, "ICM reported success but the response was not usable.", exception.Message);
            }
        }

        /// <summary>Stage one: prove the credentials work, and warm the token cache.</summary>
        /// <returns>Null when the token was obtained; the exit code when it was not.</returns>
        private static async Task<int?> AuthenticateAsync(
            IOAuthTokenService tokenService, OAuthClientCredentials credentials)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                await tokenService.GetTokenAsync(credentials);
                stopwatch.Stop();
                Console.WriteLine($"Token acquired in {stopwatch.ElapsedMilliseconds} ms.");
                Console.WriteLine();
                return null;
            }
            catch (ApiException exception)
            {
                stopwatch.Stop();
                return Fail(
                    stopwatch,
                    $"The authorization server rejected the credentials "
                    + $"({(int)exception.StatusCode} {exception.StatusCode}).",
                    ExplainToken(exception),
                    hint: "Nothing was sent to ICM — this failed before the ICM call.",
                    responseBody: exception.Content);
            }
            catch (ApiRequestException exception)
            {
                stopwatch.Stop();
                return FailUnreachable(stopwatch, exception, "the authorization server");
            }
            catch (OAuthTokenException exception)
            {
                stopwatch.Stop();
                return Fail(
                    stopwatch,
                    "The authorization server answered without a usable token.",
                    exception.Message);
            }
        }

        /// <summary>Stage two: the search this tool exists to run.</summary>
        private static async Task<int> SearchAsync(
            IServiceRequestService serviceRequests, ConsoleSettings settings)
        {
            ServiceRequestQuery query = settings.Query.ToQuery();
            Stopwatch stopwatch = Stopwatch.StartNew();

            try
            {
                ServiceRequestPage page = await serviceRequests.SearchAsync(query);
                stopwatch.Stop();

                Console.WriteLine($"Search OK in {stopwatch.ElapsedMilliseconds} ms.");
                Console.WriteLine();
                ServiceRequestPrinter.Write(page, full: IsFull(settings));
                return 0;
            }
            catch (ApiException exception)
            {
                stopwatch.Stop();
                return Fail(
                    stopwatch,
                    $"ICM returned {(int)exception.StatusCode} {exception.StatusCode}.",
                    Explain(exception),
                    responseBody: exception.Content);
            }
            catch (ApiRequestException exception)
            {
                stopwatch.Stop();
                return FailUnreachable(stopwatch, exception, "ICM");
            }
            catch (IcmResponseException exception)
            {
                stopwatch.Stop();
                return Fail(
                    stopwatch, "ICM reported success but the response was not usable.", exception.Message);
            }
        }

        /// <summary>
        /// Reports a call that never got an answer. Refit uses its own exception type for
        /// this — a sibling of <see cref="ApiException"/>, not a subclass — with the real
        /// cause underneath.
        /// </summary>
        private static int FailUnreachable(Stopwatch stopwatch, ApiRequestException exception, string who)
        {
            bool timedOut = exception.InnerException is TaskCanceledException or TimeoutException;

            return timedOut
                ? Fail(
                    stopwatch,
                    $"Timed out waiting for {who}.",
                    "Raise Icm:TimeoutSeconds, or narrow the query with Query:PageSize and Query:Fields.")
                : Fail(
                    stopwatch,
                    $"Could not reach {who} — no response at all.",
                    exception.InnerException?.Message ?? exception.Message,
                    hint: "If the host did not resolve, the ministry VPN is almost certainly down. "
                        + "ICM is on the internal network; the authorization server is not, so this "
                        + "failing while the token succeeded is the usual shape of a VPN problem.");
        }

        /// <summary>
        /// Whether to print every mapped field per record. Only <c>summary</c> does not —
        /// <c>raw</c> shows the untouched response as well, which is a superset.
        /// </summary>
        private static bool IsFull(ConsoleSettings settings) =>
            !string.Equals(settings.Output, "summary", StringComparison.OrdinalIgnoreCase);

        /// <summary>Turns a token-endpoint status into the thing most likely to have caused it.</summary>
        private static string ExplainToken(ApiException exception) => (int)exception.StatusCode switch
        {
            400 or 401 => "Check Icm:Auth:Realm first — a client registered in a different realm "
                        + "fails exactly like a wrong secret. Then the secret itself: a $ in a "
                        + "double-quoted `dotnet user-secrets set` is expanded away by the shell, "
                        + "so the stored value can differ from the one that works elsewhere while "
                        + "looking identical.",
            403 => "The client authenticated but is not allowed to request this grant or these scopes.",
            404 => "The token URL is wrong. It should end in /protocol/openid-connect/token for a "
                 + "Keycloak realm.",
            _ => "See the response body below.",
        };

        private static void WriteHeader(ConsoleSettings settings, OAuthClientCredentials credentials)
        {
            Console.WriteLine(settings.IsBusPassMode
                ? "ICM bus pass submission (transaction INT-316) + read-back"
                : "ICM Service Request query");
            Console.WriteLine(new string('-', 60));
            Console.WriteLine($"  Mode         {settings.Mode}");
            Console.WriteLine($"  ICM          {settings.Icm.BaseUrl}");
            Console.WriteLine(
                $"  Trusted user {settings.Icm.TrustedUserName ?? "(none - no X-ICM-TrustedUserName header)"}");
            Console.WriteLine(
                $"  Realm        {settings.Icm.Auth.Realm ?? "-"}"
                + (settings.Icm.Auth.IsTokenUrlOverridden
                    ? "   (ignored: Icm:Auth:TokenUrl is set and overrides it)"
                    : string.Empty));
            Console.WriteLine($"  Token URL    {credentials.TokenUrl}");
            Console.WriteLine($"  Client       {credentials.ClientId}");
            Console.WriteLine($"  Scopes       {credentials.GetScopeParameter() ?? "(client default)"}");
            Console.WriteLine($"  SearchSpec   {settings.Query.SearchSpec ?? "(none - matching everything)"}");
            Console.WriteLine(
                $"  Read by key  {settings.Query.ServiceRequestKey ?? "(skipped)"}");
            Console.WriteLine($"  Fields       {(settings.Query.Fields.Count == 0 ? "(all)" : string.Join(", ", settings.Query.Fields))}");
            Console.WriteLine($"  ViewMode     {settings.Query.ViewMode ?? "(ICM default: Sales Rep)"}");
            Console.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"  Paging       {settings.Query.PageSize} from row {settings.Query.StartRowNum}"));
            Console.WriteLine($"  Output       {settings.Output}");
            Console.WriteLine(new string('-', 60));
            Console.WriteLine();

            // The secret is never printed, not even partially. There is nothing this tool
            // could tell you with it that is worth it appearing in a terminal scrollback.
        }

        /// <summary>Turns a status code into the thing most likely to have caused it.</summary>
        private static string Explain(ApiException exception) => (int)exception.StatusCode switch
        {
            401 => "The token was rejected. Check the client id and secret, and that the token URL "
                 + "is the realm ICM trusts.",
            403 => "Either the source IP is not allowlisted — the response body says so when that "
                 + "is the cause, and the fix is the VPN, not the settings — or Icm:TrustedUserName "
                 + "is missing or names a user ICM will not act as, or the client is authenticated "
                 + "but not permitted to read service requests.",
            404 => "The path was not found. Check Icm:BaseUrl includes the version prefix "
                 + "(e.g. /gov/v1.0).",
            400 => "ICM rejected the request. A malformed SearchSpec is the usual cause - it is a "
                 + "raw Siebel expression and is passed through as written.",
            _ => "See the response body below.",
        };

        /// <summary>Reports a failed run.</summary>
        /// <param name="stopwatch">How long it took to fail.</param>
        /// <param name="headline">What went wrong.</param>
        /// <param name="detail">The underlying message, or the likely cause.</param>
        /// <param name="hint">What to try next. Not a response body — see below.</param>
        /// <param name="responseBody">What ICM actually sent back, when it sent anything.</param>
        /// <returns>The process exit code.</returns>
        private static int Fail(
            Stopwatch stopwatch,
            string headline,
            string? detail,
            string? hint = null,
            string? responseBody = null)
        {
            Console.Error.WriteLine($"FAILED after {stopwatch.ElapsedMilliseconds} ms.");
            Console.Error.WriteLine();
            Console.Error.WriteLine($"  {headline}");

            if (!string.IsNullOrWhiteSpace(detail))
            {
                Console.Error.WriteLine($"  {detail}");
            }

            if (!string.IsNullOrWhiteSpace(hint))
            {
                Console.Error.WriteLine($"  {hint}");
            }

            if (!string.IsNullOrWhiteSpace(responseBody))
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine("  Response:");
                Console.Error.WriteLine($"  {responseBody.Trim()}");
            }

            return 2;
        }
    }
}
