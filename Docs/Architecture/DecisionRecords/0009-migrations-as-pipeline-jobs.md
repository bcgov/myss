# ADR-0009 — EF migrations run in the pipeline as a Job from the app image

- **Status:** Accepted
- **Date:** 2026-09-29
- **Deciders:** Development team
- **Related:** Handbook §7.3 (migrations as OpenShift Jobs, never by hand), §9.5; ADR-0003 (EF Core code-first); gitops `t-migrate-db`, `manifests/apps/myss-api/base/cronjob.migrate.myss-api.yaml`

## Context

Until now every environment's schema was applied from a laptop: port-forward
the Postgres primary and run `dotnet ef database update` per context. The
build pipeline restarted the API on every merge with a comment saying a
migration Job should come first. With four contexts and a schema that now
changes with each slice, "by hand" is the wrong answer, and handbook §7.3
already decided the mechanism: migrations run as a Job in the pipeline.

What was open is *how* the Job gets the migrations. The API image is built by
the S2I .NET builder with no Dockerfile, so the runtime image holds the
published app and nothing else.

## Options considered

1. **A migrate mode inside the API image.** `MyssApi --migrate` builds a host
   without `Startup`, runs `Migrate` on every context in schema order, and
   exits non-zero on failure. The pipeline creates a Job from the freshly
   promoted image with that argument and the existing connection-string
   secret, waits for it, and only then restarts the pods. Same image for
   schema and code; no SDK in production; no change to the S2I build.
2. **An EF migrations bundle.** The EF team's production artifact: one
   self-contained executable per context from `dotnet ef migrations bundle`.
   Cleaner separation, but the S2I build would need a custom assemble step
   to produce and ship four bundles, and a place to keep them.
3. **Migrate at application startup or from an init container.** Replicas
   race each other, the deploy is coupled to pod boot, and it is the
   implicit application of schema changes §7.3 rules out.

## Decision

Option 1. `Configuration/MigrationRunner` is the migrate mode; `Program`
selects it on `--migrate` or on `Myss_MigrateAndExit=true`. The Job uses
the environment variable: the S2I image has no entrypoint and starts the
app through a run script, so container `args` replace that script instead
of reaching the app (the first run proved it: the kernel tried to execute
`--migrate` as a program). In gitops, a suspended CronJob per environment
holds the Job template (Jobs are immutable, so a template is what Argo CD
can own), and the `t-migrate-db` task creates a Job from it between the
image retag and the rollout restart in the build pipeline and both
promotion pipelines. The task is a no-op for apps without a database,
gated by a pipeline parameter rather than a `when`, so the restart task's
ordering stays simple. A `pipeline-migrate` Role per namespace grants the
pipeline service account only Job creation and log reads.

## Consequences

- Easier: a merge that carries a migration deploys correctly on its own; a
  failed migration stops the run and leaves the old pods serving; the
  migration is proven against real Postgres by a `[PostgresFact]` test in
  CI and by the Job itself.
- Harder: migrations must stay additive so code rollback never needs a
  schema rollback; a destructive change is two releases (expand, then
  contract). The dev cluster's Postgres has no backups, so test and prod
  need a pgBackRest repository before any migration runs there.
- Watch: the Job runs as the `myss` schema owner today; when per-module
  database roles arrive (handbook §7.1), the Job keeps the owner role and
  the app pods get restricted ones, which the separate Job makes possible.
  EF Core takes a database lock while migrating, so concurrent Jobs cannot
  interleave, but two pipeline runs in flight will still serialise on it.
