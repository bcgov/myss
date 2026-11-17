namespace Myss.Data
{
    using System;

    /// <summary>
    /// How a <see cref="MyssUserProfile"/> was made, as stored in its
    /// <see cref="MyssUserProfile.Origin"/>.
    /// </summary>
    public static class ProfileOrigins
    {
        /// <summary>The citizen registered, signed in with their own identity.</summary>
        public const string Registration = "registration";

        /// <summary>
        /// A worker submitted an Application for Assistance for a client who
        /// cannot submit their own (MYSS-321). Nobody signs in to it.
        /// </summary>
        public const string WorkerOverride = "worker-override";

        /// <summary>
        /// The prefix of a worker override account's subject. A Keycloak subject
        /// never has it, so no sign-in can ever reach the account, and the
        /// account cannot collide with a citizen's.
        /// </summary>
        public const string WorkerOverrideSubjectPrefix = "worker-override:";

        /// <summary>Builds a new worker override account's subject.</summary>
        /// <param name="id">A fresh identifier for the account.</param>
        /// <returns>"worker-override:{id}".</returns>
        public static string WorkerOverrideSubject(Guid id) => WorkerOverrideSubjectPrefix + id.ToString("D");
    }
}
