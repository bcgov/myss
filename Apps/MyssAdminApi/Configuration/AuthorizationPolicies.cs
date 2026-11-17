namespace Myss.AdminApi.Configuration
{
    using Microsoft.Extensions.DependencyInjection;
    using Myss.Common.Auth;

    /// <summary>Authorization policy names used by <c>[Authorize(Policy = ...)]</c>.</summary>
    public static class MyssPolicies
    {
        /// <summary>
        /// Admin form-editor and rates-editor endpoints: any authenticated caller with a
        /// government (IDIR) identity. Narrower than [Authorize] alone, which also
        /// admits BCeID citizens signing in; the IDIR claim is the gate.
        /// </summary>
        public const string AdminIdir = "AdminIdir";

        /// <summary>
        /// Worker endpoints (application review, stored submissions, the bus pass PDF):
        /// the WORKER or ADMIN role and a government (IDIR) identity. Moved from MyssApi
        /// with those endpoints, unchanged.
        /// </summary>
        public const string WorkerWithIdir = "WorkerWithIdir";
    }

    /// <summary>
    /// Registers the admin API's authorization policies.
    /// <para>
    /// Policies are evaluated against the flattened claims produced by
    /// <see cref="KeycloakClaims"/>, so they are identical under Option 1 and Option 2.
    /// </para>
    /// </summary>
    public static class AuthorizationPolicies
    {
        /// <summary>Adds the admin API's authorization policies to the service collection.</summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection, for chaining.</returns>
        public static IServiceCollection AddMyssAuthorization(this IServiceCollection services)
        {
            services
                .AddAuthorizationBuilder()
                .AddPolicy(
                    MyssPolicies.AdminIdir,
                    policy => policy
                        .RequireAuthenticatedUser()
                        .RequireClaim(KeycloakClaims.IdirUsernameClaimType))
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
