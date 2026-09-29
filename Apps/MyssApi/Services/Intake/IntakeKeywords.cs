namespace Myss.Api.Intake
{
    /// <summary>
    /// Stable error keywords owned by the intake module (DOMAIN.CONTEXT.NAME).
    /// The keyword is the contract with the client; the message is not.
    /// </summary>
    public static class IntakeKeywords
    {
        /// <summary>The caller has no registered profile, so cannot start an application.</summary>
        public const string ProfileRequired = "INTAKE.APPLICATION.PROFILE_REQUIRED";

        /// <summary>The caller's row version is behind the stored one: another tab saved first.</summary>
        public const string Conflict = "INTAKE.APPLICATION.CONFLICT";

        /// <summary>The application is not in a state that allows the change.</summary>
        public const string NotEditable = "INTAKE.APPLICATION.NOT_EDITABLE";

        /// <summary>The worker action is not available in the application's current state.</summary>
        public const string ReviewNotAllowed = "INTAKE.REVIEW.NOT_ALLOWED";
    }
}
