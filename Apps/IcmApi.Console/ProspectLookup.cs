namespace Icm.Api.ConsoleApp
{
    using System.Diagnostics;
    using System.Globalization;
    using Icm.Api.ConsoleApp.Configuration;
    using Icm.Api.ConsoleApp.Output;
    using Icm.Api.Models;
    using Icm.Api.Repositories;
    using Icm.Api.Services;
    using Refit;

    /// <summary>Prospect mode, as its own class so <see cref="Program"/> stays readable.</summary>
    internal static class ProspectLookup
    {
        /// <summary>
        /// Prospect mode: read the prospects on a service request, one prospect by key, or
        /// a standalone search — or, with <c>Prospect:Create:Enabled</c>, <b>create one</b>
        /// on the path <c>Prospect:Create:Path</c> names and read the service request's
        /// prospects back. The hand-run check of the two paths to the component, and of
        /// whether a directly created row links to its service request.
        /// </summary>
        public static async Task<int> RunAsync(
            HttpClient icmClient,
            IOAuthTokenService tokenService,
            OAuthClientCredentials credentials,
            ConsoleSettings settings)
        {
            // Built here rather than in Program, which is already coupled to as many
            // types as the analyzer allows; same client, same trusted user as every mode.
            IProspectService prospects = new ProspectService(
                new ProspectRepository(icmClient, settings.Icm.TrustedUserName),
                tokenService,
                credentials);
            ProspectSettings wanted = settings.Prospect;

            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                if (wanted.Create.Enabled)
                {
                    return await CreateAsync(prospects, wanted, stopwatch);
                }

                IReadOnlyList<Prospect> found;
                if (wanted.ServiceRequestId is not null)
                {
                    Console.WriteLine($"Reading the prospects on service request {wanted.ServiceRequestId}...");
                    found = await prospects.GetForServiceRequestAsync(wanted.ServiceRequestId, wanted.ToReadOptions());
                    Console.WriteLine($"{found.Count} row(s) in {stopwatch.ElapsedMilliseconds} ms.");
                }
                else if (wanted.Id is not null)
                {
                    Console.WriteLine($"Reading prospect {wanted.Id} on the standalone path...");
                    Prospect? row = await prospects.GetAsync(wanted.Id, wanted.ToReadOptions());
                    found = row is null ? [] : [row];
                    Console.WriteLine(row is null
                        ? $"Not found ({stopwatch.ElapsedMilliseconds} ms)."
                        : $"Found in {stopwatch.ElapsedMilliseconds} ms.");
                }
                else
                {
                    Console.WriteLine($"Searching prospects on {string.Join(", ", wanted.CriteriaSet)} (standalone path)...");
                    ProspectPage page = await prospects.SearchAsync(wanted.ToQuery());
                    found = page.Items;
                    Console.WriteLine(
                        $"{page.Items.Count} row(s) on this page, "
                        + $"{page.TotalCount?.ToString(CultureInfo.InvariantCulture) ?? "unknown"} in total, "
                        + $"in {stopwatch.ElapsedMilliseconds} ms.");
                }

                Console.WriteLine();
                if (found.Count == 0)
                {
                    Console.WriteLine(
                        "ICM reports \"no such row\" and \"not yours to see\" the same way. The library asks "
                        + "for ViewMode=Organization (the mode every prospect read so far was made under); "
                        + "try --Prospect:ViewMode=Manager before assuming the row is not there.");
                    return 0;
                }

                ProspectPrinter.WriteProspects(found, wanted.ShowValues);
                return 0;
            }
            catch (ArgumentException exception)
            {
                stopwatch.Stop();
                Console.Error.WriteLine($"The prospect lookup is not usable: {exception.Message}");
                return 1;
            }
            catch (ApiException exception)
            {
                stopwatch.Stop();
                return Program.Fail(
                    stopwatch,
                    $"ICM returned {(int)exception.StatusCode} {exception.StatusCode}.",
                    Program.Explain(exception),
                    responseBody: exception.Content);
            }
            catch (ApiRequestException exception)
            {
                stopwatch.Stop();
                return Program.FailUnreachable(stopwatch, exception, "ICM");
            }
            catch (IcmResponseException exception)
            {
                stopwatch.Stop();
                return Program.Fail(
                    stopwatch, "ICM reported success but the response was not usable.", exception.Message);
            }
        }

        /// <summary>The run header's title for this mode.</summary>
        /// <param name="settings">The settings.</param>
        /// <returns>The title.</returns>
        public static string Title(ConsoleSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            return settings.Prospect.Create.Enabled ? "ICM prospect CREATE + read-back" : "ICM prospect lookup";
        }

        /// <summary>Writes the mode's lines of the run header.</summary>
        /// <param name="settings">The settings.</param>
        public static void WriteHeader(ConsoleSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ProspectSettings wanted = settings.Prospect;

            if (wanted.Create.Enabled)
            {
                Console.WriteLine($"  Create       PUT Prospects/SRProspects, Id NULL");
                Console.WriteLine($"  Under SR     {wanted.Create.ServiceRequestId}");
            }
            else
            {
                Console.WriteLine($"  Criteria     {string.Join(", ", wanted.CriteriaSet)}");
            }

            Console.WriteLine($"  ViewMode     {wanted.ViewMode ?? "(library default: Organization)"}");
            Console.WriteLine($"  Values       {(wanted.ShowValues ? "shown" : "hidden (--Prospect:ShowValues=true to print them)")}");
        }

        private static async Task<int> CreateAsync(IProspectService prospects, ProspectSettings wanted, Stopwatch stopwatch)
        {
            ProspectCreateSettings create = wanted.Create;
            ProspectInput input = create.ToInput();

            ServiceRequestPrinter.WriteWarning(
                $"Creating a prospect on service request {create.ServiceRequestId} (PUT Prospects/SRProspects, Id NULL)"
                + " - this writes a row to the target ICM.");
            Console.WriteLine();

            Prospect created = await prospects.CreateAsync(input);

            Console.WriteLine($"Created in {stopwatch.ElapsedMilliseconds} ms. ICM returned:");
            Console.WriteLine();
            ProspectPrinter.WriteProspects([created], wanted.ShowValues);

            // The proof is the read-back under the service request, not the write's own
            // answer: a row the child collection does not list is a row the workflow's
            // consumers will never see.
            Stopwatch readBack = Stopwatch.StartNew();
            IReadOnlyList<Prospect> rows = await prospects.GetForServiceRequestAsync(create.ServiceRequestId!, wanted.ToReadOptions());
            bool linked = rows.Any(row => string.Equals(row.Id, created.Id, StringComparison.Ordinal));
            Console.WriteLine(
                $"Read back {rows.Count} row(s) under service request {create.ServiceRequestId} in {readBack.ElapsedMilliseconds} ms: "
                + (linked ? $"the new row {created.Id} IS listed." : $"the new row {created.Id} is NOT listed."));
            if (!linked)
            {
                ServiceRequestPrinter.WriteWarning(
                    "The create succeeded but the service request does not list the row.");
            }

            return 0;
        }
    }
}
