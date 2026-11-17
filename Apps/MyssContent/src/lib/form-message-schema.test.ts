import { readFileSync } from "node:fs";
import { resolve } from "node:path";

import { describe, expect, it } from "vitest";

/**
 * The schema's presence and length checks back up the message rules applied by
 * the admin API and the admin site, so its limits must match theirs.
 */
const SCHEMA_FILE = resolve(
  __dirname,
  "../api/form-message/content-types/form-message/schema.json",
);

interface Attribute {
  readonly type: string;
  readonly required?: boolean;
  readonly maxLength?: number;
  readonly regex?: string;
}

interface Schema {
  readonly options: { readonly draftAndPublish?: boolean };
  readonly attributes: Readonly<Record<string, Attribute>>;
}

const schema = JSON.parse(readFileSync(SCHEMA_FILE, "utf8")) as Schema;

describe("form message schema", () => {
  it("has draft and publish on, so readers can ask for published entries only", () => {
    expect(schema.options.draftAndPublish).toBe(true);
  });

  it("has exactly the six fields, each required", () => {
    expect(Object.keys(schema.attributes).sort()).toEqual([
      "body",
      "changedBy",
      "formSpecId",
      "heading",
      "messageKey",
      "title",
    ]);
    for (const attribute of Object.values(schema.attributes)) {
      expect(attribute.required).toBe(true);
    }
  });

  it.each([
    { field: "messageKey", type: "string", maxLength: 50 },
    { field: "heading", type: "string", maxLength: 100 },
    { field: "title", type: "string", maxLength: 100 },
    { field: "body", type: "text", maxLength: 1000 },
    { field: "changedBy", type: "string", maxLength: 100 },
  ])("limits $field to $maxLength characters", ({ field, type, maxLength }) => {
    expect(schema.attributes[field]).toMatchObject({ type, maxLength });
  });

  it.each(["formSpecId", "messageKey"])(
    "accepts a %s of lowercase letters, digits and hyphens only",
    (field) => {
      expect(schema.attributes[field]).toMatchObject({
        type: "string",
        regex: "^[a-z0-9-]+$",
      });
    },
  );
});
