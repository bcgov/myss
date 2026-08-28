import type { Core } from "@strapi/strapi";

import { seededForms, type Json } from "./lib/form-spec-seed-data";
import { seededRates } from "./lib/eligibility-rate-seed-data";
import { jsonEqual } from "./lib/json-equal";

const FORM_SPEC_UID = "api::form-spec.form-spec";
const ELIGIBILITY_RATE_UID = "api::eligibility-rate.eligibility-rate";

// Phase 0: form specs and the eligibility rate table are read with a scoped,
// read-only Strapi API token held by MyssApi (Strapi:ApiToken), so the Public
// role must NOT be able to read them. This actively revokes the grant rather
// than merely no longer creating it, because earlier boots of this app wrote
// those permission rows into the database — removing the code that created them
// would leave the API exactly as open as before, in a way that reads as fixed.
//
// Idempotent and safe on a fresh database: nothing to revoke is the normal case.
async function revokePublicRead(strapi: Core.Strapi) {
  const publicRole = await strapi.db
    .query("plugin::users-permissions.role")
    .findOne({ where: { type: "public" } });
  if (!publicRole) return;

  for (const uid of [FORM_SPEC_UID, ELIGIBILITY_RATE_UID]) {
    for (const action of [`${uid}.find`, `${uid}.findOne`]) {
      const existing = await strapi.db
        .query("plugin::users-permissions.permission")
        .findOne({ where: { action, role: publicRole.id } });
      if (existing) {
        await strapi.db
          .query("plugin::users-permissions.permission")
          .delete({ where: { id: existing.id } });
        strapi.log.info(`Revoked public permission ${action}`);
      }
    }
  }
}

// Publishes every seeded form version (an upsert). A version missing from the
// database is created; a version already present is re-published ONLY when the
// seed's title or spec has actually changed. So editing the seed file and
// restarting Strapi rolls the change out with no manual delete. Versions
// authored in the admin panel (a version number the seed does not define) are
// never touched, because the lookup is keyed by formSpecId + version.
async function seedForms(strapi: Core.Strapi) {
  for (const { formSpecId, title, versions } of seededForms) {
    for (const { version, spec } of versions) {
      const existing = await strapi.documents(FORM_SPEC_UID).findFirst({
        filters: { formSpecId, version },
      });

      if (existing) {
        if (existing.title === title && jsonEqual(existing.spec, spec)) continue;
        await strapi.documents(FORM_SPEC_UID).delete({
          documentId: existing.documentId,
        });
        strapi.log.info(`Re-seeding changed form-spec ${formSpecId} v${version}`);
      }

      await strapi.documents(FORM_SPEC_UID).create({
        data: {
          formSpecId,
          version,
          title,
          spec,
        },
        status: "published",
      });
      strapi.log.info(`Seeded form-spec ${formSpecId} v${version}`);
    }
  }
}

// Publishes every seeded rate table (an upsert, keyed by effectiveDate). Missing
// tables are created; an existing one is re-published only when its incomeRows
// or assetLimits differ from the seed, so a rate edit also rolls out on a plain
// restart.
async function seedRates(strapi: Core.Strapi) {
  for (const { effectiveDate, incomeRows, assetLimits } of seededRates) {
    const existing = await strapi.documents(ELIGIBILITY_RATE_UID).findFirst({
      filters: { effectiveDate },
    });

    const data = {
      effectiveDate,
      // The seed keeps precise readonly types for its own tests; Strapi's JSON
      // columns take the repo's permissive `Json` (the same widening seedForms
      // does with `spec`).
      incomeRows: incomeRows as unknown as Json,
      assetLimits: assetLimits as unknown as Json,
    };

    if (existing) {
      if (
        jsonEqual(existing.incomeRows, data.incomeRows) &&
        jsonEqual(existing.assetLimits, data.assetLimits)
      ) {
        continue;
      }
      await strapi.documents(ELIGIBILITY_RATE_UID).delete({
        documentId: existing.documentId,
      });
      strapi.log.info(`Re-seeding changed eligibility-rate ${effectiveDate}`);
    }

    await strapi.documents(ELIGIBILITY_RATE_UID).create({
      data,
      status: "published",
    });
    strapi.log.info(`Seeded eligibility-rate ${effectiveDate}`);
  }
}

export default {
  register(/* { strapi }: { strapi: Core.Strapi } */) {},

  async bootstrap({ strapi }: { strapi: Core.Strapi }) {
    await revokePublicRead(strapi);
    await seedForms(strapi);
    await seedRates(strapi);
  },
};
