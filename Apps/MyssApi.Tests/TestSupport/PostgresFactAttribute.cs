namespace Myss.Api.Tests.TestSupport
{
    /// <summary>
    /// A fact that runs only when a real Postgres is available through the
    /// <c>MYSS_TEST_POSTGRES</c> connection string; otherwise it is reported as
    /// skipped, never as passed. The in-memory suite covers the code paths;
    /// these facts prove the database-enforced ones (constraints, jsonb).
    /// CI sets the variable from a Postgres service; locally the compose
    /// Postgres works: <c>Host=localhost;Port=5432;Database=myss;Username=myss;Password=…</c>.
    /// </summary>
    public sealed class PostgresFactAttribute : FactAttribute
    {
        /// <summary>The environment variable holding the connection string.</summary>
        public const string ConnectionStringVariable = "MYSS_TEST_POSTGRES";

        /// <summary>Initializes a new instance of the <see cref="PostgresFactAttribute"/> class.</summary>
        public PostgresFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(ConnectionString))
            {
                Skip = $"Set {ConnectionStringVariable} to a Postgres connection string to run this test.";
            }
        }

        /// <summary>Gets the configured connection string, or null.</summary>
        public static string? ConnectionString => Environment.GetEnvironmentVariable(ConnectionStringVariable);
    }
}
