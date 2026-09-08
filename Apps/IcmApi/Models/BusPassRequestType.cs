namespace Icm.Api.Models
{
    /// <summary>
    /// What the applicant is asking for.
    /// </summary>
    /// <remarks>
    /// The old MCP form expressed this as two radio groups — existing client vs new
    /// applicant, then address update vs replacement — flattened server-side into flags.
    /// Semantically there were only ever three requests, and the workflow's own
    /// <c>ICMBusPassRequestType</c> field is a single value, so this model says it once.
    /// </remarks>
    public enum BusPassRequestType
    {
        /// <summary>A new applicant applying for a bus pass.</summary>
        NewApplication,

        /// <summary>An existing client reporting a change of address.</summary>
        AddressUpdate,

        /// <summary>An existing client requesting a replacement pass.</summary>
        Replacement,
    }
}
