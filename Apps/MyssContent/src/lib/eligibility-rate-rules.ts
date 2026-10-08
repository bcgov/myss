/**
 * Validation rules for eligibility-rate entries. Pure functions: the lifecycle
 * hook loads the rows that share the incoming effective date and turns any
 * violation into an `ApplicationError`.
 *
 * `incomeRows` and `assetLimits` arrive as JSON strings from the admin panel
 * and as objects from the Document Service and REST; both are handled.
 */

/** Stable keywords, so messages can later be sourced from content and translated. */
export const RateRule = {
  JsonUnparseable: "RATES.JSON.UNPARSEABLE",
  ShapeInvalid: "RATES.SHAPE.INVALID",
  FamilySizesInvalid: "RATES.FAMILY_SIZES.INVALID",
  ColumnMissing: "RATES.COLUMN.MISSING",
  ColumnUnknown: "RATES.COLUMN.UNKNOWN",
  NotApplicableNotZero: "RATES.NOT_APPLICABLE.NOT_ZERO",
  AmountNotANumber: "RATES.AMOUNT.NOT_A_NUMBER",
  AmountNegative: "RATES.AMOUNT.NEGATIVE",
  AmountTooPrecise: "RATES.AMOUNT.TOO_PRECISE",
  EffectiveDateDuplicate: "RATES.EFFECTIVE_DATE.DUPLICATE",
} as const;

export type RateRuleKeyword = (typeof RateRule)[keyof typeof RateRule];

/** A rule violation: a stable keyword plus a message an admin can act on. */
export interface Violation {
  readonly keyword: RateRuleKeyword;
  readonly message: string;
}

/** Income-limit columns, one per client type. */
export const INCOME_COLUMNS: readonly string[] = ["a", "b", "c", "d", "e", "f", "g", "h", "i"];

/** Columns for two-adult households, which have no limit at family size 1. */
export const COUPLE_COLUMNS: readonly string[] = ["a", "c", "d", "f", "h", "i"];

/** Asset-limit columns, one per asset category. */
export const ASSET_COLUMNS: readonly string[] = ["a", "b", "c", "d"];

/** The family sizes a table covers; 7 also serves larger households. */
export const FAMILY_SIZES: readonly number[] = [1, 2, 3, 4, 5, 6, 7];

/** A stored row that shares the incoming effective date. Draft rows have `publishedAt: null`. */
export interface RateTableRow {
  readonly id?: number;
  readonly documentId?: string | null;
  readonly publishedAt?: string | Date | null;
}

/** The `params.data` of a create or update event. */
export interface IncomingRateTable {
  readonly documentId?: string | null;
  readonly effectiveDate?: unknown;
  readonly incomeRows?: unknown;
  readonly assetLimits?: unknown;
}

function violation(keyword: RateRuleKeyword, message: string): Violation {
  return { keyword, message };
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function hasOwn(record: Record<string, unknown>, key: string): boolean {
  return Object.prototype.hasOwnProperty.call(record, key);
}

function describeValue(value: unknown): string {
  if (typeof value === "string") return `the text ${JSON.stringify(value)}`;
  if (typeof value === "number") return String(value);
  return JSON.stringify(value) ?? String(value);
}

// Exact for amounts in cents: dividing an integer by 100 rounds to the nearest
// double, so only a value with at most 2 decimals survives the round trip.
function hasAtMostTwoDecimals(value: number): boolean {
  return Math.round(value * 100) / 100 === value;
}

function parseJsonField(
  value: unknown,
  label: string,
): { ok: true; value: unknown } | { ok: false; violation: Violation } {
  if (typeof value !== "string") return { ok: true, value };
  try {
    return { ok: true, value: JSON.parse(value) };
  } catch (error) {
    const detail = error instanceof Error ? error.message : String(error);
    return {
      ok: false,
      violation: violation(RateRule.JsonUnparseable, `${label} is not valid JSON: ${detail}`),
    };
  }
}

/** The problem with one amount, or undefined when it is a valid amount. */
function amountProblem(value: unknown, place: string): Violation | undefined {
  if (typeof value !== "number" || !Number.isFinite(value)) {
    return violation(
      RateRule.AmountNotANumber,
      `${place} must be a number, not ${describeValue(value)}.`,
    );
  }
  if (value < 0) {
    return violation(RateRule.AmountNegative, `${place} must be 0 or more, not ${value}.`);
  }
  if (!hasAtMostTwoDecimals(value)) {
    return violation(
      RateRule.AmountTooPrecise,
      `${place} must have at most 2 decimal places, not ${value}.`,
    );
  }
  return undefined;
}

function familySizeLabel(row: Record<string, unknown>, index: number): string {
  return typeof row.familySize === "number" ? `family size ${row.familySize}` : `row ${index + 1}`;
}

function capitalise(text: string): string {
  return text.charAt(0).toUpperCase() + text.slice(1);
}

function hasExactlyEachFamilySize(rows: readonly Record<string, unknown>[]): boolean {
  const sizes = rows.map((row) => row.familySize);
  return (
    sizes.length === FAMILY_SIZES.length &&
    FAMILY_SIZES.every((size) => sizes.filter((s) => s === size).length === 1)
  );
}

/** Adds `label` to the list kept for `key`. */
function collect(groups: Map<string, string[]>, key: string, label: string): void {
  const labels = groups.get(key) ?? [];
  labels.push(label);
  groups.set(key, labels);
}

/** The income-limit rules: shape, family sizes 1–7, the nine columns, and every amount. */
export function validateIncomeRows(value: unknown): Violation[] {
  const parsed = parseJsonField(value, "The income limits");
  if (!parsed.ok) return [parsed.violation];

  const rows = parsed.value;
  if (!Array.isArray(rows)) {
    return [
      violation(
        RateRule.ShapeInvalid,
        "The income limits must be a list of rows, one for each family size from 1 to 7.",
      ),
    ];
  }
  if (!rows.every(isRecord)) {
    return [violation(RateRule.ShapeInvalid, "Each income-limit row must be an object.")];
  }

  const violations: Violation[] = [];

  if (!hasExactlyEachFamilySize(rows)) {
    const found = rows.map((row) => describeValue(row.familySize)).join(", ");
    violations.push(
      violation(
        RateRule.FamilySizesInvalid,
        `The income limits need exactly one row for each family size from 1 to 7. Found: ${found || "none"}.`,
      ),
    );
  }

  const missing = new Map<string, string[]>();
  const unknown = new Map<string, string[]>();
  rows.forEach((row, index) => {
    const label = familySizeLabel(row, index);
    for (const column of INCOME_COLUMNS) {
      if (!hasOwn(row, column)) collect(missing, column, label);
    }
    for (const key of Object.keys(row)) {
      if (key !== "familySize" && !INCOME_COLUMNS.includes(key)) collect(unknown, key, label);
    }
  });
  for (const [column, labels] of missing) {
    violations.push(
      violation(
        RateRule.ColumnMissing,
        `Column ${column.toUpperCase()} is missing for ${labels.join(", ")}.`,
      ),
    );
  }
  for (const [key, labels] of unknown) {
    violations.push(
      violation(
        RateRule.ColumnUnknown,
        `Unknown column "${key}" for ${labels.join(", ")}. The columns are A to I.`,
      ),
    );
  }

  rows.forEach((row, index) => {
    const label = familySizeLabel(row, index);
    for (const column of INCOME_COLUMNS) {
      if (!hasOwn(row, column)) continue;
      const place = `${capitalise(label)}, column ${column.toUpperCase()}`;
      const problem = amountProblem(row[column], place);
      if (problem) {
        violations.push(problem);
      } else if (row.familySize === 1 && COUPLE_COLUMNS.includes(column) && row[column] !== 0) {
        violations.push(
          violation(
            RateRule.NotApplicableNotZero,
            `${place} must be 0: it is a couple column, and a couple is never a family of one.`,
          ),
        );
      }
    }
  });

  return violations;
}

/** The asset-limit rules: shape, the four columns, and every amount. */
export function validateAssetLimits(value: unknown): Violation[] {
  const parsed = parseJsonField(value, "The asset limits");
  if (!parsed.ok) return [parsed.violation];

  const limits = parsed.value;
  if (!isRecord(limits)) {
    return [
      violation(RateRule.ShapeInvalid, "The asset limits must be an object with columns A to D."),
    ];
  }

  const violations: Violation[] = [];
  for (const column of ASSET_COLUMNS) {
    if (!hasOwn(limits, column)) {
      violations.push(
        violation(RateRule.ColumnMissing, `Asset limit column ${column.toUpperCase()} is missing.`),
      );
    }
  }
  for (const key of Object.keys(limits)) {
    if (!ASSET_COLUMNS.includes(key)) {
      violations.push(
        violation(
          RateRule.ColumnUnknown,
          `Unknown asset limit column "${key}". The columns are A to D.`,
        ),
      );
    }
  }
  for (const column of ASSET_COLUMNS) {
    if (!hasOwn(limits, column)) continue;
    const problem = amountProblem(limits[column], `Asset limit ${column.toUpperCase()}`);
    if (problem) violations.push(problem);
  }

  return violations;
}

/**
 * Another document with the same effective date. Rows sharing the incoming
 * `documentId` are the same table (its draft, or the published copy a
 * publish creates) and are not duplicates.
 */
export function findDuplicateDate(
  data: IncomingRateTable,
  sameDateRows: readonly RateTableRow[],
): Violation[] {
  if (typeof data.effectiveDate !== "string" || data.effectiveDate === "") return [];
  const otherDocument = sameDateRows.some((row) => row.documentId !== data.documentId);
  if (!otherDocument) return [];
  return [
    violation(
      RateRule.EffectiveDateDuplicate,
      `A rate table dated ${data.effectiveDate} already exists. Edit that table, or use a different effective date.`,
    ),
  ];
}

function validateValues(data: IncomingRateTable): Violation[] {
  const violations: Violation[] = [];
  if (data.incomeRows !== undefined) violations.push(...validateIncomeRows(data.incomeRows));
  if (data.assetLimits !== undefined) violations.push(...validateAssetLimits(data.assetLimits));
  return violations;
}

/**
 * Everything the hook enforces on create. Only the fields present are
 * checked, so a status-only publish passes.
 *
 * `sameDateRows` must be every stored row with the incoming effective date,
 * drafts included.
 */
export function evaluateRateCreate(
  data: IncomingRateTable,
  sameDateRows: readonly RateTableRow[],
): Violation[] {
  return [...validateValues(data), ...findDuplicateDate(data, sameDateRows)];
}

/**
 * Everything the hook enforces on update. Without a `documentId` the update
 * cannot tell its own rows from another table's, so the duplicate-date check
 * is skipped rather than refusing a valid edit.
 */
export function evaluateRateUpdate(
  data: IncomingRateTable,
  sameDateRows: readonly RateTableRow[],
): Violation[] {
  const duplicates = data.documentId ? findDuplicateDate(data, sameDateRows) : [];
  return [...validateValues(data), ...duplicates];
}

/** How many messages an admin-panel error lists before summarising the rest. */
export const MAX_LISTED_VIOLATIONS = 10;

/** The messages as one admin-panel error, listing at most `MAX_LISTED_VIOLATIONS`. */
export function formatViolations(violations: readonly Violation[]): string {
  const listed = violations.slice(0, MAX_LISTED_VIOLATIONS).map((v) => v.message);
  const rest = violations.length - listed.length;
  if (rest > 0) listed.push(`…and ${rest} more ${rest === 1 ? "problem" : "problems"}.`);
  return listed.join(" ");
}
