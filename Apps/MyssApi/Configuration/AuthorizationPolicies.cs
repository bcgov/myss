namespace Myss.Api.Configuration
{
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// The role names carried in the token. NOTE: the final role set is still to be confirmed
    /// with IDIM (the handbook proposes APPLICANT/CLIENT/WORKER/REVIEWER/SUPERVISOR/ADMIN);
    /// these mirror the original application's set.
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

    /// <summary>Authorization policy names used by <c>[Authorize(Policy = ...)]</c>.</summary>
    public static class MyssPolicies
    {
        /// <summary>Citizen-facing endpoints.</summary>
        public const string Client = "Client";

        /// <summary>Staff endpoints; ADMIN also satisfies this.</summary>
        public const string Worker = "Worker";

        /// <summary>Administrative endpoints.</summary>
        public const string Admin = "Admin";

        /// <summary>
        /// Staff endpoints that additionally require a government (IDIR) identity — a
        /// deliberate hardening control, not a reproduction of the original behaviour.
        /// </summary>
        public const string WorkerWithIdir = "WorkerWithIdir";
    }

    /// <summary>
    /// Registers the app's authorization policies.
    /// <para>
    /// PERMANENT core: policies are evaluated against the flattened claims produced by
    /// <see cref="KeycloakClaims"/>, so they are identical under Option 1 and Option 2.
    /// </para>
    /// </summary>
    public static class AuthorizationPolicies
    {
        /// <summary>Adds the MySS authorization policies to the service collection.</summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection, for chaining.</returns>
        public static IServiceCollection AddMyssAuthorization(this IServiceCollection services)
        {
            services
                .AddAuthorizationBuilder()
                .AddPolicy(
                    MyssPolicies.Client,
                    policy => policy.RequireAuthenticatedUser().RequireRole(MyssRoles.Client))
                .AddPolicy(
                    MyssPolicies.Worker,
                    policy => policy
                        .RequireAuthenticatedUser()
                        .RequireRole(MyssRoles.Worker, MyssRoles.Admin))
                .AddPolicy(
                    MyssPolicies.Admin,
                    policy => policy.RequireAuthenticatedUser().RequireRole(MyssRoles.Admin))
                .AddPolicy(
                    MyssPolicies.WorkerWithIdir,
                    policy => policy
                        .RequireAuthenticatedUser()
                        .RequireRole(MyssRoles.Worker, MyssRoles.Admin)
                        .RequireClaim(KeycloakClaims.IdirUsernameClaimType));

            return services;
        }
    }
}
