namespace Icm.Api.ConsoleApp.Output
{
    using System.Reflection;
    using System.Text.Json;
    using Icm.Api.Models;

    /// <summary>Writes a case, and the people on it, to the console.</summary>
    /// <remarks>
    /// Reflects over <see cref="Case"/> and <see cref="CaseContact"/> rather than listing
    /// their properties, so a field added to a model shows up here without anyone
    /// remembering to add it. Only non-null values are printed. The values themselves are
    /// printed by default — SIT holds test data — and <c>--Case:ShowValues=false</c>
    /// reduces each to presence for a run against anything else.
    /// </remarks>
    internal static class CasePrinter
    {
        private static readonly PropertyInfo[] CaseFields = Fields<Case>();
        private static readonly PropertyInfo[] ContactFields = Fields<CaseContact>();

        /// <summary>Writes one case.</summary>
        /// <param name="number">Its position in the result, for the heading.</param>
        /// <param name="record">The case.</param>
        /// <param name="showValues">True to print values; false to print only whether each is set.</param>
        public static void WriteCase(int number, Case record, bool showValues)
        {
            ArgumentNullException.ThrowIfNull(record);

            Console.WriteLine($"  [{number}] Case {record.CaseNumber ?? "(no case number)"}  row id {record.Id ?? "(none)"}");
            WriteFields(record, CaseFields, record.AdditionalFields, record.UnparsedValues, showValues, "Case");
            Console.WriteLine();
        }

        /// <summary>Writes the people on a case.</summary>
        /// <param name="people">The rows of the case's Contact child collection.</param>
        /// <param name="showValues">True to print values; false to print only whether each is set.</param>
        public static void WriteContacts(IReadOnlyList<CaseContact> people, bool showValues)
        {
            ArgumentNullException.ThrowIfNull(people);

            Console.WriteLine($"       {people.Count} contact(s) on the case:");
            Console.WriteLine();
            for (int i = 0; i < people.Count; i++)
            {
                CaseContact person = people[i];
                Console.WriteLine(
                    $"       [{i + 1}] {person.Relationship ?? "(no relationship)"}"
                    + $"{(person.IsPrimary == true ? ", primary" : string.Empty)}  row id {person.Id ?? "(none)"}");
                WriteFields(person, ContactFields, person.AdditionalFields, person.UnparsedValues, showValues, "CaseContact", indent: 6);
                Console.WriteLine();
            }
        }

        private static PropertyInfo[] Fields<T>() => [.. typeof(T)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.Name is not (nameof(Case.UnparsedValues) or nameof(Case.AdditionalFields)))];

        private static void WriteFields(
            object record,
            PropertyInfo[] fields,
            IReadOnlyDictionary<string, JsonElement> additional,
            IReadOnlyDictionary<string, string> unparsed,
            bool showValues,
            string model,
            int indent = 0)
        {
            string pad = new(' ', 7 + indent);
            foreach (PropertyInfo field in fields)
            {
                object? value = field.GetValue(record);
                if (value is null || (value is string text && text.Length == 0))
                {
                    continue;
                }

                // The row id is a key, not personal information, and is what the next
                // ICM call needs — printed either way.
                bool show = showValues || field.Name == nameof(Case.Id);
                Console.WriteLine($"{pad}{field.Name,-28} {(show ? ServiceRequestPrinter.Format(value) : "(set)")}");
            }

            // Loud on purpose: the request names its fields, so a field here is one the
            // client asked for and does not model — or ICM's naming moved.
            if (additional.Count > 0)
            {
                Console.WriteLine();
                ServiceRequestPrinter.WriteWarning($"{pad}{additional.Count} field(s) not modelled by this client:");
                foreach ((string key, JsonElement value) in additional)
                {
                    ServiceRequestPrinter.WriteWarning($"{pad}  {key,-26} {(showValues ? value.GetRawText() : "(set)")}");
                }

                ServiceRequestPrinter.WriteWarning($"{pad}Add them to Siebel{model} and {model} if they are wanted.");
            }

            if (unparsed.Count > 0)
            {
                Console.WriteLine();
                ServiceRequestPrinter.WriteWarning($"{pad}{unparsed.Count} value(s) could not be parsed:");
                foreach ((string key, string raw) in unparsed)
                {
                    ServiceRequestPrinter.WriteWarning($"{pad}  {key,-26} {(showValues ? raw : "(set)")}");
                }
            }
        }
    }
}
