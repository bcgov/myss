namespace Icm.Api.Host.Contracts
{
    /// <summary>
    /// A postal address as the applicant stated it.
    /// </summary>
    public class BusPassAddressRequest
    {
        /// <summary>Gets or sets the unit or apartment number.</summary>
        public string? Unit { get; set; }

        /// <summary>Gets or sets the first street address line.</summary>
        public string? Line1 { get; set; }

        /// <summary>Gets or sets the second street address line.</summary>
        public string? Line2 { get; set; }

        /// <summary>Gets or sets the city.</summary>
        public string? City { get; set; }

        /// <summary>Gets or sets the province, normally "BC".</summary>
        public string? Province { get; set; }

        /// <summary>Gets or sets the postal code.</summary>
        public string? PostalCode { get; set; }
    }
}
