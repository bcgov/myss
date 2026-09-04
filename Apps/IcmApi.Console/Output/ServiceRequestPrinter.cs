namespace Icm.Api.ConsoleApp.Output
{
    using System.Globalization;
    using System.Reflection;
    using System.Text.Json;
    using Icm.Api.Models;

    /// <summary>Writes a page of service requests to the console.</summary>
    /// <remarks>
    /// Reflects over <see cref="ServiceRequest"/> rather than listing its properties, so a
    /// field added to the model shows up here without anyone remembering to add it. Only
    /// non-null values are printed: a record ICM returns in full is fifty-odd fields, most
    /// of them empty, and printing those buries the ones that matter.
    /// </remarks>
    public static class ServiceRequestPrinter
    {
        private static readonly PropertyInfo[] Fields = [.. typeof(ServiceRequest)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.Name is not (nameof(ServiceRequest.Links)
                or nameof(ServiceRequest.UnparsedValues)
                or nameof(ServiceRequest.AdditionalFields)))];

        /// <summary>Writes the page.</summary>
        /// <param name="page">The page ICM returned.</param>
        /// <param name="full">True to print every non-null field; false for one line each.</param>
        public static void Write(ServiceRequestPage page, bool full)
        {
            ArgumentNullException.ThrowIfNull(page);

            if (page.Items.Count == 0)
            {
                Console.WriteLine("No records matched.");
                Console.WriteLine();
                Console.WriteLine(
                    "ICM answers an empty search with 204 No Content, which is not an error. If a "
                    + "search you expected to match came back empty, the likeliest cause is "
                    + "Query:ViewMode - ICM defaults to 'Sales Rep', which only returns records the "
                    + "authenticated client owns.");
                return;
            }

            Console.WriteLine($"{page.Items.Count} record(s):");
            Console.WriteLine();

            for (int i = 0; i < page.Items.Count; i++)
            {
                ServiceRequest record = page.Items[i];

                if (full)
                {
                    WriteFull(i + 1, record);
                }
                else
                {
                    Console.WriteLine(
                        $"  {i + 1,3}. {record.ServiceRequestNumber ?? "(no SR number)"}"
                        + $"  {record.Status ?? "-"}  {record.Type ?? "-"}"
                        + $"  created {Format(record.CreatedDate)}");
                }
            }

            if (page.Links.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("Links:");
                foreach (ServiceRequestLink link in page.Links)
                {
                    Console.WriteLine($"  {link.Rel ?? "-"}: {link.Href}");
                }
            }
        }

        private static void WriteFull(int number, ServiceRequest record)
        {
            Console.WriteLine($"  [{number}] {record.ServiceRequestNumber ?? record.Id ?? "(unidentified)"}");

            foreach (PropertyInfo field in Fields)
            {
                object? value = field.GetValue(record);
                if (value is null)
                {
                    continue;
                }

                Console.WriteLine($"       {field.Name,-36} {Format(value)}");
            }

            if (record.Links.Count > 0)
            {
                Console.WriteLine($"       {"Links",-36} {record.Links.Count}");
            }

            // Loud on purpose: a field here is one the client is not modelling, which is
            // exactly the kind of gap that is otherwise invisible.
            if (record.AdditionalFields.Count > 0)
            {
                Console.WriteLine();
                WriteWarning($"       {record.AdditionalFields.Count} field(s) not modelled by this client:");
                foreach ((string key, JsonElement value) in record.AdditionalFields)
                {
                    WriteWarning($"         {key,-34} {value.GetRawText()}");
                }

                WriteWarning(
                    "       These arrived as raw JSON rather than being dropped. Add them to "
                    + "SiebelServiceRequest and ServiceRequest if they are wanted.");
            }

            // Loud on purpose. A value here means a date arrived in a shape SiebelDate does
            // not recognise.
            if (record.UnparsedValues.Count > 0)
            {
                Console.WriteLine();
                WriteWarning($"       {record.UnparsedValues.Count} value(s) could not be parsed:");
                foreach ((string key, string raw) in record.UnparsedValues)
                {
                    WriteWarning($"         {key,-34} {raw}");
                }

                WriteWarning(
                    "       Siebel documents ISO 8601 dates. If these are not ISO, note the exact "
                    + "shape and add it to SiebelDate - do not guess the month/day order.");
            }

            Console.WriteLine();
        }

        /// <summary>Formats a value the way its type deserves, invariantly.</summary>
        private static string Format(object? value) => value switch
        {
            null => "-",
            bool flag => flag ? "true" : "false",
            DateTimeOffset instant => instant.ToString("O", CultureInfo.InvariantCulture),
            JsonElement json => json.GetRawText(),
            DateTime local => local.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)
                + $" ({local.Kind})",
            DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            _ => value.ToString() ?? "-",
        };

        private static void WriteWarning(string message)
        {
            ConsoleColor previous = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(message);
            Console.ForegroundColor = previous;
        }
    }
}
