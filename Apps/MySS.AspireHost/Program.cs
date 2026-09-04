// The one-command local stack: everything README.md's "Local development" section had
// you do by hand — docker compose up, apply the two EF migration contexts, start the
// API, Strapi and the webclient — expressed as an Aspire app model.
//
// The containers mirror compose.yaml (same images, env, host ports and init scripts).
// Data lives in named volumes so it survives restarts, and the volume names are exactly
// the ones docker compose creates (myss_postgres-data etc.), so the two ways of running
// the stack share one set of data — in either direction, though only one can be up at a
// time (same host ports). The containers themselves use Aspire's default session
// lifetime, so they stop when this app host stops.
//
// All configuration comes from IConfiguration (appsettings.json, then user secrets,
// then environment variables) under Aspire:Parameters. Non-secret defaults live in
// appsettings.json; secrets (passwords, Strapi keys) live in user secrets and travel
// as Aspire secret parameters so they are redacted in the dashboard and any generated
// manifest. Apps/MyssContent/.env(.example) is no longer read here — it remains only
// for the docker compose path.
//
// Still manual, because they live inside Strapi's admin UI: the first-visit admin user,
// and the read-only API token MyssApi needs in Strapi:ApiToken (appsettings.local.json).

using System.Globalization;
using Aspire.Hosting.JavaScript;

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

string repoRoot = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", ".."));
string apiDirectory = Path.Combine(repoRoot, "Apps", "MyssApi");
string contentDirectory = Path.Combine(repoRoot, "Apps", "MyssContent");
string webclientDirectory = Path.Combine(repoRoot, "Apps", "MyssWebclient");

// ---------------------------------------------------------------------------
// Postgres — compose.yaml `postgres`. POSTGRES_DB creates the forms database;
// the init scripts create the `strapi` database and role (first start on an
// empty volume only, exactly like compose).
// ---------------------------------------------------------------------------
string postgresDatabase = Require("Aspire:Parameters:Postgres:Database");

IResourceBuilder<ParameterResource> postgresUser =
    builder.AddParameter("postgres-username", Require("Aspire:Parameters:Postgres:Username"));
IResourceBuilder<ParameterResource> postgresPassword =
    SecretParameter("postgres-password", "Aspire:Parameters:Postgres:Password");

// Container names are fixed (WithContainerName) so Docker Desktop shows
// MySS-* instead of Aspire's random-suffixed defaults. Fixed names have one
// sharp edge: if a hard-killed run leaves a container behind, the next start
// fails with a name conflict — `docker rm -f <name>` clears it.
IResourceBuilder<PostgresServerResource> postgres = builder
    .AddPostgres(
        "MySS-Db", postgresUser, postgresPassword,
        port: RequireInt("Aspire:Parameters:Postgres:Port"))
    .WithContainerName("MySS-Db")
    .WithImage(
        Require("Aspire:Parameters:Postgres:Image"),
        Require("Aspire:Parameters:Postgres:Tag"))
    .WithEnvironment("POSTGRES_DB", postgresDatabase)

    // Volume names deliberately match what docker compose creates for
    // compose.yaml (project "myss" + volume name), so the two ways of running
    // the stack share one set of data in either direction.
    .WithDataVolume("myss_postgres-data")
    .WithInitFiles(Path.Combine(repoRoot, "Infra", "Development", "Postgres", "init"));

// Named FormsDb so WithReference injects ConnectionStrings__FormsDb — the name
// MyssApi's configuration already reads.
IResourceBuilder<PostgresDatabaseResource> formsDb =
    postgres.AddDatabase("FormsDb", postgresDatabase);

// ---------------------------------------------------------------------------
// ClamAV — compose.yaml `clamav`. amd64-only image; Apple Silicon runs it
// under Rosetta, same as compose. The signature volume is what makes restarts
// fast — the first ever start still downloads for several minutes.
// ---------------------------------------------------------------------------
builder
    .AddContainer(
        "MySS-VirusScan",
        Require("Aspire:Parameters:ClamAv:Image"),
        Require("Aspire:Parameters:ClamAv:Tag"))
    .WithContainerName("MySS-VirusScan")
    .WithContainerRuntimeArgs("--platform=linux/amd64")
    .WithEndpoint(port: RequireInt("Aspire:Parameters:ClamAv:Port"), targetPort: 3310, name: "clamd")
    .WithVolume("myss_clamav-db", "/var/lib/clamav");

// ---------------------------------------------------------------------------
// MinIO — compose.yaml `minio` + the one-shot `minio-init` bucket creation.
// Credentials match ObjectStorage in MyssApi's appsettings.Development.json.
// ---------------------------------------------------------------------------
IResourceBuilder<ParameterResource> minioRootPassword =
    SecretParameter("minio-root-password", "Aspire:Parameters:Minio:RootPassword");
string minioRootUser = Require("Aspire:Parameters:Minio:RootUser");
string minioBucket = Require("Aspire:Parameters:Minio:Bucket");

IResourceBuilder<ContainerResource> minio = builder
    .AddContainer(
        "MySS-ObjectStorage",
        Require("Aspire:Parameters:Minio:Image"),
        Require("Aspire:Parameters:Minio:Tag"))
    .WithContainerName("MySS-ObjectStorage")
    .WithArgs("server", "/data", "--console-address", ":9001")
    .WithEnvironment("MINIO_ROOT_USER", minioRootUser)
    .WithEnvironment("MINIO_ROOT_PASSWORD", minioRootPassword)
    .WithHttpEndpoint(
        port: RequireInt("Aspire:Parameters:Minio:ApiPort"), targetPort: 9000, name: "api")
    .WithHttpEndpoint(
        port: RequireInt("Aspire:Parameters:Minio:ConsolePort"), targetPort: 9001, name: "console")
    .WithVolume("myss_minio-data", "/data")
    .WithHttpHealthCheck(path: "/minio/health/live", endpointName: "api");

// The credentials reach the shell via environment variables (the password as a
// secret parameter) so the literal never appears in the container's args,
// which the app model and dashboard display. Container-to-container, addressed
// by the network alias Aspire derives from the MinIO resource's name — this
// URL must track that name, on the fixed in-network port 9000.
IResourceBuilder<ContainerResource> minioInit = builder
    .AddContainer(
        "MySS-ObjectStorage-Init",
        Require("Aspire:Parameters:Minio:InitImage"),
        Require("Aspire:Parameters:Minio:InitTag"))
    .WithContainerName("MySS-ObjectStorage-Init")
    .WithEntrypoint("/bin/sh")
    .WithEnvironment("MC_INIT_USER", minioRootUser)
    .WithEnvironment("MC_INIT_PASSWORD", minioRootPassword)
    .WithArgs(
        "-c",
        "mc alias set local http://MySS-ObjectStorage:9000 \"$MC_INIT_USER\" \"$MC_INIT_PASSWORD\" "
        + $"&& mc mb --ignore-existing local/{minioBucket}")
    .WaitFor(minio);

// ---------------------------------------------------------------------------
// EF migrations — README.md's manual `dotnet tool restore` + two
// `dotnet ef database update` runs, chained so each step's output is its own
// dashboard resource. The API waits for the last one.
// ---------------------------------------------------------------------------
IResourceBuilder<ExecutableResource> toolRestore = builder
    .AddExecutable("ef-tool-restore", "dotnet", apiDirectory, "tool", "restore");

// Both migration runs get the same FormsDb reference as the API: `dotnet ef`
// builds the app's host, which reads ConnectionStrings__FormsDb from the
// environment — without it the runs would use the committed development
// connection string and fail whenever the configured password differs.
IResourceBuilder<ExecutableResource> migrateForms = builder
    .AddExecutable(
        "migrate-forms", "dotnet", apiDirectory,
        "ef", "database", "update", "--context", "FormsDbContext")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithReference(formsDb)
    .WaitFor(postgres)
    .WaitForCompletion(toolRestore);

IResourceBuilder<ExecutableResource> migrateAttachments = builder
    .AddExecutable(
        "migrate-attachments", "dotnet", apiDirectory,
        "ef", "database", "update", "--context", "AttachmentsDbContext")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithReference(formsDb)
    .WaitForCompletion(migrateForms);

// ---------------------------------------------------------------------------
// MySSContent — Strapi, run on the host with npm (compose runs the same code
// in a container, fed by Apps/MyssContent/.env). Here its env is assembled
// from IConfiguration: connection details from appsettings.json, secrets from
// user secrets as secret parameters. Running on the host, Strapi reaches
// Postgres through the published port (DatabaseHost: localhost).
// ---------------------------------------------------------------------------
IResourceBuilder<JavaScriptAppResource> content = builder
    .AddJavaScriptApp("MySSContent", contentDirectory, "develop")

    // The installer runs before every start; these flags cut its warm-path
    // time (no registry audit round-trip, prefer the local cache). Strapi's
    // own boot — admin build, schema sync, the every-boot seed — is the bulk
    // of this resource's start time and is inherent to `strapi develop`.
    .WithNpm(installArgs: ["--no-audit", "--no-fund", "--prefer-offline"])
    .WithHttpEndpoint(
        port: RequireInt("Aspire:Parameters:Strapi:Port"), env: "PORT", isProxied: false)

    // Strapi answers its health probe with 204 No Content (MEASURED locally);
    // the check's default expectation is 200, which would report a perfectly
    // healthy Strapi as unhealthy forever.
    .WithHttpHealthCheck(path: "/_health", statusCode: 204)
    .WaitFor(postgres)
    .WithEnvironment("HOST", "0.0.0.0")
    .WithEnvironment("DATABASE_CLIENT", Require("Aspire:Parameters:Strapi:DatabaseClient"))
    .WithEnvironment("DATABASE_HOST", Require("Aspire:Parameters:Strapi:DatabaseHost"))
    .WithEnvironment("DATABASE_PORT", Require("Aspire:Parameters:Strapi:DatabasePort"))
    .WithEnvironment("DATABASE_NAME", Require("Aspire:Parameters:Strapi:DatabaseName"))
    .WithEnvironment("DATABASE_USERNAME", Require("Aspire:Parameters:Strapi:DatabaseUsername"))
    .WithEnvironment("DATABASE_SSL", Require("Aspire:Parameters:Strapi:DatabaseSsl"))

    // Secret values travel as secret parameters, not literals: a literal value
    // is visible in the app model, the dashboard and any generated manifest,
    // while a secret parameter is redacted everywhere it is displayed.
    .WithEnvironment(
        "APP_KEYS", SecretParameter("strapi-app-keys", "Aspire:Parameters:Strapi:AppKeys"))
    .WithEnvironment(
        "API_TOKEN_SALT",
        SecretParameter("strapi-api-token-salt", "Aspire:Parameters:Strapi:ApiTokenSalt"))
    .WithEnvironment(
        "ADMIN_JWT_SECRET",
        SecretParameter("strapi-admin-jwt-secret", "Aspire:Parameters:Strapi:AdminJwtSecret"))
    .WithEnvironment(
        "TRANSFER_TOKEN_SALT",
        SecretParameter("strapi-transfer-token-salt", "Aspire:Parameters:Strapi:TransferTokenSalt"))
    .WithEnvironment(
        "JWT_SECRET", SecretParameter("strapi-jwt-secret", "Aspire:Parameters:Strapi:JwtSecret"))
    .WithEnvironment(
        "ENCRYPTION_KEY",
        SecretParameter("strapi-encryption-key", "Aspire:Parameters:Strapi:EncryptionKey"))
    .WithEnvironment(
        "DATABASE_PASSWORD",
        SecretParameter("strapi-database-password", "Aspire:Parameters:Strapi:DatabasePassword"));

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

    // The same credentials the MinIO container was started with — left to
    // appsettings.Development.json, the API would silently authenticate with
    // the committed static values and every storage call would be rejected
    // whenever the configured root password differs.
    .WithEnvironment("ObjectStorage__AccessKey", minioRootUser)
    .WithEnvironment("ObjectStorage__SecretKey", minioRootPassword)
    .WaitFor(postgres)
    .WaitFor(minio)

    // Until the bucket one-shot has finished, an attachment upload would fail:
    // the API's storage provider writes to the bucket but never creates it.
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
    .WithEndpoint(
        "http", endpoint => endpoint.Port = RequireInt("Aspire:Parameters:Webclient:Port"))
    .WithEnvironment("BROWSER", "none")
    .WaitFor(api);

builder.Build().Run();

// A missing key fails fast at startup with the key's name, rather than
// surfacing later as a half-configured resource.
string Require(string key) =>
    builder.Configuration[key]
    ?? throw new InvalidOperationException(
        $"Missing configuration value '{key}' — add it to appsettings.json or user secrets.");

int RequireInt(string key) => int.Parse(Require(key), CultureInfo.InvariantCulture);

IResourceBuilder<ParameterResource> SecretParameter(string name, string key) =>
    builder.AddParameter(name, Require(key), secret: true);
