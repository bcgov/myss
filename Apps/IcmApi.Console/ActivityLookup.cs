namespace Icm.Api.ConsoleApp
{
    using System.Diagnostics;
    using System.Globalization;
    using System.Reflection;
    using Icm.Api.ConsoleApp.Configuration;
    using Icm.Api.ConsoleApp.Output;
    using Icm.Api.Models;
    using Icm.Api.Repositories;
    using Icm.Api.Services;
    using Refit;
    using Activity = Icm.Api.Models.Activity;

    /// <summary>Activity mode, as its own class so <see cref="Program"/> stays readable.</summary>
    internal static class ActivityLookup
    {
        private static readonly PropertyInfo[] ActivityFields = [.. typeof(Activity)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.Name is not (nameof(Activity.UnparsedValues) or nameof(Activity.AdditionalFields)))];

        private static readonly PropertyInfo[] PlanFields = [.. typeof(ActivityPlan)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.Name is not (nameof(ActivityPlan.UnparsedValues) or nameof(ActivityPlan.AdditionalFields)))];

        /// <summary>
        /// Activity mode: the plans and activities on a service request (the events on it),
        /// one activity by key, or a search — or, with <c>Activity:Create:Enabled</c>,
        /// <b>create one</b> on a service request and read the request's activities back.
        /// </summary>
        public static async Task<int> RunAsync(
            HttpClient icmClient,
            IOAuthTokenService tokenService,
            OAuthClientCredentials credentials,
            ConsoleSettings settings)
        {
            IActivityService activities = new ActivityService(
                new ActivityRepository(icmClient, settings.Icm.TrustedUserName), tokenService, credentials);
            ActivitySettings wanted = settings.Activity;

            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                if (wanted.Create.Enabled)
                {
                    ActivityInput input = wanted.Create.ToInput();
                    ServiceRequestPrinter.WriteWarning(
                        $"Creating a {input.Type} activity \"{input.ActionCode}\" on service request {input.ServiceRequestId} (PUT Activities/Activities, Id NULL)"
                        + " - this writes a row to the target ICM.");
                    Console.WriteLine();

                    Activity created = await activities.CreateAsync(input);
                    Console.WriteLine($"Created in {stopwatch.ElapsedMilliseconds} ms. ICM returned:");
                    Console.WriteLine();
                    WriteActivities([created], wanted.ShowValues);
                    Console.WriteLine();
                    Console.WriteLine($"Waiting {wanted.SettleSeconds} s, then reading the service request's plans and activities back...");
                    await Task.Delay(TimeSpan.FromSeconds(wanted.SettleSeconds));
                    Console.WriteLine();
                    await WriteServiceRequestAsync(activities, input.ServiceRequestId!, wanted);
                    return 0;
                }

                if (wanted.ServiceRequestId is not null)
                {
                    Console.WriteLine($"Reading the plans and activities on service request {wanted.ServiceRequestId}...");
                    Console.WriteLine();
                    await WriteServiceRequestAsync(activities, wanted.ServiceRequestId, wanted);
                    return 0;
                }

                IReadOnlyList<Activity> found;
                if (wanted.Id is not null)
                {
                    Console.WriteLine($"Reading activity {wanted.Id}...");
                    Activity? row = await activities.GetAsync(wanted.Id, wanted.ToReadOptions());
                    found = row is null ? [] : [row];
                    Console.WriteLine(row is null ? $"Not found ({stopwatch.ElapsedMilliseconds} ms)." : $"Found in {stopwatch.ElapsedMilliseconds} ms.");
                }
                else
                {
                    Console.WriteLine($"Searching activities on {string.Join(", ", wanted.CriteriaSet)}...");
                    ActivityPage page = await activities.SearchAsync(wanted.ToQuery());
                    found = page.Items;
                    Console.WriteLine(
                        $"{page.Items.Count} row(s) on this page, {page.TotalCount?.ToString(CultureInfo.InvariantCulture) ?? "unknown"} in total, "
                        + $"in {stopwatch.ElapsedMilliseconds} ms.");
                }

                Console.WriteLine();
                WriteActivities(found, wanted.ShowValues);
                return 0;
            }
            catch (ArgumentException exception)
            {
                stopwatch.Stop();
                Console.Error.WriteLine($"The activity lookup is not usable: {exception.Message}");
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

        /// <summary>The run header's title for this mode.</summary>
        /// <param name="settings">The settings.</param>
        /// <returns>The title.</returns>
        public static string Title(ConsoleSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            return settings.Activity.Create.Enabled ? "ICM activity CREATE + read-back" : "ICM activity lookup";
        }

        /// <summary>Writes the mode's lines of the run header.</summary>
        /// <param name="settings">The settings.</param>
        public static void WriteHeader(ConsoleSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ActivitySettings wanted = settings.Activity;
            Console.WriteLine(wanted.Create.Enabled
                ? $"  Create       {wanted.Create.Type} / {wanted.Create.ActionBy} \"{wanted.Create.ActionCode}\" on SR {wanted.Create.ServiceRequestId}"
                : $"  Criteria     {string.Join(", ", wanted.CriteriaSet)}");
            Console.WriteLine($"  ViewMode     {wanted.ViewMode ?? "(library default: Organization)"}");
            Console.WriteLine($"  Values       {(wanted.ShowValues ? "shown" : "hidden (--Activity:ShowValues=true to print them)")}");
        }

        private static async Task WriteServiceRequestAsync(IActivityService activities, string serviceRequestKey, ActivitySettings wanted)
        {
            Stopwatch watch = Stopwatch.StartNew();
            IReadOnlyList<ActivityPlan> plans = await activities.GetPlansForServiceRequestAsync(serviceRequestKey, wanted.ToReadOptions());
            Console.WriteLine($"{plans.Count} activity plan(s) in {watch.ElapsedMilliseconds} ms:");
            for (int i = 0; i < plans.Count; i++)
            {
                Console.WriteLine($"  [{i + 1}] Plan {plans[i].Id}  {plans[i].Template}  {plans[i].Status}  template {plans[i].TemplateId}");
                if (wanted.ShowValues)
                {
                    CasePrinter.WriteFields(plans[i], PlanFields, plans[i].AdditionalFields, plans[i].UnparsedValues, showValues: true, "ActivityPlan");
                }
            }

            Console.WriteLine();
            watch.Restart();
            IReadOnlyList<Activity> rows = await activities.GetForServiceRequestAsync(serviceRequestKey, wanted.ToReadOptions());
            Console.WriteLine($"{rows.Count} activit{(rows.Count == 1 ? "y" : "ies")} in {watch.ElapsedMilliseconds} ms:");
            WriteActivities([.. rows.OrderBy(row => row.Sequence, StringComparer.Ordinal).ThenBy(row => row.Created)], wanted.ShowValues);
        }

        private static void WriteActivities(IReadOnlyList<Activity> rows, bool showValues)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                Activity row = rows[i];
                Console.WriteLine(
                    $"  [{i + 1}] {row.Sequence ?? "-",-12} {row.Type ?? "-",-12} {row.ActionBy ?? "-",-7} {row.Status ?? "-",-10} "
                    + $"{row.PlanName ?? "(no plan)",-28} {row.ActionCode ?? "-"}  result={row.ResultCode ?? "-"}  id {row.Id}");
                if (showValues)
                {
                    CasePrinter.WriteFields(row, ActivityFields, row.AdditionalFields, row.UnparsedValues, showValues, "Activity");
                    Console.WriteLine();
                }
            }
        }
    }
}
