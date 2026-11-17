namespace Myss.AdminApi.Support
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// What a worker searches for clients by (MYSS-275). Any one, or several:
    /// several narrow the search, each matching as <see cref="IClientSearchService"/>
    /// describes. Blank fields are ignored.
    /// </summary>
    public sealed record ClientSearchCriteria
    {
        /// <summary>Gets part of the client's first name.</summary>
        public string? FirstName { get; init; }

        /// <summary>Gets part of the client's last name.</summary>
        public string? LastName { get; init; }

        /// <summary>Gets the client's full nine-digit SIN; spaces and dashes are ignored.</summary>
        public string? Sin { get; init; }

        /// <summary>Gets part of the client's BCeID user name.</summary>
        public string? BceidUsername { get; init; }
    }

    /// <summary>How a client signs in to MySS, as far as the profile shows it.</summary>
    public static class ClientSignInTypes
    {
        /// <summary>BCeID.</summary>
        public const string Bceid = "BCEID";

        /// <summary>BC Services Card.</summary>
        public const string BcServicesCard = "BC_SERVICES_CARD";

        /// <summary>A worker override account (MYSS-321): nobody signs in to it.</summary>
        public const string WorkerOverride = "WORKER_OVERRIDE";
    }

    /// <summary>One client a search found.</summary>
    public sealed class ClientSummaryModel
    {
        /// <summary>Gets the profile identifier.</summary>
        public Guid Id { get; init; }

        /// <summary>Gets the first name.</summary>
        public required string FirstName { get; init; }

        /// <summary>Gets the last name.</summary>
        public required string LastName { get; init; }

        /// <summary>Gets the BCeID user name; null when the client does not sign in with BCeID or has not since it was recorded.</summary>
        public string? BceidUsername { get; init; }

        /// <summary>Gets the last three digits of the SIN, the only part a result shows.</summary>
        public string? SinLastThree { get; init; }

        /// <summary>Gets the date of birth.</summary>
        public DateOnly DateOfBirth { get; init; }

        /// <summary>Gets one of <see cref="ClientSignInTypes"/>, or null when the profile does not show it.</summary>
        public string? SignInType { get; init; }
    }

    /// <summary>A search's answer: the matches, at most <see cref="ClientSearchService.MaxResults"/>.</summary>
    public sealed class ClientSearchResultModel
    {
        /// <summary>Gets the matching clients, by last name then first name.</summary>
        public required IReadOnlyList<ClientSummaryModel> Clients { get; init; }

        /// <summary>Gets a value indicating whether more clients matched than were returned.</summary>
        public bool HasMore { get; init; }
    }

    /// <summary>A search, or why it was refused.</summary>
    /// <param name="Result">The matches, when the criteria were usable.</param>
    /// <param name="RefusalKeyword">One of <see cref="ClientSearchKeywords"/>, when they were not.</param>
    public sealed record ClientSearchOutcome(ClientSearchResultModel? Result, string? RefusalKeyword);

    /// <summary>The keywords a refused search answers with.</summary>
    public static class ClientSearchKeywords
    {
        /// <summary>Every field was blank.</summary>
        public const string CriteriaRequired = "SUPPORT.CLIENT_SEARCH.CRITERIA_REQUIRED";

        /// <summary>The SIN was not nine digits.</summary>
        public const string SinInvalid = "SUPPORT.CLIENT_SEARCH.SIN_INVALID";
    }
}
