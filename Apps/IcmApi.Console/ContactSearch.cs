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

    /// <summary>Contact mode, as its own class so <see cref="Program"/> stays readable.</summary>
    internal static class ContactSearch
    {
        /// <summary>
        /// Contact mode: one search through <see cref="IContactService"/> with whatever
        /// criteria are set, printing which fields came back rather than their values
        /// unless asked.
        /// </summary>
        public static async Task<int> RunAsync(
            HttpClient icmClient,
            IOAuthTokenService tokenService,
            OAuthClientCredentials credentials,
            ConsoleSettings settings)
        {
            IContactService contacts = new ContactService(
                new ContactRepository(icmClient, settings.Icm.TrustedUserName),
                tokenService,
                credentials);

            // Which criteria, never their values: every one of them identifies a person.
            Console.WriteLine($"Searching contacts on {string.Join(", ", settings.Contact.CriteriaSet)}...");
            Console.WriteLine();

            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                ContactPage page = await contacts.SearchAsync(settings.Contact.ToQuery());
                stopwatch.Stop();

                Console.WriteLine(
                    $"{page.Items.Count} contact(s) on this page, "
                    + $"{page.TotalCount?.ToString(CultureInfo.InvariantCulture) ?? "unknown"} in total, "
                    + $"in {stopwatch.ElapsedMilliseconds} ms.");
                if (page.Items.Count == 0)
                {
                    Console.WriteLine();
                    Console.WriteLine(
                        "ICM reports \"no such contact\" and \"not yours to see\" the same way, so this is "
                        + "either no match or a visibility question — try "
                        + "--Contact:ViewMode=Organization before assuming the former.");
                }

                foreach (Contact contact in page.Items)
                {
                    Console.WriteLine();
                    ContactPrinter.Write(contact, settings.Contact.ShowValues);
                }

                return 0;
            }
            catch (ArgumentException exception)
            {
                // The library refused the query before sending anything. Its message
                // names the criterion and never the value.
                stopwatch.Stop();
                Console.Error.WriteLine($"The contact search is not usable: {exception.Message}");
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
