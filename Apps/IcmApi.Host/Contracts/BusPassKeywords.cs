namespace Icm.Api.Host.Contracts
{
    /// <summary>
    /// Stable dotted keywords for failures this middleware reports. The keyword
    /// is the contract: MyssApi logs it and matches on it; it is never reused for
    /// a different meaning.
    /// </summary>
    public static class BusPassKeywords
    {
        /// <summary>The host has no ICM credentials configured.</summary>
        public const string NotConfigured = "ICM.BUSPASS.NOT_CONFIGURED";

        /// <summary>No access token could be obtained from ICM's authorization server.</summary>
        public const string TokenUnavailable = "ICM.BUSPASS.TOKEN_UNAVAILABLE";

        /// <summary>ICM could not be reached.</summary>
        public const string Unreachable = "ICM.BUSPASS.UNREACHABLE";

        /// <summary>ICM did not answer in time.</summary>
        public const string Timeout = "ICM.BUSPASS.TIMEOUT";

        /// <summary>ICM answered with a failure status.</summary>
        public const string UpstreamError = "ICM.BUSPASS.UPSTREAM_ERROR";

        /// <summary>ICM reported success but its answer could not be read.</summary>
        public const string UnusableResponse = "ICM.BUSPASS.UNUSABLE_RESPONSE";
    }
}
