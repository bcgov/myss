import type { Core } from "@strapi/strapi";

import { seededForms } from "./lib/form-spec-seed-data";

const FORM_SPEC_UID = "api::form-spec.form-spec";

// Phase 0: form specs are read with a scoped, read-only Strapi API token held
// by MyssApi (Strapi:ApiToken), so the Public role must NOT be able to read
// them. This actively revokes the grant rather than merely no longer creating
// it, because earlier boots of this app wrote those permission rows into the
// database — removing the code that created them would leave the spec API
// exactly as open as before, in a way that reads as fixed.
//
// Idempotent and safe on a fresh database: nothing to revoke is the normal case.
async function revokePublicRead(strapi: Core.Strapi) {
  const publicRole = await strapi.db
    .query("plugin::users-permissions.role")
    .findOne({ where: { type: "public" } });
  if (!publicRole) return;

  for (const action of [`${FORM_SPEC_UID}.find`, `${FORM_SPEC_UID}.findOne`]) {
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

export default {
  register(/* { strapi }: { strapi: Core.Strapi } */) {},

  async bootstrap({ strapi }: { strapi: Core.Strapi }) {
    await revokePublicRead(strapi);
    await seedForms(strapi);
  },
};
