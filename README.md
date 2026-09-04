# My Self Serve

"My Self Serve (MySS) provides online access to income and disability assistance for residents of British Columbia."

# Local development

Prerequisites: Docker, the .NET 10 SDK, Node 22+.

## One command: Aspire

Everything below — the containers, the EF migrations, and all three apps — can be run
with a single command through the Aspire app host:

```bash
dotnet run --project Apps/MySS.AspireHost
```

It starts the compose containers' equivalents (Postgres 17, ClamAV, MinIO with its
bucket-creation one-shot), applies both EF migration contexts once Postgres is healthy,
then starts `MySSApi` (http://localhost:5000), `MySSContent` (Strapi,
http://localhost:1337, run with npm on the host) and `MyssWebClient` (Vite) — with a
dashboard (URL printed at startup) showing every resource's logs, health and telemetry.
Strapi's env is read from `Apps/MyssContent/.env` when present, else straight from the
committed `.env.example`, so the copy step is optional on this path.

Container data lives in named volumes (`myss_postgres-data`, `myss_clamav-db`,
`myss_minio-data`) and survives restarts; the containers themselves stop when the app
host stops. The names are exactly what `docker compose` creates for compose.yaml, so the
Aspire and compose paths share one set of data in either direction — but only one of the
two can be up at a time (same host ports).

Still manual, because they live inside Strapi's admin UI: the first-visit admin user,
and the API token for `Strapi:ApiToken` (goes in `Apps/MyssApi/appsettings.local.json`).

## Start the stack manually (compose)

Strapi reads its configuration from a gitignored `.env` — create it from the
example first (the committed values work as-is for local development):

```bash
cp Apps/MyssContent/.env.example Apps/MyssContent/.env
docker compose up -d --wait
```

This brings up Postgres 17, Strapi, ClamAV and MinIO (the ClamAV first start
downloads its signature databases, so `--wait` can take a few minutes; the
`minio-init` one-shot creates the `myss-attachments` bucket). The databases
initialize themselves on the first start:

- `myss` (the application database) is created by the Postgres image
  (`POSTGRES_DB`).
- `strapi` and its role come from
  `Infra/Development/Postgres/init/01-strapi-db.sql`. Init scripts only run
  when the data volume is empty — they do not re-run on restarts.
- Strapi applies its own schema migrations and seeds the POC form specs at
  boot.

The forms and attachments schemas in `myss` are **not** automatic. Apply the
EF migrations once (and again after pulling new migrations):

```bash
cd Apps/MyssApi
dotnet tool restore
dotnet ef database update --context FormsDbContext
dotnet ef database update --context AttachmentsDbContext
```

The connection string in `appsettings.Development.json` already points at the
compose Postgres.

## Run the apps

- API: `cd Apps/MyssApi && dotnet run` → http://localhost:5000
- Webclient: `cd Apps/MyssWebclient && npm install && npm run dev` →
  http://localhost:5173 (the forms demo is under `/techdemos/forms`)
- Strapi admin: http://localhost:1337/admin — the first visit asks you to
  create the initial admin user (local account, any credentials)

## Tests

- ClamAV: `./test-clamav.sh` — confirms the local clamd answers and actually
  detects (EICAR via INSTREAM, the same protocol the API uses). Run it when
  attachment scanning misbehaves; on a first-ever ClamAV start it will say the
  daemon is not answering until the signature download finishes.
- API: `dotnet test Apps/MyssApi.Tests`
- Webclient: `cd Apps/MyssWebclient && npm run test:unit` and
  `npm run test:browser-headless` — the browser tests need a one-time
  `npx playwright install chromium`

## Reset

`docker compose down` keeps the data. `docker compose down -v` wipes it — the
next `up` re-runs the init script and the Strapi seed; re-apply the EF
migrations afterwards.

# Previous work

## Reference Prototypes

https://github.com/bcgov/myss-web
https://github.com/bcgov/myss-api

## Current running app

https://myselfserve.gov.bc.ca/
