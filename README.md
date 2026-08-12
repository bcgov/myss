# My Self Serve

"My Self Serve (MySS) provides online access to income and disability assistance for residents of British Columbia."

# Local development

Prerequisites: Docker, the .NET 10 SDK, Node 22+.

## Start the stack

Strapi reads its configuration from a gitignored `.env` — create it from the
example first (the committed values work as-is for local development):

```bash
cp Apps/MyssContent/.env.example Apps/MyssContent/.env
docker compose up -d --wait
```

This brings up Postgres 17 and Strapi. The databases initialize themselves on
the first start:

- `myss` (the application database) is created by the Postgres image
  (`POSTGRES_DB`).
- `strapi` and its role come from
  `Infra/Development/Postgres/init/01-strapi-db.sql`. Init scripts only run
  when the data volume is empty — they do not re-run on restarts.
- Strapi applies its own schema migrations and seeds the POC form specs at
  boot.

The forms schema in `myss` is **not** automatic. Apply the EF migrations once
(and again after pulling new migrations):

```bash
cd Apps/MyssApi
dotnet tool restore
dotnet ef database update --context FormsDbContext
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
