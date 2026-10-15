import type { Core } from "@strapi/strapi";

import { seededForms, type Json } from "./lib/form-spec-seed-data";
import { seededRates } from "./lib/eligibility-rate-seed-data";
import { seedRateAction } from "./lib/eligibility-rate-seeding";
import { seededErrorMessages } from "./lib/error-message-seed-data";
import { jsonEqual } from "./lib/json-equal";

const FORM_SPEC_UID = "api::form-spec.form-spec";
const ELIGIBILITY_RATE_UID = "api::eligibility-rate.eligibility-rate";
const ERROR_MESSAGE_UID = "api::error-message.error-message";

// Form specs, the eligibility rate table and the error message
// catalogue are read with a scoped, read-only Strapi API token held by MyssApi
// (Strapi:ApiToken), so the Public role must NOT be able to read them. This
// actively revokes the grant rather than merely no longer creating it, because
// earlier boots of this app wrote those permission rows into the database —
// removing the code that created them would leave the API exactly as open as
// before, in a way that reads as fixed.
//
// Idempotent and safe on a fresh database: nothing to revoke is the normal case.
async function revokePublicRead(strapi: Core.Strapi) {
  const publicRole = await strapi.db
    .query("plugin::users-permissions.role")
    .findOne({ where: { type: "public" } });
  if (!publicRole) return;

  for (const uid of [FORM_SPEC_UID, ELIGIBILITY_RATE_UID, ERROR_MESSAGE_UID]) {
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

// Publishes every seeded form version. A version missing from the database is
// created; a version already present is left untouched. Published form-spec
// versions are an immutable contract for the submissions rendered against them
// (form-spec-rules.ts rule (e), and the beforeDelete warning), so a CHANGED
// spec must roll out as a NEW version entry in the seed — never by mutating an
// existing one. Mutation is impossible anyway: the lifecycle rejects a changed
// published spec, and delete-then-create fails the version-sequence check when
// a later version exists. If a seed diverges from an already-published version,
// bootstrap refuses to boot rather than silently disagree with the database.
// Versions authored in the admin panel (a version number the seed does not
// define) are never touched, because the lookup is keyed by formSpecId + version.
async function seedForms(strapi: Core.Strapi) {
  for (const { formSpecId, title, versions } of seededForms) {
    for (const { version, spec } of versions) {
      const existing = await strapi.documents(FORM_SPEC_UID).findFirst({
        filters: { formSpecId, version },
      });

      if (existing) {
        // Spec is the immutable contract; title is just an admin-panel label,
        // so a title-only difference is tolerated and left as-is.
        if (!jsonEqual(existing.spec, spec)) {
          throw new Error(
            `Seed for form-spec ${formSpecId} v${version} differs from the ` +
              `already-published spec, which is immutable. Roll the change out ` +
              `as a new version entry in the seed instead of changing v${version}.`,
          );
        }
        continue;
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

// Publishes every seeded rate table that is missing (keyed by effectiveDate).
// An existing table is never changed: an admin may have edited it, and a
// restart must not put the seed's values back over that edit.
async function seedRates(strapi: Core.Strapi) {
  for (const seed of seededRates) {
    const { effectiveDate } = seed;
    const draft = await strapi.documents(ELIGIBILITY_RATE_UID).findFirst({
      filters: { effectiveDate },
    });
    const published = await strapi.documents(ELIGIBILITY_RATE_UID).findFirst({
      filters: { effectiveDate },
      status: "published",
    });

    switch (seedRateAction(seed, draft, published)) {
      case "create":
        await strapi.documents(ELIGIBILITY_RATE_UID).create({
          data: {
            effectiveDate,
            // The seed keeps precise readonly types for its own tests; Strapi's
            // JSON columns take the repo's permissive `Json` (the same widening
            // seedForms does with `spec`).
            incomeRows: seed.incomeRows as unknown as Json,
            assetLimits: seed.assetLimits as unknown as Json,
          },
          status: "published",
        });
        strapi.log.info(`Seeded eligibility-rate ${effectiveDate}`);
        break;
      case "keep-changed":
        strapi.log.info(`Kept eligibility-rate ${effectiveDate} unchanged; it differs from the seed`);
        break;
      case "keep-draft-only":
        strapi.log.warn(
          `Kept eligibility-rate ${effectiveDate} unchanged; it is not published, so it is not served`,
        );
        break;
      case "keep":
        break;
    }
  }
}

// Publishes every seeded error message a fresh database is missing, keyed by
// keyword. Deliberately create-if-missing and never an upsert, as with the rate
// table: the catalogue exists so a Service Designer can reword a message in the
// admin panel, and an upsert would silently revert that edit on the next
// restart. A changed seed therefore reaches an existing environment only
// through the admin panel (or by deleting the row so the next boot reseeds it).
// MyssApi serves the same seed text as its compiled fallback while a row is
// missing, so a keyword never goes unworded in between.
async function seedErrorMessages(strapi: Core.Strapi) {
  for (const { keyword, message, note } of seededErrorMessages) {
    const existing = await strapi.documents(ERROR_MESSAGE_UID).findFirst({
      filters: { keyword },
    });
    if (existing) continue;

    await strapi.documents(ERROR_MESSAGE_UID).create({
      data: { keyword, message, note },
      status: "published",
    });
    strapi.log.info(`Seeded error-message ${keyword}`);
  }
}

export default {
  register(/* { strapi }: { strapi: Core.Strapi } */) {},

  async bootstrap({ strapi }: { strapi: Core.Strapi }) {
    await revokePublicRead(strapi);
    await seedForms(strapi);
    await seedRates(strapi);
    await seedErrorMessages(strapi);
  },
};
