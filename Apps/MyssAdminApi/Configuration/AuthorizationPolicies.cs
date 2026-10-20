namespace Myss.AdminApi.Configuration
{
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// The role names carried in the token. The admin API's policy does not test a
    /// role today; these name the mock personas' roles so they match MyssApi's.
    /// </summary>
    public static class MyssRoles
    {
        /// <summary>A citizen using their own file.</summary>
        public const string Client = "CLIENT";

        /// <summary>Ministry staff acting on client files.</summary>
        public const string Worker = "WORKER";

        /// <summary>Elevated staff.</summary>
        public const string Admin = "ADMIN";
    }

    /// <summary>Authorization policy names used by <c>[Authorize(Policy = ...)]</c>.</summary>
    public static class MyssPolicies
    {
        /// <summary>
        /// Admin form-editor and rates-editor endpoints: any authenticated caller with a
        /// government (IDIR) identity. Narrower than [Authorize] alone, which also
        /// admits BCeID citizens signing in; the IDIR claim is the gate.
        /// </summary>
        public const string AdminIdir = "AdminIdir";
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
                        .RequireClaim(KeycloakClaims.IdirUsernameClaimType));

            return services;
        }
    }
}
