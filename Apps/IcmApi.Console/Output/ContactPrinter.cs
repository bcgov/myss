namespace Icm.Api.ConsoleApp.Output
{
    using System.Globalization;
    using Icm.Api.Models;

    /// <summary>Writes a contact from a search to the console.</summary>
    /// <remarks>
    /// Unlike a service request, every field of a contact is personal information, and
    /// there is no non-personal subset to default to. So the default is presence, not
    /// content: which fields came back filled is enough to tell whether the search and the
    /// mapping worked, and keeps a person's name, birth date and phone numbers out of
    /// terminal scrollback unless someone asks for them.
    /// </remarks>
    internal static class ContactPrinter
    {
        /// <summary>Writes one contact.</summary>
        /// <param name="contact">The contact.</param>
        /// <param name="showValues">True to print values; false to print only whether each is set.</param>
        public static void Write(Contact contact, bool showValues)
        {
            // The row id is a key, not personal information, and is what the next ICM
            // call needs — printed either way.
            Console.WriteLine($"  Id                       {contact.Id ?? "(empty)"}");

            (string Name, object? Value)[] fields =
            [
                ("PersonId", contact.PersonId),
                ("IntegrationId", contact.IntegrationId),
                ("BcServicesCardDid", contact.BcServicesCardDid),
                ("Sin", contact.Sin),
                ("Phn", contact.Phn),
                ("FirstName", contact.FirstName),
                ("MiddleName", contact.MiddleName),
                ("LastName", contact.LastName),
                ("BirthDate", contact.BirthDate),
                ("Gender", contact.Gender),
                ("Email", contact.Email),
                ("CellPhone", contact.CellPhone),
                ("HomePhone", contact.HomePhone),
                ("WorkPhone", contact.WorkPhone),
                ("MessagePhone", contact.MessagePhone),
                ("IsDeceased", contact.IsDeceased),
                ("IsPotentialDuplicate", contact.IsPotentialDuplicate),
                ("Created", contact.Created),
                ("Updated", contact.Updated),
            ];

            foreach ((string name, object? value) in fields)
            {
                bool empty = value is null || (value is string text && text.Length == 0);
                string shown = empty
                    ? "(empty)"
                    : showValues
                        ? string.Create(CultureInfo.InvariantCulture, $"{value}")
                        : "(set)";
                Console.WriteLine($"  {name,-24} {shown}");
            }

            foreach (string key in contact.AdditionalFields.Keys)
            {
                // The request names its fields, so nothing should land here.
                Console.WriteLine($"  UNMODELLED               {key}");
            }

            foreach ((string key, string value) in contact.UnparsedValues)
            {
                Console.WriteLine($"  UNPARSED                 {key}{(showValues ? $" = {value}" : string.Empty)}");
            }
        }
    }
}
