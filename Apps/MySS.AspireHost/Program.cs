// The one-command local stack: everything README.md's "Local development" section had
// you do by hand — copy the Strapi .env, docker compose up, apply the two EF migration
// contexts, start the API, Strapi and the webclient — expressed as an Aspire app model.
//
// The containers mirror compose.yaml (same images, env, host ports and init scripts).
// Data lives in named volumes so it survives restarts, and the volume names are exactly
// the ones docker compose creates (myss_postgres-data etc.), so the two ways of running
// the stack share one set of data — in either direction, though only one can be up at a
// time (same host ports). The containers themselves use Aspire's default session
// lifetime, so they stop when this app host stops.
//
// Still manual, because they live inside Strapi's admin UI: the first-visit admin user,
// and the read-only API token MyssApi needs in Strapi:ApiToken (appsettings.local.json).

using System.Text;
using Aspire.Hosting.JavaScript;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

string repoRoot = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", ".."));
string apiDirectory = Path.Combine(repoRoot, "Apps", "MyssApi");
string contentDirectory = Path.Combine(repoRoot, "Apps", "MyssContent");
string webclientDirectory = Path.Combine(repoRoot, "Apps", "MyssWebclient");

// ---------------------------------------------------------------------------
// Postgres — compose.yaml `postgres`. POSTGRES_DB creates `myss`; the init
// scripts create the `strapi` database and role (first start on an empty
// volume only, exactly like compose).
// ---------------------------------------------------------------------------
IResourceBuilder<ParameterResource> postgresUser =
    builder.AddParameter("postgres-username", "myss");
IResourceBuilder<ParameterResource> postgresPassword =
    builder.AddParameter("postgres-password", "myss-local-dev", secret: true);

// Container names are fixed (WithContainerName) so Docker Desktop shows
// MySS-* instead of Aspire's random-suffixed defaults. Fixed names have one
// sharp edge: if a hard-killed run leaves a container behind, the next start
// fails with a name conflict — `docker rm -f <name>` clears it.
IResourceBuilder<PostgresServerResource> postgres = builder
    .AddPostgres("MySS-Db", postgresUser, postgresPassword, port: 5432)
    .WithContainerName("MySS-Db")
    .WithImage("postgres", "17-alpine")
    .WithEnvironment("POSTGRES_DB", "myss")

    // Volume names deliberately match what docker compose creates for
    // compose.yaml (project "myss" + volume name), so the two ways of running
    // the stack share one set of data in either direction.
    .WithDataVolume("myss_postgres-data")
    .WithInitFiles(Path.Combine(repoRoot, "Infra", "Development", "Postgres", "init"));

// Named FormsDb so WithReference injects ConnectionStrings__FormsDb — the name
// MyssApi's configuration already reads.
IResourceBuilder<PostgresDatabaseResource> formsDb = postgres.AddDatabase("FormsDb", "myss");

// ---------------------------------------------------------------------------
// ClamAV — compose.yaml `clamav`. amd64-only image; Apple Silicon runs it
// under Rosetta, same as compose. The signature volume is what makes restarts
// fast — the first ever start still downloads for several minutes.
// ---------------------------------------------------------------------------
builder
    .AddContainer("MySS-VirusScan", "clamav/clamav", "1.5.3")
    .WithContainerName("MySS-VirusScan")
    .WithContainerRuntimeArgs("--platform=linux/amd64")
    .WithEndpoint(port: 3310, targetPort: 3310, name: "clamd")
    .WithVolume("myss_clamav-db", "/var/lib/clamav");

// ---------------------------------------------------------------------------
// MinIO — compose.yaml `minio` + the one-shot `minio-init` bucket creation.
// Credentials match ObjectStorage in MyssApi's appsettings.Development.json.
// ---------------------------------------------------------------------------
IResourceBuilder<ContainerResource> minio = builder
    .AddContainer("MySS-ObjectStorage", "minio/minio", "latest")
    .WithContainerName("MySS-ObjectStorage")
    .WithArgs("server", "/data", "--console-address", ":9001")
    .WithEnvironment("MINIO_ROOT_USER", "myss")
    .WithEnvironment("MINIO_ROOT_PASSWORD", "myss-local-dev")
    .WithHttpEndpoint(port: 9000, targetPort: 9000, name: "api")
    .WithHttpEndpoint(port: 9001, targetPort: 9001, name: "console")
    .WithVolume("myss_minio-data", "/data")
    .WithHttpHealthCheck(path: "/minio/health/live", endpointName: "api");

IResourceBuilder<ContainerResource> minioInit = builder
    .AddContainer("MySS-ObjectStorage-Init", "minio/mc", "latest")
    .WithContainerName("MySS-ObjectStorage-Init")
    .WithEntrypoint("/bin/sh")

    // Container-to-container, addressed by the network alias Aspire derives
    // from the MinIO resource's name — this URL must track that name.
    .WithArgs(
        "-c",
        "mc alias set local http://MySS-ObjectStorage:9000 myss myss-local-dev "
        + "&& mc mb --ignore-existing local/myss-attachments")
    .WaitFor(minio);

// ---------------------------------------------------------------------------
// EF migrations — README.md's manual `dotnet tool restore` + two
// `dotnet ef database update` runs, chained so each step's output is its own
// dashboard resource. The API waits for the last one.
// ---------------------------------------------------------------------------
IResourceBuilder<ExecutableResource> toolRestore = builder
    .AddExecutable("ef-tool-restore", "dotnet", apiDirectory, "tool", "restore");

IResourceBuilder<ExecutableResource> migrateForms = builder
    .AddExecutable(
        "migrate-forms", "dotnet", apiDirectory,
        "ef", "database", "update", "--context", "FormsDbContext")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WaitFor(postgres)
    .WaitForCompletion(toolRestore);

IResourceBuilder<ExecutableResource> migrateAttachments = builder
    .AddExecutable(
        "migrate-attachments", "dotnet", apiDirectory,
        "ef", "database", "update", "--context", "AttachmentsDbContext")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WaitForCompletion(migrateForms);

// ---------------------------------------------------------------------------
// MySSContent — Strapi, run on the host with npm (compose runs the same code
// in a container). Its env comes from Apps/MyssContent/.env, falling back to
// the committed .env.example, whose values work as-is locally — so the manual
// "cp .env.example .env" step is no longer load-bearing.
// ---------------------------------------------------------------------------
Dictionary<string, string> strapiEnv = ReadDotEnv(
    Path.Combine(contentDirectory, ".env"),
    Path.Combine(contentDirectory, ".env.example"));

IResourceBuilder<JavaScriptAppResource> content = builder
    .AddJavaScriptApp("MySSContent", contentDirectory, "develop")

    // The installer runs before every start; these flags cut its warm-path
    // time (no registry audit round-trip, prefer the local cache). Strapi's
    // own boot — admin build, schema sync, the every-boot seed — is the bulk
    // of this resource's start time and is inherent to `strapi develop`.
    .WithNpm(installArgs: ["--no-audit", "--no-fund", "--prefer-offline"])
    .WithHttpEndpoint(port: 1337, env: "PORT", isProxied: false)

    // Strapi answers its health probe with 204 No Content (MEASURED locally);
    // the check's default expectation is 200, which would report a perfectly
    // healthy Strapi as unhealthy forever.
    .WithHttpHealthCheck(path: "/_health", statusCode: 204)
    .WaitFor(postgres);

// Secret values travel as secret parameters, not literals: a literal value is
// visible in the app model, the dashboard and any generated manifest, while a
// secret parameter is redacted everywhere it is displayed.
HashSet<string> strapiSecretKeys = new(StringComparer.Ordinal)
{
    "APP_KEYS",
    "API_TOKEN_SALT",
    "ADMIN_JWT_SECRET",
    "TRANSFER_TOKEN_SALT",
    "JWT_SECRET",
    "ENCRYPTION_KEY",
    "DATABASE_PASSWORD",
};

foreach ((string key, string value) in strapiEnv)
{
    if (strapiSecretKeys.Contains(key))
    {
        content.WithEnvironment(
            key,
            builder.AddParameter(
                $"strapi-{key.ToLowerInvariant().Replace('_', '-')}", value, secret: true));
    }
    else
    {
        content.WithEnvironment(key, value);
    }
}

// Running on the host, Strapi reaches Postgres through the published port —
// the .env.example values already say localhost:5432.
content.WithEnvironment("HOST", "0.0.0.0");
content.WithEnvironment("DATABASE_HOST", "localhost");
content.WithEnvironment("DATABASE_PORT", "5432");

// ---------------------------------------------------------------------------
// MySSApi — waits for the schema to exist. Connection string, Strapi URL and
// MinIO URL are injected from the resources they describe; the values equal
// what appsettings.Development.json already holds, so the API behaves the
// same run either way. ClamAV is deliberately not awaited: its first start
// can take minutes and only attachment scanning needs it.
// ---------------------------------------------------------------------------
IResourceBuilder<ProjectResource> api = builder
    .AddProject<Projects.MyssApi>("MySSApi")
    .WithReference(formsDb)
    .WithEnvironment("Strapi__BaseUrl", content.GetEndpoint("http"))
    .WithEnvironment("ObjectStorage__ServiceUrl", minio.GetEndpoint("api"))
    .WaitFor(postgres)
    .WaitFor(minio)

    // Until the bucket one-shot has finished, an attachment upload would fail:
    // the API's storage provider writes to myss-attachments but never creates it.
    .WaitForCompletion(minioInit)
    .WaitForCompletion(migrateAttachments);

// MyssApi's OTLP exporter is config-driven (OpenTelemetry:Endpoint), not
// env-var-driven, so point it at the dashboard explicitly and its traces and
// metrics show up there.
string? dashboardOtlp = builder.Configuration["DOTNET_DASHBOARD_OTLP_ENDPOINT_URL"];
if (!string.IsNullOrWhiteSpace(dashboardOtlp))
{
    api.WithEnvironment("OpenTelemetry__Endpoint", dashboardOtlp);
    api.WithEnvironment("OpenTelemetry__ExportProtocol", "Grpc");
}

// ---------------------------------------------------------------------------
// MyssWebClient — Vite dev server on its usual 5173, so the webclient's
// default API_URL (http://localhost:5000) and the API's CORS expectations
// hold unchanged.
// ---------------------------------------------------------------------------
builder
    .AddViteApp("MyssWebClient", webclientDirectory)
    .WithNpm(installArgs: ["--no-audit", "--no-fund", "--prefer-offline"])

    // AddViteApp gives the dev server a dynamic --port behind Aspire's proxy;
    // pin the public side to Vite's usual 5173 so bookmarks and any fixed
    // OIDC redirect URIs keep working.
    .WithEndpoint("http", endpoint => endpoint.Port = 5173)
    .WithEnvironment("BROWSER", "none")
    .WaitFor(api);

builder.Build().Run();

// Reads the first .env-style file that exists. KEY=VALUE lines; # comments and
// blanks skipped; surrounding quotes stripped (APP_KEYS ships quoted).
static Dictionary<string, string> ReadDotEnv(params string[] candidates)
{
    Dictionary<string, string> values = new(StringComparer.Ordinal);
    string? path = Array.Find(candidates, File.Exists);
    if (path is null)
    {
        return values;
    }

    foreach (string rawLine in File.ReadAllLines(path, Encoding.UTF8))
    {
        string line = rawLine.Trim();
        int separator = line.IndexOf('=', StringComparison.Ordinal);
        if (line.Length == 0 || line.StartsWith('#') || separator <= 0)
        {
            continue;
        }

        string key = line[..separator].Trim();
        string value = line[(separator + 1)..].Trim();
        if (value.Length >= 2
            && ((value.StartsWith('"') && value.EndsWith('"'))
                || (value.StartsWith('\'') && value.EndsWith('\''))))
        {
            value = value[1..^1];
        }

        values[key] = value;
    }

    return values;
}
