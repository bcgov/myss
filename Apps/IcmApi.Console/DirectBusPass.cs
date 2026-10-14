namespace Icm.Api.ConsoleApp
{
    using System.Diagnostics;
    using System.Net;
    using System.Text.Json;
    using Icm.Api.ConsoleApp.Configuration;
    using Icm.Api.ConsoleApp.Output;
    using Icm.Api.Models;
    using Icm.Api.Repositories;
    using Icm.Api.Services;
    using Refit;

    /// <summary>
    /// Direct mode: file a bus pass application <b>without the workflow</b> — one POST of
    /// the service request with its prospect nested — then read the new SR, its prospects,
    /// its activities and its activity plan back beside a workflow-created reference SR's.
    /// The experiment that says whether ICM treats a directly created SR the way it treats
    /// the workflow's.
    /// </summary>
    internal static class DirectBusPass
    {
        private static readonly string[] Children = ["SRProspects", "Activities", "ActivityPlan"];
        private static readonly JsonSerializerOptions RawJsonOptions = new() { WriteIndented = true };

        /// <summary>Runs the scenario. <b>Creates a service request in the target ICM.</b></summary>
        public static async Task<int> RunAsync(
            HttpClient icmClient,
            IOAuthTokenService tokenService,
            OAuthClientCredentials credentials,
            IServiceRequestService serviceRequests,
            ConsoleSettings settings)
        {
            DirectSettings wanted = settings.Direct;
            ServiceRequestInput input = wanted.ToInput(settings.Prospect.Create);

            ServiceRequestPrinter.WriteWarning(
                $"Creating a {input.Type} / {input.SRSubType} service request with {input.Prospects?.Count ?? 0} nested prospect(s) "
                + "- this writes to the target ICM.");
            Console.WriteLine();

            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                string? key;
                if (wanted.RawBody)
                {
                    key = await CreateRawAsync(icmClient, tokenService, credentials, settings, input, stopwatch);
                    if (key is null)
                    {
                        return 2;
                    }
                }
                else
                {
                    ServiceRequest created = await serviceRequests.CreateAsync(input);
                    stopwatch.Stop();
                    Console.WriteLine($"Created in {stopwatch.ElapsedMilliseconds} ms: row id {created.Id ?? "(none)"}, SR number {created.ServiceRequestNumber ?? "(none)"}.");
                    if (created.AdditionalFields.Count > 0)
                    {
                        Console.WriteLine($"       (write answered with {created.AdditionalFields.Count} field(s) outside the read vocabulary: {string.Join(", ", created.AdditionalFields.Keys)})");
                    }

                    Console.WriteLine();
                    key = created.Id;
                }

                if (key is not { Length: > 0 })
                {
                    ServiceRequestPrinter.WriteWarning("ICM returned no row id; nothing to read back.");
                    return 2;
                }

                if (wanted.Staged)
                {
                    await UpdateAsync(serviceRequests, key, "SR Sub Type", new ServiceRequestInput { SRSubType = wanted.ServiceRequest.SubType });
                    await UpdateAsync(serviceRequests, key, "SR Sub Sub Type", new ServiceRequestInput { SRSubSubType = wanted.ServiceRequest.SubSubType });
                }

                if (wanted.ProspectByUpdate)
                {
                    await UpdateAsync(serviceRequests, key, "nested prospect", DirectSettings.ToProspectUpdate(settings.Prospect.Create));
                }

                // The workflow's SRs get their activity plan ~1-2 s after creation
                // (MEASURED 2026-09-29); give a directly created one the same chance.
                Console.WriteLine($"Waiting {wanted.SettleSeconds} s for ICM's post-create processing...");
                await Task.Delay(TimeSpan.FromSeconds(wanted.SettleSeconds));
                Console.WriteLine();

                ServiceRequestReadOptions readOptions = new() { ViewMode = wanted.ViewMode, ExcludeEmptyFields = true };
                ServiceRequest? readBack = await serviceRequests.GetAsync(key, readOptions);
                if (readBack is null)
                {
                    ServiceRequestPrinter.WriteWarning(
                        $"The new service request {key} could not be read back (404): the create answered, but the row is not visible "
                        + "under this ViewMode.");
                }
                else
                {
                    ServiceRequestPrinter.Write(new ServiceRequestPage { Items = [readBack] }, full: true);
                }

                Console.WriteLine();
                foreach (string child in Children)
                {
                    await WriteChildAsync(icmClient, tokenService, credentials, settings, "NEW", key, child, wanted.ViewMode);
                }

                if (wanted.ReferenceServiceRequestId is { Length: > 0 } reference)
                {
                    Console.WriteLine(new string('=', 60));
                    Console.WriteLine($"Reference (workflow-created) service request {reference}");
                    Console.WriteLine(new string('=', 60));
                    Console.WriteLine();
                    foreach (string child in Children)
                    {
                        await WriteChildAsync(icmClient, tokenService, credentials, settings, "REF", reference, child, wanted.ViewMode);
                    }
                }

                return 0;
            }
            catch (ArgumentException exception)
            {
                stopwatch.Stop();
                Console.Error.WriteLine($"The direct create is not usable: {exception.Message}");
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
            catch (HttpRequestException exception)
            {
                stopwatch.Stop();
                return Program.Fail(stopwatch, "Could not reach ICM for a child read.", exception.Message);
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
            return "ICM DIRECT bus pass SR + prospect CREATE (no workflow) + activity read-back";
        }

        /// <summary>Writes the mode's lines of the run header.</summary>
        /// <param name="settings">The settings.</param>
        public static void WriteHeader(ConsoleSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            DirectServiceRequestSettings sr = settings.Direct.ServiceRequest;
            Console.WriteLine($"  SR           {sr.Type} / {sr.SubType} / {sr.SubSubType}, {sr.Status}, {sr.CommMethod}, {sr.ServiceOffice}");
            Console.WriteLine($"  Prospect     {(settings.Direct.IncludeProspect ? $"{settings.Prospect.Create.FirstName} {settings.Prospect.Create.LastName}, via {(settings.Direct.Staged ? "staged updates" : settings.Direct.ProspectVia)}" : "none")}");
            Console.WriteLine($"  Staged       {(settings.Direct.Staged ? "yes: type on create, then sub type, sub sub type, prospect by PUT" : "no")}");
            Console.WriteLine($"  Reference    {settings.Direct.ReferenceServiceRequestId ?? "(none)"}");
            Console.WriteLine($"  ViewMode     {settings.Direct.ViewMode ?? "(ICM default)"}");
        }

        /// <summary>
        /// The raw-body create: the same fields the library would send, in the document's
        /// names, with the prospect rows under <see cref="DirectSettings.ChildName"/>.
        /// Returns the created row's id, or null after printing the failure.
        /// </summary>
        private static async Task<string?> CreateRawAsync(
            HttpClient icmClient,
            IOAuthTokenService tokenService,
            OAuthClientCredentials credentials,
            ConsoleSettings settings,
            ServiceRequestInput input,
            Stopwatch stopwatch)
        {
            DirectSettings wanted = settings.Direct;
            bool live = wanted.LiveVocabulary;
            Dictionary<string, object?> body = new()
            {
                ["Id"] = input.Id,
                ["Comm Method"] = input.CommMethod,
                [live ? "Are Any Of The Family Members Indigenous" : "ICMCPU Aborginal"] = input.AreAnyOfTheFamilyMembersIndigenous,
                ["Integration Id"] = input.IntegrationId,
                ["Memo"] = input.Memo,
                ["Primary Contact Id"] = input.PrimaryContactId,
                ["Primary Organization Id"] = input.PrimaryOrganizationId,
                ["Priority"] = input.Priority,
                [live ? "Service Request Number" : "SR Number"] = input.ServiceRequestNumber,
                [live ? "Type" : "SR Type"] = input.Type,
                ["SR Sub Type"] = input.SRSubType,
                ["SR Sub Sub Type"] = input.SRSubSubType,
                ["Service Office"] = input.ServiceOffice,
                ["Status"] = input.Status,
            };

            if (input.Prospects is { Count: > 0 } || wanted.EmptyChildList)
            {
                IList<ProspectInput> rows = input.Prospects ?? [];
                List<Dictionary<string, object?>> children = rows.Select(row => new Dictionary<string, object?>
                {
                    ["Id"] = wanted.ChildId,
                    ["Service Request Id"] = wanted.ChildServiceRequestId,
                    ["Prospective Contact UId"] = row.ProspectiveContactUId,
                    ["First Name"] = row.FirstName,
                    ["Last Name"] = row.LastName,
                    ["Birth Date"] = row.BirthDate?.ToString("MM/dd/yyyy", System.Globalization.CultureInfo.InvariantCulture),
                    ["Applicant Flag"] = row.IsApplicant is { } flag ? (flag ? "Y" : "N") : null,
                    ["Purpose"] = row.Purpose,
                    ["Street Address"] = row.StreetAddress,
                    ["City"] = row.City,
                    ["State"] = row.Province,
                    ["Postal Code"] = row.PostalCode,
                    ["Home Phone #"] = row.HomePhone,
                    ["Preferred Communication Method"] = row.PreferredCommunicationMethod,
                    ["Social Security Number"] = row.Sin,
                    ["Source"] = row.Source,
                    ["Source Type"] = row.SourceType,
                }.Where(pair => pair.Value is not (null or "")).ToDictionary(pair => pair.Key, pair => pair.Value)).ToList();

                // "ListOfSRProspects/SRProspects" nests Siebel-style — an object named for
                // the list holding the array named for the component, the shape the bus
                // pass workflow's own envelope uses; a bare name is a plain array.
                // An empty array still reads as one (empty) prospect instance to Siebel
                // (MEASURED 2026-09-29: 'Prospects' has no user key), so an empty list is
                // sent as an empty container instead.
                string[] parts = wanted.ChildName.Split('/', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                body[parts[0]] = parts.Length == 2
                    ? (children.Count == 0 ? new Dictionary<string, object?>() : new Dictionary<string, object?> { [parts[1]] = children })
                    : children;
            }

            // Attaching to an existing SR: only the child list travels, by PUT on the key.
            bool attach = wanted.AttachTo is { Length: > 0 };
            if (attach)
            {
                body = body.Where(pair => pair.Key.StartsWith("ListOf", StringComparison.Ordinal) || pair.Key == wanted.ChildName)
                    .ToDictionary(pair => pair.Key, pair => pair.Value);
            }

            // Blank as well as null stays off the body: a "" would clear, or fail to
            // validate, a field the run meant to leave alone.
            string json = JsonSerializer.Serialize(
                body.Where(pair => pair.Value is not (null or "")).ToDictionary(pair => pair.Key, pair => pair.Value),
                RawJsonOptions);
            string path = attach
                ? $"{settings.Icm.BaseUrl!.TrimEnd('/')}/data/ServiceRequest/ServiceRequest/{Uri.EscapeDataString(wanted.AttachTo!)}/"
                : $"{settings.Icm.BaseUrl!.TrimEnd('/')}/data/ServiceRequest/ServiceRequest/";
            HttpMethod method = attach ? HttpMethod.Put : HttpMethod.Post;

            Console.WriteLine($"RAW {method} {path} (child under \"{wanted.ChildName}\", {wanted.Vocabulary} vocabulary)");
            Console.WriteLine(json);
            Console.WriteLine();

            (HttpStatusCode status, string response) = await RawIcmReader.SendAsync(icmClient, tokenService, credentials, settings, method, path, json);
            stopwatch.Stop();
            Console.WriteLine($"{(int)status} {status} in {stopwatch.ElapsedMilliseconds} ms");
            Console.WriteLine(Program.PrettyJson(response));
            Console.WriteLine();
            if ((int)status is < 200 or > 299)
            {
                return null;
            }

            if (attach)
            {
                return wanted.AttachTo;
            }

            using JsonDocument document = JsonDocument.Parse(response);
            JsonElement items = document.RootElement.TryGetProperty("items", out JsonElement found) ? found : document.RootElement;
            if (items.ValueKind == JsonValueKind.Array)
            {
                items = items.EnumerateArray().FirstOrDefault();
            }

            return Field(items, "Id") is { Length: > 0 } id && id != "-" ? id : null;
        }

        private static async Task UpdateAsync(IServiceRequestService serviceRequests, string key, string what, ServiceRequestInput input)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            ServiceRequest? updated = await serviceRequests.UpdateAsync(key, input);
            Console.WriteLine(updated is null
                ? $"Update ({what}) answered \"nothing changed\" in {stopwatch.ElapsedMilliseconds} ms."
                : $"Updated ({what}) in {stopwatch.ElapsedMilliseconds} ms.");
            Console.WriteLine();
        }

        private static async Task WriteChildAsync(
            HttpClient icmClient,
            IOAuthTokenService tokenService,
            OAuthClientCredentials credentials,
            ConsoleSettings settings,
            string label,
            string key,
            string child,
            string? viewMode)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            (HttpStatusCode status, string body) = await RawIcmReader.ReadChildAsync(
                icmClient, tokenService, credentials, settings, key, child, viewMode, excludeEmptyFields: true);
            stopwatch.Stop();

            Console.WriteLine($"[{label}] {key}/{child}: {(int)status} {status} in {stopwatch.ElapsedMilliseconds} ms");
            if ((int)status is < 200 or > 299)
            {
                Console.WriteLine($"       {body.Trim()}");
                Console.WriteLine();
                return;
            }

            using JsonDocument document = JsonDocument.Parse(body);
            if (!document.RootElement.TryGetProperty("items", out JsonElement items) || items.ValueKind != JsonValueKind.Array)
            {
                Console.WriteLine(Program.PrettyJson(body));
                Console.WriteLine();
                return;
            }

            foreach (JsonElement item in items.EnumerateArray())
            {
                Console.WriteLine(child switch
                {
                    "Activities" => $"       {Field(item, "Sequence"),-12} {Field(item, "Type"),-12} {Field(item, "Action By"),-7} "
                        + $"{Field(item, "Status"),-10} {Field(item, "Created"),-20} {Field(item, "Plan Name"),-28} "
                        + $"{Field(item, "Action Code")}  result={Field(item, "Result Code")}  owner={Field(item, "Primary Owner Id")}  due={Field(item, "Due")}",
                    "ActivityPlan" => $"       plan {Field(item, "Id")}  {Field(item, "Template")}  {Field(item, "Status")}  template {Field(item, "Template Id")}",
                    _ => $"       prospect {Field(item, "Id")}  {Field(item, "First Name")} {Field(item, "Last Name")}  purpose={Field(item, "Purpose")}  "
                        + $"created {Field(item, "Created")} by {Field(item, "Created By Name")}  applicant={Field(item, "Applicant Flag")}",
                });
            }

            Console.WriteLine();
        }

        private static string Field(JsonElement item, string name) =>
            item.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? "-"
                : "-";
    }
}
