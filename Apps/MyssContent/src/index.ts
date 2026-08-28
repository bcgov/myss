import type { Core } from "@strapi/strapi";

import busPassFormSpec from "./buspassform.json";
import { seededForms, type Json } from "./lib/form-spec-seed-data";
import { seededRates } from "./lib/eligibility-rate-seed-data";

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

// Creates any missing seeded versions, one entry per version, across every
// seeded form. Existing entries are left untouched.
async function seedForms(strapi: Core.Strapi) {
  for (const { formSpecId, title, versions } of seededForms) {
    for (const { version, spec } of versions) {
      const existing = await strapi.documents(FORM_SPEC_UID).findFirst({
        filters: { formSpecId, version },
      });
      if (existing) continue;

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

// Creates any missing seeded rate tables (create-only-if-missing, keyed by
// effectiveDate), one published entry each. Existing entries are left untouched.
async function seedRates(strapi: Core.Strapi) {
  for (const { effectiveDate, incomeRows, assetLimits } of seededRates) {
    const existing = await strapi.documents(ELIGIBILITY_RATE_UID).findFirst({
      filters: { effectiveDate },
    });
    if (existing) continue;

    await strapi.documents(ELIGIBILITY_RATE_UID).create({
      data: {
        effectiveDate,
        // The seed keeps precise readonly types for its own tests; Strapi's JSON
        // columns take the repo's permissive `Json` (the same widening seedForms
        // does with `spec`).
        incomeRows: incomeRows as unknown as Json,
        assetLimits: assetLimits as unknown as Json,
      },
      status: "published",
    });
    strapi.log.info(`Seeded eligibility-rate ${effectiveDate}`);
  }
}

async function seedBusPassForm(strapi: Core.Strapi) {
  // Seed the initial Bus Pass version for fresh environments. Later changes
  // should be published as new versions through Strapi Admin.
  const version = 1;
  const existing = await strapi.documents(FORM_SPEC_UID).findFirst({
    filters: { formSpecId: "bc-bus-pass", version },
  });
  // Never overwrite an entry that has already been created or edited in Strapi.
  if (existing) return;

  await strapi.documents(FORM_SPEC_UID).create({
    data: {
      formSpecId: "bc-bus-pass",
      version,
      title: "BC Bus Pass",
      spec: busPassFormSpec,
    },
    status: "published",
  });
  strapi.log.info(`Seeded form-spec bc-bus-pass v${version}`);
}

export default {
  register(/* { strapi }: { strapi: Core.Strapi } */) {},

  async bootstrap({ strapi }: { strapi: Core.Strapi }) {
    await revokePublicRead(strapi);
    await seedForms(strapi);
    await seedBusPassForm(strapi);
    await seedRates(strapi);
  },
};
