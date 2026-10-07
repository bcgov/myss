import type { AssetColumn, EligibilityRates, IncomeColumn } from "@/api/eligibility";
import type { SaveEstimatorRatesInput } from "@/api/eligibilityRatesAdmin";
import {
  ASSET_COLUMNS,
  FAMILY_SIZES,
  INCOME_COLUMNS,
  isNotApplicable,
} from "@/lib/rateColumns";

// Every decision the rates editor makes, as pure functions. A cell id is the
// API's field name for that cell, so a 422 maps straight onto it:
// incomeRows.{familySize}.{letter} or assetLimits.{letter}.

/** What the admin has typed, as text, keyed by cell id. N/A cells have no entry. */
export type RatesDraft = Readonly<Record<string, string>>;

export const BLANK_MESSAGE = "Enter an amount.";

export const INVALID_MESSAGE =
  "Enter a dollar amount using digits and up to 2 decimal places, for example 1535.50.";

export const TOO_LARGE_MESSAGE = "Enter an amount under 1,000,000,000.";

/** Up to 9 whole-dollar digits, optionally a point and one or two more digits; nothing else. */
const AMOUNT = /^\d{1,9}(\.\d{1,2})?$/;

/** Digits that are only wrong for being too many. */
const TOO_LARGE = /^\d{10,}(\.\d{1,2})?$/;

export function incomeCellId(familySize: number, letter: IncomeColumn): string {
  return `incomeRows.${familySize}.${letter}`;
}

export function assetCellId(letter: AssetColumn): string {
  return `assetLimits.${letter}`;
}

const EDITABLE_CELL_IDS: readonly string[] = [
  ...FAMILY_SIZES.flatMap((familySize) =>
    INCOME_COLUMNS.filter(({ letter }) => !isNotApplicable(familySize, letter)).map(({ letter }) =>
      incomeCellId(familySize, letter),
    ),
  ),
  ...ASSET_COLUMNS.map(assetCellId),
];

const EDITABLE_CELLS: ReadonlySet<string> = new Set(EDITABLE_CELL_IDS);

/** Every editable cell in table order: income row by row without the N/A cells, then the asset limits. */
export function editableCellIds(): readonly string[] {
  return EDITABLE_CELL_IDS;
}

/** Whether a field name is one of the editable cells. */
export function isEditableCell(id: string): boolean {
  return EDITABLE_CELLS.has(id);
}

/** Whether a table has exactly the rows 1–7, in order, so every editable cell is on screen. */
export function isCompleteTable(rates: EligibilityRates): boolean {
  return (
    rates.incomeRows.length === FAMILY_SIZES.length &&
    rates.incomeRows.every((row, index) => row.familySize === FAMILY_SIZES[index])
  );
}

/** The draft an edit starts from: every editable cell's current amount, as text. */
export function ratesDraft(rates: EligibilityRates): RatesDraft {
  const draft: Record<string, string> = {};
  for (const familySize of FAMILY_SIZES) {
    const row = rates.incomeRows.find((r) => r.familySize === familySize);
    for (const { letter } of INCOME_COLUMNS) {
      if (isNotApplicable(familySize, letter)) continue;
      draft[incomeCellId(familySize, letter)] = row ? String(row[letter]) : "";
    }
  }
  for (const letter of ASSET_COLUMNS) {
    draft[assetCellId(letter)] = String(rates.assetLimits[letter]);
  }
  return draft;
}

/**
 * The amount typed in a cell, or null when it is not one. Accepts only digits
 * with at most 2 decimal places, so "1e3", "12abc", "-5" and "1,060", which
 * Number() or parseFloat() would partly accept, are refused.
 */
export function parseAmount(text: string): number | null {
  const trimmed = text.trim();
  return AMOUNT.test(trimmed) ? Number(trimmed) : null;
}

/** The message for each cell that cannot be saved; empty when every cell is valid. */
export function validateDraft(draft: RatesDraft): Record<string, string> {
  const errors: Record<string, string> = {};
  for (const id of EDITABLE_CELL_IDS) {
    const text = (draft[id] ?? "").trim();
    if (text === "") {
      errors[id] = BLANK_MESSAGE;
    } else if (TOO_LARGE.test(text)) {
      errors[id] = TOO_LARGE_MESSAGE;
    } else if (parseAmount(text) === null) {
      errors[id] = INVALID_MESSAGE;
    }
  }
  return errors;
}

/** The short text shown under an invalid input; the error summary holds the full message. */
export function inlineMessage(message: string): string {
  if (message === BLANK_MESSAGE) return BLANK_MESSAGE;
  if (message === INVALID_MESSAGE) return "Use digits, up to 2 decimals.";
  if (message === TOO_LARGE_MESSAGE) return "Too large.";
  return "Check this amount.";
}

/** The complete table to save, with N/A cells as 0. Only for a draft that passed validateDraft. */
export function applyDraft(draft: RatesDraft): SaveEstimatorRatesInput {
  const amount = (id: string): number => {
    const value = parseAmount(draft[id] ?? "");
    if (value === null) throw new Error(`Cell ${id} does not hold a valid amount.`);
    return value;
  };

  return {
    incomeRows: FAMILY_SIZES.map((familySize) => {
      const row = { familySize, a: 0, b: 0, c: 0, d: 0, e: 0, f: 0, g: 0, h: 0, i: 0 };
      for (const { letter } of INCOME_COLUMNS) {
        if (!isNotApplicable(familySize, letter)) row[letter] = amount(incomeCellId(familySize, letter));
      }
      return row;
    }),
    assetLimits: {
      a: amount(assetCellId("a")),
      b: amount(assetCellId("b")),
      c: amount(assetCellId("c")),
      d: amount(assetCellId("d")),
    },
  };
}

/** Whether any cell's amount differs from the table the edit started from; "1060.00" is no change from 1060. */
export function hasChanges(base: EligibilityRates, draft: RatesDraft): boolean {
  const original = ratesDraft(base);
  return EDITABLE_CELL_IDS.some(
    (id) => parseAmount(draft[id] ?? "") !== parseAmount(original[id] ?? ""),
  );
}

/**
 * A cell's name for its input and the error summary, for example "Family size 3,
 * category C (Income Assistance 65+)". Any other field name is returned as is.
 */
export function cellLabel(id: string): string {
  const income = /^incomeRows\.(\d+)\.([a-i])$/.exec(id);
  if (income) {
    const column = INCOME_COLUMNS.find(({ letter }) => letter === income[2]);
    return `Family size ${income[1]}, category ${income[2].toUpperCase()} (${column?.group})`;
  }
  const asset = /^assetLimits\.([a-d])$/.exec(id);
  if (asset) return `Asset limit, category ${asset[1].toUpperCase()}`;
  return id;
}
