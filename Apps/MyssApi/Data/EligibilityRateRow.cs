namespace Myss.Api.Data
{
    using System;

    /// <summary>
    /// One row of the eligibility rate table: the monthly income limit for a given
    /// family size, by client type (BR-D9-05).
    /// </summary>
    public class EligibilityRateRow
    {
        /// <summary>Gets or sets the surrogate key.</summary>
        public int Id { get; set; }

        /// <summary>Gets or sets the family unit size, 1–7 (7 is the cap).</summary>
        public int FamilySize { get; set; }

        /// <summary>Gets or sets the limit for client type A.</summary>
        public decimal TypeA { get; set; }

        /// <summary>Gets or sets the limit for client type B.</summary>
        public decimal TypeB { get; set; }

        /// <summary>Gets or sets the limit for client type C.</summary>
        public decimal TypeC { get; set; }

        /// <summary>Gets or sets the limit for client type D.</summary>
        public decimal TypeD { get; set; }

        /// <summary>Gets or sets the limit for client type E.</summary>
        public decimal TypeE { get; set; }

        /// <summary>Gets or sets the date this row takes effect.</summary>
        public DateOnly EffectiveFrom { get; set; }

        /// <summary>Gets or sets provenance notes (e.g. the FDD rule reference).</summary>
        public string? Notes { get; set; }

        /// <summary>Returns the limit for the given client type code.</summary>
        /// <param name="clientType">Client type code "A" through "E".</param>
        /// <returns>The monthly income limit.</returns>
        public decimal AmountFor(string clientType) => clientType switch
        {
            "A" => this.TypeA,
            "B" => this.TypeB,
            "C" => this.TypeC,
            "D" => this.TypeD,
            "E" => this.TypeE,
            _ => throw new ArgumentOutOfRangeException(
                nameof(clientType), clientType, "Client type must be A, B, C, D or E."),
        };
    }
}
