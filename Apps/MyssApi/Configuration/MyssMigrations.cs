namespace Myss.Api.Configuration
{
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

    /// <summary>
    /// Where the migrations of the shared data model live. The forms, intake and
    /// platform contexts are defined in MyssData, which MyssAdminApi shares, but
    /// their migrations stay here: MyssApi is the only service that changes the
    /// schema (it runs <c>--migrate</c> as the deployment Job). EF looks for
    /// migrations in the context's own assembly unless told otherwise, so every
    /// place this API builds one of those contexts must point it here, or
    /// <c>Migrate</c> finds none and silently does nothing.
    /// </summary>
    public static class MyssMigrations
    {
        /// <summary>The assembly holding the migrations: this one.</summary>
        public static readonly string AssemblyName = typeof(MyssMigrations).Assembly.GetName().Name!;

        /// <summary>
        /// Points a shared-model context at this assembly's migrations and its
        /// own history table.
        /// </summary>
        /// <param name="npgsql">The provider options being configured.</param>
        /// <param name="historySchema">The schema of the context's history table, or null for the default (the forms context's).</param>
        public static void Configure(NpgsqlDbContextOptionsBuilder npgsql, string? historySchema)
        {
            npgsql.MigrationsAssembly(AssemblyName);
            if (historySchema is not null)
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", historySchema);
            }
        }
    }
}
