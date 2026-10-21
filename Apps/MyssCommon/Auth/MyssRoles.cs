namespace Myss.Common.Auth
{
    /// <summary>
    /// The role names carried in the token. NOTE: the final role set is still to be confirmed
    /// with IDIM. The target design proposes APPLICANT, CLIENT, WORKER, REVIEWER, SUPERVISOR
    /// and ADMIN; these mirror the original application's set.
    /// </summary>
    public static class MyssRoles
    {
        /// <summary>A citizen using their own file.</summary>
        public const string Client = "CLIENT";

        /// <summary>Ministry staff acting on client files.</summary>
        public const string Worker = "WORKER";

        /// <summary>Elevated staff; satisfies the worker policies.</summary>
        public const string Admin = "ADMIN";
    }
}
