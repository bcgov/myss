import type {
  AssetColumn,
  IncomeColumn,
} from "@/api/eligibility";

// The rate-table columns, defined once so every view of the rate table iterates
// the same list. The letters match MyssApi's EligibilityRateColumns and
// MyssContent's eligibility-rate-rules.ts.

/** The heading an income-limit column is shown under. */
export type RateGroup =
  | "Income Assistance"
  | "Income Assistance 65+"
  | "Disability Assistance";

/** One income-limit column: its letter, its group, and the household it is for. */
export interface IncomeColumnInfo {
  readonly letter: IncomeColumn;
  readonly group: RateGroup;
  readonly household: "single" | "couple";
}

/** Income-limit columns A–I in display order; each group's columns are adjacent. */
export const INCOME_COLUMNS: readonly IncomeColumnInfo[] = [
  { letter: "a", group: "Income Assistance", household: "couple" },
  { letter: "b", group: "Income Assistance", household: "single" },
  { letter: "c", group: "Income Assistance 65+", household: "couple" },
  { letter: "d", group: "Income Assistance 65+", household: "couple" },
  { letter: "e", group: "Income Assistance 65+", household: "single" },
  { letter: "f", group: "Disability Assistance", household: "couple" },
  { letter: "g", group: "Disability Assistance", household: "single" },
  { letter: "h", group: "Disability Assistance", household: "couple" },
  { letter: "i", group: "Disability Assistance", household: "couple" },
];

/** Columns for two-adult households, which have no limit at family size 1 (stored as 0). */
export const COUPLE_COLUMNS: readonly IncomeColumn[] = INCOME_COLUMNS.filter(
  (column) => column.household === "couple",
).map((column) => column.letter);

/** Asset-limit columns A–D, one per asset category. */
export const ASSET_COLUMNS: readonly AssetColumn[] = ["a", "b", "c", "d"];

/** A group heading and the number of adjacent income columns it spans. */
export interface IncomeColumnGroup {
  readonly group: RateGroup;
  readonly span: number;
}

function groupColumns(columns: readonly IncomeColumnInfo[]): IncomeColumnGroup[] {
  const groups: { group: RateGroup; span: number }[] = [];
  for (const column of columns) {
    const last = groups.at(-1);
    if (last?.group === column.group) {
      last.span += 1;
    } else {
      groups.push({ group: column.group, span: 1 });
    }
  }
  return groups;
}

/** The income-column groups in display order. */
export const INCOME_COLUMN_GROUPS: readonly IncomeColumnGroup[] = groupColumns(INCOME_COLUMNS);

/** Whether a cell has no limit: a couple column at family size 1. */
export function isNotApplicable(familySize: number, letter: IncomeColumn): boolean {
  return familySize === 1 && COUPLE_COLUMNS.includes(letter);
}
