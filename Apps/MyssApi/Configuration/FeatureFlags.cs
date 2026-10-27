namespace Myss.Api.Configuration
{
    using System.Collections.Generic;

    /// <summary>
    /// The citizen features that can be switched on or off per environment
    /// (handbook §2.7: work that is merged but not ready stays behind a flag).
    /// Values come from the <c>FeatureManagement</c> section. A flag that is
    /// not set is off, so each environment turns on what it should show, e.g.
    /// <c>Myss_FeatureManagement__BusPass=true</c>. Endpoints are gated with
    /// <c>[FeatureGate]</c> and never run when off (usually a 404);
    /// the webclient reads the same values from <c>GET /v1/features</c> to
    /// hide the matching pages and links. Sign-in, registration and the
    /// dashboard are not flagged: every signed-in page depends on them.
    /// </summary>
    public static class FeatureFlags
    {
        /// <summary>The configuration section the flags are read from.</summary>
        public const string SectionName = "FeatureManagement";

        /// <summary>Resetting a forgotten PIN by emailed link.</summary>
        public const string PinReset = "PinReset";

        /// <summary>The public Pre-Eligibility Estimator.</summary>
        public const string EligibilityEstimator = "EligibilityEstimator";

        /// <summary>The public BC Bus Pass request.</summary>
        public const string BusPass = "BusPass";

        /// <summary>The dashboard's Notifications section.</summary>
        public const string Notifications = "Notifications";

        /// <summary>The dashboard's Messages section.</summary>
        public const string Messages = "Messages";

        /// <summary>Service requests, AFA included.</summary>
        public const string ServiceRequests = "ServiceRequests";

        /// <summary>Account Info: phones, PIN and notification preferences.</summary>
        public const string AccountInfo = "AccountInfo";

        /// <summary>The Income Assistance application (intake).</summary>
        public const string Intake = "Intake";

        /// <summary>Virus-scanned file uploads.</summary>
        public const string Attachments = "Attachments";

        /// <summary>The tech demo pages and the demo endpoint.</summary>
        public const string TechDemos = "TechDemos";

        /// <summary>Every flag, in the order <c>GET /v1/features</c> lists them.</summary>
        public static readonly IReadOnlyList<string> All =
        [
            PinReset,
            EligibilityEstimator,
            BusPass,
            Notifications,
            Messages,
            ServiceRequests,
            AccountInfo,
            Intake,
            Attachments,
            TechDemos,
        ];
    }
}
