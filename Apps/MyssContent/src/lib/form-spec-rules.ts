/**
 * Phase 0.3 — publish-time validation rules for form specs.
 *
 * Every function here is pure: it takes the incoming data and the rows that
 * already exist for that `formSpecId`, and returns violation messages. No
 * Strapi, no database, no I/O. The lifecycle hook is a thin wrapper that does
 * one query, calls `evaluateCreate` / `evaluateUpdate`, and turns any returned
 * message into an `ApplicationError` so the admin panel displays it.
 *
 * The behaviour encoded here was MEASURED against a running Strapi 5.51 on
 * 2026-08-17, not inferred from documentation. Three observations drive the
 * design, and breaking any of them breaks ordinary authoring:
 *
 *   1. Publishing is a CREATE, not an update. One publish fires
 *      `beforeUpdate` on the draft row, then `beforeCreate` for a second row
 *      carrying the same `documentId` and a `publishedAt` timestamp.
 *      => The version-sequence rule must run ONLY when `documentId` is null,
 *         or publishing a valid entry gets rejected because the draft already
 *         holds that version. `strapi.documents().create({status:"published"})`
 *         — which our own seeder calls — fires `beforeCreate` twice for the
 *         same reason, so getting this wrong fails the boot on a fresh DB.
 *
 *   2. Editing a published entry updates the DRAFT row, never the published
 *      one. There is no "update to a published row" event to catch.
 *      => Immutability is enforced on draft update, by comparing against the
 *         published sibling that still exists at that moment.
 *
 *   3. Republishing DELETES the old published row before creating the new one,
 *      so at publish-create time there is nothing left to compare against.
 *      => Confirms immutability cannot be enforced at publish time either.
 *
 * `spec` arrives as a STRING from the admin panel and as an OBJECT from the
 * Document Service (the seeder). Both are handled.
 */

/** Stable keywords, so messages can later be sourced from content and translated. */
export const FormSpecRule = {
  SpecUnparseable: "FORMSPEC.SPEC.UNPARSEABLE",
  SpecNotAnObject: "FORMSPEC.SPEC.NOT_AN_OBJECT",
  ComponentsMissing: "FORMSPEC.COMPONENTS.MISSING",
  ComponentsEmpty: "FORMSPEC.COMPONENTS.EMPTY",
  ComponentKeyMissing: "FORMSPEC.COMPONENT_KEY.MISSING",
  ComponentKeyDuplicate: "FORMSPEC.COMPONENT_KEY.DUPLICATE",
  ConditionalUnknownField: "FORMSPEC.CONDITIONAL.UNKNOWN_FIELD",
  VersionNotNext: "FORMSPEC.VERSION.NOT_NEXT",
  PublishedImmutable: "FORMSPEC.PUBLISHED.IMMUTABLE",
} as const;

export type FormSpecRuleKeyword = (typeof FormSpecRule)[keyof typeof FormSpecRule];

/** A rule violation: a stable keyword plus a message an author can act on. */
export interface Violation {
  readonly keyword: FormSpecRuleKeyword;
  readonly message: string;
}

/** A row as stored by Strapi. Draft rows have `publishedAt: null`. */
export interface FormSpecRow {
  readonly id?: number;
  readonly documentId?: string | null;
  readonly formSpecId?: string | null;
  readonly version?: number | null;
  readonly publishedAt?: string | Date | null;
  readonly spec?: unknown;
  readonly title?: string | null;
}

/** The `params.data` of a create or update event. */
export interface IncomingFormSpec {
  readonly documentId?: string | null;
  readonly formSpecId?: string | null;
  readonly version?: number | null;
  readonly publishedAt?: string | Date | null;
  readonly spec?: unknown;
  readonly title?: string | null;
}

function violation(keyword: FormSpecRuleKeyword, message: string): Violation {
  return { keyword, message };
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

/**
 * Normalises the two shapes `spec` arrives in. A string is parsed; an object
 * is passed through. Anything else, or invalid JSON, is a parse failure.
 */
export function parseSpec(
  spec: unknown,
): { ok: true; value: Record<string, unknown> } | { ok: false; violation: Violation } {
  let candidate: unknown = spec;

  if (typeof spec === "string") {
    try {
      candidate = JSON.parse(spec);
    } catch (error) {
      const detail = error instanceof Error ? error.message : String(error);
      return {
        ok: false,
        violation: violation(
          FormSpecRule.SpecUnparseable,
          `The form spec is not valid JSON: ${detail}`,
        ),
      };
    }
  }

  if (!isRecord(candidate)) {
    return {
      ok: false,
      violation: violation(
        FormSpecRule.SpecNotAnObject,
        "The form spec must be a JSON object with a `components` array.",
      ),
    };
  }

  return { ok: true, value: candidate };
}

interface ComponentLike {
  readonly key?: unknown;
  readonly conditional?: unknown;
}

function collectComponents(spec: Record<string, unknown>): ComponentLike[] {
  const out: ComponentLike[] = [];

  // Form.io nests components inside panels, columns, fieldsets and wizard
  // pages. A rule that only walks the top level would miss most real forms.
  const walk = (nodes: unknown) => {
    if (!Array.isArray(nodes)) return;
    for (const node of nodes) {
      if (!isRecord(node)) continue;
      out.push(node as ComponentLike);
      walk(node.components);
      if (Array.isArray(node.columns)) {
        for (const column of node.columns) {
          if (isRecord(column)) walk(column.components);
        }
      }
      if (Array.isArray(node.rows)) {
        for (const row of node.rows) {
          if (Array.isArray(row)) {
            for (const cell of row) {
              if (isRecord(cell)) walk(cell.components);
            }
          }
        }
      }
    }
  };

  walk(spec.components);
  return out;
}

/** The `when` field of a conditional, when it names another component. */
function conditionalTarget(component: ComponentLike): string | undefined {
  if (!isRecord(component.conditional)) return undefined;
  const when = component.conditional.when;
  return typeof when === "string" && when.length > 0 ? when : undefined;
}

/**
 * Rules (a), (b) and (c) of §8.2: parse as Form.io JSON, reject duplicate or
 * malformed field keys, reject conditionals referencing fields that do not
 * exist.
 */
export function validateFormSpec(spec: unknown): Violation[] {
  const parsed = parseSpec(spec);
  if (!parsed.ok) return [parsed.violation];

  if (!Array.isArray(parsed.value.components)) {
    return [
      violation(
        FormSpecRule.ComponentsMissing,
        "The form spec must have a `components` array.",
      ),
    ];
  }

  const components = collectComponents(parsed.value);
  if (components.length === 0) {
    return [
      violation(FormSpecRule.ComponentsEmpty, "The form spec has no components."),
    ];
  }

  const violations: Violation[] = [];
  const seen = new Set<string>();
  const duplicates = new Set<string>();

  for (const component of components) {
    const key = component.key;
    if (typeof key !== "string" || key.trim() === "") {
      violations.push(
        violation(
          FormSpecRule.ComponentKeyMissing,
          "Every component needs a non-empty `key`.",
        ),
      );
      continue;
    }
    if (seen.has(key)) duplicates.add(key);
    seen.add(key);
  }

  for (const key of [...duplicates].sort()) {
    violations.push(
      violation(
        FormSpecRule.ComponentKeyDuplicate,
        `Duplicate component key "${key}". Keys must be unique across the whole form.`,
      ),
    );
  }

  const unknownTargets = new Set<string>();
  for (const component of components) {
    const target = conditionalTarget(component);
    if (target !== undefined && !seen.has(target)) unknownTargets.add(target);
  }

  for (const target of [...unknownTargets].sort()) {
    violations.push(
      violation(
        FormSpecRule.ConditionalUnknownField,
        `A conditional refers to "${target}", which is not a field in this form.`,
      ),
    );
  }

  return violations;
}

/**
 * True when this create is a genuinely new document rather than Strapi
 * materialising a published row for a document that already exists.
 *
 * MEASURED: a real create has `documentId === null` in `params.data`; the
 * publish-create carries the existing `documentId`.
 */
export function isNewDocument(data: IncomingFormSpec): boolean {
  return data.documentId === null || data.documentId === undefined;
}

/** Rule (d): the version must be exactly `max(version) + 1` for this form. */
export function assertVersionSequence(
  data: IncomingFormSpec,
  siblings: readonly FormSpecRow[],
): Violation[] {
  const version = data.version;
  if (typeof version !== "number" || !Number.isInteger(version) || version < 1) {
    return [
      violation(
        FormSpecRule.VersionNotNext,
        "Version must be a whole number of 1 or more.",
      ),
    ];
  }

  const versions = siblings
    .map((row) => row.version)
    .filter((value): value is number => typeof value === "number");

  // Draft and published rows of the same version both appear here; the max is
  // what matters, so duplicates are harmless.
  const expected = versions.length === 0 ? 1 : Math.max(...versions) + 1;

  if (version !== expected) {
    return [
      violation(
        FormSpecRule.VersionNotNext,
        `Version must be ${expected} for "${data.formSpecId}" — versions are sequential and ` +
          `${versions.length === 0 ? "this is the first" : `the highest so far is ${Math.max(...versions)}`}. ` +
          `Received ${version}.`,
      ),
    ];
  }

  return [];
}

function isPublished(row: FormSpecRow): boolean {
  return row.publishedAt !== null && row.publishedAt !== undefined;
}

/** The published row of a document, if one exists. */
export function findPublishedSibling(
  documentId: string | null | undefined,
  siblings: readonly FormSpecRow[],
): FormSpecRow | undefined {
  if (!documentId) return undefined;
  return siblings.find((row) => row.documentId === documentId && isPublished(row));
}

/** Key-order-insensitive comparison, so a reserialised spec is not a "change". */
export function canonicalJson(value: unknown): string {
  const normalise = (input: unknown): unknown => {
    if (Array.isArray(input)) return input.map(normalise);
    if (isRecord(input)) {
      const out: Record<string, unknown> = {};
      for (const key of Object.keys(input).sort()) out[key] = normalise(input[key]);
      return out;
    }
    return input;
  };

  const parsed = parseSpec(value);
  return JSON.stringify(normalise(parsed.ok ? parsed.value : value));
}

/**
 * Rule (e): a published version is immutable. `title` is deliberately NOT
 * immutable — it is a label in the admin listing, not part of the contract a
 * submission was rendered against. `spec`, `version` and `formSpecId` are.
 */
export function checkImmutability(
  data: IncomingFormSpec,
  published: FormSpecRow,
): Violation[] {
  const violations: Violation[] = [];
  const nextVersion = (published.version ?? 0) + 1;

  if (data.spec !== undefined && canonicalJson(data.spec) !== canonicalJson(published.spec)) {
    violations.push(
      violation(
        FormSpecRule.PublishedImmutable,
        `Version ${published.version} of "${published.formSpecId}" is published and cannot be ` +
          `changed — submissions were rendered against it. Create version ${nextVersion} instead.`,
      ),
    );
  }

  if (data.version !== undefined && data.version !== published.version) {
    violations.push(
      violation(
        FormSpecRule.PublishedImmutable,
        `The version of a published entry cannot be changed. Create version ${nextVersion} as a ` +
          `new entry instead.`,
      ),
    );
  }

  if (data.formSpecId !== undefined && data.formSpecId !== published.formSpecId) {
    violations.push(
      violation(
        FormSpecRule.PublishedImmutable,
        "The form id of a published entry cannot be changed.",
      ),
    );
  }

  return violations;
}

/**
 * Everything the hook enforces on create.
 *
 * `siblings` must be every row sharing this `formSpecId`, drafts included.
 */
export function evaluateCreate(
  data: IncomingFormSpec,
  siblings: readonly FormSpecRow[],
): Violation[] {
  const violations: Violation[] = [];

  if (data.spec !== undefined) violations.push(...validateFormSpec(data.spec));

  // Skipped for the publish-create: the draft row already holds this version,
  // so a sequence check here would reject a valid publish.
  if (isNewDocument(data)) violations.push(...assertVersionSequence(data, siblings));

  return violations;
}

/** Everything the hook enforces on update. */
export function evaluateUpdate(
  data: IncomingFormSpec,
  siblings: readonly FormSpecRow[],
): Violation[] {
  const violations: Violation[] = [];

  if (data.spec !== undefined) violations.push(...validateFormSpec(data.spec));

  const published = findPublishedSibling(data.documentId, siblings);
  if (published) violations.push(...checkImmutability(data, published));

  return violations;
}

/** One line per violation, for an admin-panel error message. */
export function formatViolations(violations: readonly Violation[]): string {
  return violations.map((v) => v.message).join(" ");
}
