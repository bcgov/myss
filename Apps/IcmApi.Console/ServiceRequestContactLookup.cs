namespace Icm.Api.ConsoleApp
{
    using System.Diagnostics;
    using System.Reflection;
    using Icm.Api.ConsoleApp.Configuration;
    using Icm.Api.ConsoleApp.Output;
    using Icm.Api.Models;
    using Icm.Api.Repositories;
    using Icm.Api.Services;
    using Refit;

    /// <summary>SR-contact mode, as its own class so <see cref="Program"/> stays readable.</summary>
    internal static class ServiceRequestContactLookup
    {
        private static readonly PropertyInfo[] Fields = [.. typeof(ServiceRequestContact)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.Name is not (nameof(ServiceRequestContact.UnparsedValues) or nameof(ServiceRequestContact.AdditionalFields)))];

        /// <summary>
        /// SR-contact mode: the contacts on a service request with their relationship, or —
        /// with <c>SrContact:ContactId</c> — <b>link</b> an existing contact to it first, the
        /// way the bus pass workflow files a matched applicant, and read back.
        /// </summary>
        public static async Task<int> RunAsync(
            HttpClient icmClient,
            IOAuthTokenService tokenService,
            OAuthClientCredentials credentials,
            ConsoleSettings settings)
        {
            IServiceRequestService serviceRequests = new ServiceRequestService(
                new ServiceRequestRepository(icmClient, settings.Icm.TrustedUserName), tokenService, credentials);
            SrContactSettings wanted = settings.SrContact;
            ServiceRequestReadOptions options = new() { ViewMode = wanted.ViewMode ?? "Organization", ExcludeEmptyFields = true };

            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                if (wanted.ContactId is not null)
                {
                    ServiceRequestPrinter.WriteWarning(
                        $"Linking contact {wanted.ContactId} to service request {wanted.ServiceRequestId} as {wanted.Relationship ?? "Key player"}"
                        + $"{(wanted.IsPrimary ?? true ? ", primary" : string.Empty)} (PUT …/ICMContact) - this writes to the target ICM.");
                    string id = await serviceRequests.LinkContactAsync(wanted.ServiceRequestId!, wanted.ToLink());
                    Console.WriteLine($"Linked in {stopwatch.ElapsedMilliseconds} ms; ICM echoed service request {id}.");
                    Console.WriteLine();
                }

                Console.WriteLine($"Reading the contacts on service request {wanted.ServiceRequestId}...");
                Stopwatch readWatch = Stopwatch.StartNew();
                IReadOnlyList<ServiceRequestContact> people = await serviceRequests.GetContactsAsync(wanted.ServiceRequestId!, options);
                Console.WriteLine($"{people.Count} contact(s) in {readWatch.ElapsedMilliseconds} ms.");
                Console.WriteLine();
                for (int i = 0; i < people.Count; i++)
                {
                    ServiceRequestContact person = people[i];
                    // The row id, relationship and flag are what the next call needs and are
                    // printed either way; the name is personal information and follows ShowValues.
                    Console.WriteLine(
                        $"  [{i + 1}] {person.Relationship ?? "(no relationship)"}{(person.IsPrimary == true ? ", primary" : string.Empty)}"
                        + $"  contact {person.Id ?? "(none)"}  {Name(person.FirstName, person.LastName, wanted.ShowValues)}");
                    if (wanted.ShowValues)
                    {
                        CasePrinter.WriteFields(person, Fields, person.AdditionalFields, person.UnparsedValues, showValues: true, "ServiceRequestContact");
                        Console.WriteLine();
                    }
                }

                ServiceRequest? header = await serviceRequests.GetAsync(wanted.ServiceRequestId!, new ServiceRequestReadOptions
                {
                    Fields = ["Id", "Primary Contact Id", "Given Names", "Last Name"], ViewMode = options.ViewMode,
                });
                Console.WriteLine();
                Console.WriteLine(header is null
                    ? "The service request itself could not be read back."
                    : $"SR header: Primary Contact Id {header.PrimaryContactId ?? "(none)"}, Given Names {Quote(header.GivenNames, wanted.ShowValues)}, Last Name {Quote(header.LastName, wanted.ShowValues)}");
                return 0;
            }
            catch (ArgumentException exception)
            {
                stopwatch.Stop();
                Console.Error.WriteLine($"The SR-contact lookup is not usable: {exception.Message}");
                return 1;
            }
            catch (ApiException exception)
            {
                stopwatch.Stop();
                return Program.Fail(stopwatch, $"ICM returned {(int)exception.StatusCode} {exception.StatusCode}.", Program.Explain(exception), responseBody: exception.Content);
            }
            catch (ApiRequestException exception)
            {
                stopwatch.Stop();
                return Program.FailUnreachable(stopwatch, exception, "ICM");
            }
            catch (IcmResponseException exception)
            {
                stopwatch.Stop();
                return Program.Fail(stopwatch, "ICM reported success but the response was not usable.", exception.Message);
            }
        }

        /// <summary>A person's name, or only whether one came back when values are hidden.</summary>
        private static string Name(string? first, string? last, bool showValues)
        {
            if (showValues)
            {
                return $"{first} {last}".Trim();
            }

            bool any = !string.IsNullOrWhiteSpace(first) || !string.IsNullOrWhiteSpace(last);
            return any ? "(name set)" : "(no name)";
        }

        /// <summary>A quoted header value, or only whether it is set when values are hidden.</summary>
        private static string Quote(string? value, bool showValues)
        {
            if (showValues)
            {
                return $"\"{value}\"";
            }

            return string.IsNullOrWhiteSpace(value) ? "(empty)" : "(set)";
        }

        /// <summary>The run header's title for this mode.</summary>
        /// <param name="settings">The settings.</param>
        /// <returns>The title.</returns>
        public static string Title(ConsoleSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            return settings.SrContact.ContactId is null ? "ICM service request contacts" : "ICM LINK contact to service request + read-back";
        }

        /// <summary>Writes the mode's lines of the run header.</summary>
        /// <param name="settings">The settings.</param>
        public static void WriteHeader(ConsoleSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            Console.WriteLine($"  SR           {settings.SrContact.ServiceRequestId}");
            Console.WriteLine($"  Contact      {settings.SrContact.ContactId ?? "(none - read only)"}");
            Console.WriteLine($"  ViewMode     {settings.SrContact.ViewMode ?? "Organization"}");
            Console.WriteLine($"  Values       {(settings.SrContact.ShowValues ? "shown" : "hidden (--SrContact:ShowValues=true to print them)")}");
        }
    }
}
