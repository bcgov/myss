namespace Icm.Api.Models
{
    /// <summary>How the name criteria of a <see cref="ContactQuery"/> are compared.</summary>
    /// <remarks>
    /// All three ignore case, because a name is typed by a person and ICM's plain
    /// comparison does not: MEASURED against SIT1 on 2026-09-17, <c>=</c> and <c>LIKE</c>
    /// miss <c>smith</c> against <c>Smith</c>, while Siebel's <c>~=</c> and <c>~LIKE</c>
    /// match it.
    /// </remarks>
    public enum ContactNameMatch
    {
        /// <summary>The whole name, ignoring case.</summary>
        Exact,

        /// <summary>Names that begin with the text, ignoring case.</summary>
        StartsWith,

        /// <summary>Names that contain the text anywhere, ignoring case.</summary>
        Contains,
    }
}
