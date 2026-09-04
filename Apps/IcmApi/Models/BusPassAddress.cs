namespace Icm.Api.Models
{
    /// <summary>
    /// A civic address as the bus pass workflow carries one.
    /// </summary>
    public class BusPassAddress
    {
        /// <summary>Gets or sets the unit or suite number.</summary>
        public string? Unit { get; set; }

        /// <summary>Gets or sets the first street address line.</summary>
        public string? Line1 { get; set; }

        /// <summary>Gets or sets the second street address line.</summary>
        public string? Line2 { get; set; }

        /// <summary>Gets or sets the city.</summary>
        public string? City { get; set; }

        /// <summary>
        /// Gets or sets the province. Sent as given — the old form normalized
        /// <c>British Columbia</c> to <c>BC</c> and required BC, and that rule belongs to
        /// the caller's validation, not to this client.
        /// </summary>
        public string? Province { get; set; }

        /// <summary>Gets or sets the postal code.</summary>
        public string? PostalCode { get; set; }
    }
}
