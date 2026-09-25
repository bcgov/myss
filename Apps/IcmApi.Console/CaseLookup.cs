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

    /// <summary>Case mode, as its own class so <see cref="Program"/> stays readable.</summary>
    internal static class CaseLookup
    {
        /// <summary>
        /// Case mode: read a case by key or find it by criteria, then read the people on it
        /// — the hand-run check that the case field lists, the child-collection path and
        /// the visibility default hold against a real ICM.
        /// </summary>
        public static async Task<int> RunAsync(ICaseService cases, ConsoleSettings settings)
        {
            CaseSettings wanted = settings.Case;
            Console.WriteLine(wanted.Id is not null
                ? $"Reading case {wanted.Id}..."
                : $"Searching cases on {string.Join(", ", wanted.CriteriaSet)}...");
            Console.WriteLine();

            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                IReadOnlyList<Case> found;
                if (wanted.Id is not null)
                {
                    Case? record = await cases.GetAsync(wanted.Id, wanted.ToReadOptions());
                    found = record is null ? [] : [record];
                    Console.WriteLine(record is null
                        ? $"Not found ({stopwatch.ElapsedMilliseconds} ms)."
                        : $"Found in {stopwatch.ElapsedMilliseconds} ms.");
                }
                else
                {
                    CasePage page = await cases.SearchAsync(wanted.ToQuery());
                    found = page.Items;
                    Console.WriteLine(
                        $"{page.Items.Count} case(s) on this page, "
                        + $"{page.TotalCount?.ToString(CultureInfo.InvariantCulture) ?? "unknown"} in total, "
                        + $"in {stopwatch.ElapsedMilliseconds} ms.");
                }

                if (found.Count == 0)
                {
                    Console.WriteLine();
                    Console.WriteLine(
                        "ICM reports \"no such case\" and \"not yours to see\" the same way. The library "
                        + "already asks for ViewMode=Manager (the one MEASURED to see cases); try "
                        + "--Case:ViewMode=Group or Catalog before assuming the case is not there.");
                    return 0;
                }

                Console.WriteLine();
                for (int i = 0; i < found.Count; i++)
                {
                    CasePrinter.WriteCase(i + 1, found[i], wanted.ShowValues);

                    if (!wanted.IncludeContacts || found[i].Id is not { } key)
                    {
                        continue;
                    }

                    Stopwatch childWatch = Stopwatch.StartNew();
                    IReadOnlyList<CaseContact> people = await cases.GetContactsAsync(key, wanted.ToReadOptions());
                    Console.WriteLine($"       Contacts read in {childWatch.ElapsedMilliseconds} ms.");
                    CasePrinter.WriteContacts(people, wanted.ShowValues);
                }

                return 0;
            }
            catch (ArgumentException exception)
            {
                stopwatch.Stop();
                Console.Error.WriteLine($"The case lookup is not usable: {exception.Message}");
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
    }
}
