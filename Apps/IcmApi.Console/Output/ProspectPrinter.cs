namespace Icm.Api.ConsoleApp.Output
{
    using System.Reflection;
    using Icm.Api.Models;

    /// <summary>Writes prospect rows to the console, the way <see cref="CasePrinter"/> writes cases.</summary>
    internal static class ProspectPrinter
    {
        private static readonly PropertyInfo[] ProspectFields = [.. typeof(Prospect)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.Name is not (nameof(Prospect.UnparsedValues) or nameof(Prospect.AdditionalFields)))];

        /// <summary>Writes prospect rows.</summary>
        /// <param name="rows">The rows.</param>
        /// <param name="showValues">True to print values; false to print only whether each is set.</param>
        public static void WriteProspects(IReadOnlyList<Prospect> rows, bool showValues)
        {
            ArgumentNullException.ThrowIfNull(rows);

            for (int i = 0; i < rows.Count; i++)
            {
                Prospect row = rows[i];
                Console.WriteLine(
                    $"  [{i + 1}] Prospect {row.Id ?? "(no row id)"}  on service request {row.ServiceRequestId ?? "(none)"}"
                    + $"{(row.Purpose is { Length: > 0 } ? $"  ({row.Purpose})" : string.Empty)}");
                CasePrinter.WriteFields(row, ProspectFields, row.AdditionalFields, row.UnparsedValues, showValues, "Prospect");
                Console.WriteLine();
            }
        }
    }
}
