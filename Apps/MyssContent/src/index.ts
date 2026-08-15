import type { Core } from "@strapi/strapi";

const FORM_SPEC_UID = "api::form-spec.form-spec";

type Json = string | number | boolean | null | Json[] | { [key: string]: Json };

// POC test form. v1 is seeded here so a fresh database has a working form;
// later versions are authored through the admin panel as new entries.
const testFormSpecV1: Json = {
  display: "form",
  components: [
    {
      type: "textfield",
      key: "firstName",
      label: "First name",
      input: true,
      validate: { required: true },
    },
    {
      type: "textfield",
      key: "lastName",
      label: "Last name",
      input: true,
      validate: { required: true },
    },
    {
      type: "select",
      key: "relationship",
      label: "Relationship status",
      input: true,
      widget: "choicesjs",
      data: {
        values: [
          { value: "single", label: "Single" },
          { value: "couple", label: "Married / in a relationship" },
        ],
      },
      validate: { required: true },
    },
    {
      type: "textfield",
      key: "spouseName",
      label: "Spouse name",
      input: true,
      conditional: { show: true, when: "relationship", eq: "couple" },
    },
    {
      type: "number",
      key: "monthlyIncome",
      label: "Monthly income ($)",
      input: true,
      validate: { required: true, min: 0 },
    },
    {
      type: "checkbox",
      key: "declaration",
      label: "I declare the information provided is true and complete",
      input: true,
      validate: { required: true },
    },
    {
      type: "button",
      key: "submit",
      action: "submit",
      label: "Submit",
      input: true,
    },
  ],
};

// v2 adds a "Contact email" field and rewords the income label. Seeded as a
// separate entry; v1 stays as-is so old submissions keep rendering with it.
const testFormSpecV2: Json = {
  display: "form",
  components: [
    {
      type: "textfield",
      key: "firstName",
      label: "First name",
      input: true,
      validate: { required: true },
    },
    {
      type: "textfield",
      key: "lastName",
      label: "Last name",
      input: true,
      validate: { required: true },
    },
    {
      type: "select",
      key: "relationship",
      label: "Relationship status",
      input: true,
      widget: "choicesjs",
      data: {
        values: [
          { value: "single", label: "Single" },
          { value: "couple", label: "Married / in a relationship" },
        ],
      },
      validate: { required: true },
    },
    {
      type: "textfield",
      key: "spouseName",
      label: "Spouse name",
      input: true,
      conditional: { show: true, when: "relationship", eq: "couple" },
    },
    {
      type: "email",
      key: "contactEmail",
      label: "Contact email (new in v2)",
      input: true,
    },
    {
      type: "number",
      key: "monthlyIncome",
      label: "Total monthly income ($) (reworded in v2)",
      input: true,
      validate: { required: true, min: 0 },
    },
    {
      type: "checkbox",
      key: "declaration",
      label: "I declare the information provided is true and complete",
      input: true,
      validate: { required: true },
    },
    {
      type: "button",
      key: "submit",
      action: "submit",
      label: "Submit",
      input: true,
    },
  ],
};

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

// Creates any missing seeded versions, one entry per version. Existing
// entries are left untouched.
async function seedTestForm(strapi: Core.Strapi) {
  const versions: Array<{ version: number; spec: Json }> = [
    { version: 1, spec: testFormSpecV1 },
    { version: 2, spec: testFormSpecV2 },
  ];

  for (const { version, spec } of versions) {
    const existing = await strapi.documents(FORM_SPEC_UID).findFirst({
      filters: { formSpecId: "poc-test-form", version },
    });
    if (existing) continue;

    await strapi.documents(FORM_SPEC_UID).create({
      data: {
        formSpecId: "poc-test-form",
        version,
        title: "POC test form",
        spec,
      },
      status: "published",
    });
    strapi.log.info(`Seeded form-spec poc-test-form v${version}`);
  }
}

export default {
  register(/* { strapi }: { strapi: Core.Strapi } */) {},

  async bootstrap({ strapi }: { strapi: Core.Strapi }) {
    await revokePublicRead(strapi);
    await seedTestForm(strapi);
  },
};
