// The text of a number field drawn as a text input: what the citizen may type
// into it, the number that text stands for, and how a stored number reads
// back. No React, no Form.io.

/**
 * The typed text, tidied, or null to refuse the keystroke. Grouping commas,
 * spaces and a "$" (pasted from elsewhere, or typed out of habit beside the
 * field's own "$") are dropped; what is left must be an optional minus sign,
 * digits, and at most `decimalLimit` digits after one decimal point. A text
 * that is still on its way to a number ("", "-", "12.") is allowed.
 */
export function cleanNumberText(
  text: string,
  decimalLimit: number,
): string | null {
  const stripped = text.replace(/[,\s$]/g, "");
  const pattern =
    decimalLimit > 0
      ? new RegExp(String.raw`^-?\d*(\.\d{0,${decimalLimit}})?$`)
      : /^-?\d*$/;
  return pattern.test(stripped) ? stripped : null;
}

/** The number a tidied text stands for; null while it holds no digit yet. */
export function parseNumberText(text: string): number | null {
  if (!/\d/.test(text)) return null;
  const value = Number(text);
  return Number.isFinite(value) ? value : null;
}

/**
 * A stored value as the field shows it when the citizen is not typing in it:
 * at most `decimalLimit` decimals, with grouping commas when the form asks for
 * them. Empty for no value. A numeric string (as Form.io may hold one) reads
 * as its number.
 */
export function formatNumberValue(
  value: unknown,
  decimalLimit: number,
  grouping: boolean,
): string {
  const number =
    typeof value === "string" && value.trim() !== "" ? Number(value) : value;
  if (typeof number !== "number" || !Number.isFinite(number)) return "";
  return number.toLocaleString("en-CA", {
    maximumFractionDigits: decimalLimit,
    useGrouping: grouping,
  });
}
