namespace Icm.Api.Host.Contracts
{
    using System;
    using Icm.Api.Models;

    /// <summary>
    /// What ICM's workflow said about a submission. A populated
    /// <see cref="ErrorCode"/> is a business rejection carried on an HTTP 200;
    /// callers must check it, because ICM still files a service request for it
    /// and still returns that number.
    /// </summary>
    /// <remarks>
    /// The workflow also echoes the matched first and last name. They are not in
    /// this response on purpose: returned to whoever typed a SIN, they would let
    /// anyone confirm another person's name from their SIN.
    /// </remarks>
    public class BusPassApplicationResponse
    {
        /// <summary>Gets or sets the application number ICM assigned, if any.</summary>
        public string? ApplicationNumber { get; set; }

        /// <summary>Gets or sets ICM's error code when the request was not accepted.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Gets or sets ICM's error message when the request was not accepted.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>Gets or sets the status word the workflow reported.</summary>
        public string? Status { get; set; }

        /// <summary>
        /// Builds the response from the workflow's result.
        /// </summary>
        /// <param name="result">The result.</param>
        /// <returns>The response, with the echoed name left out.</returns>
        public static BusPassApplicationResponse From(BusPassResult result)
        {
            ArgumentNullException.ThrowIfNull(result);

            return new BusPassApplicationResponse
            {
                ApplicationNumber = result.ApplicationNumber,
                ErrorCode = string.IsNullOrWhiteSpace(result.ErrorCode) ? null : result.ErrorCode,
                ErrorMessage = result.ErrorMessage,
                Status = result.Status,
            };
        }
    }
}
