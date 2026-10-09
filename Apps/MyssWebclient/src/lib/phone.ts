// Phone numbers on Account Info (MYSS-271). The Ministry format is
// (area code) ###-####: the API stores the ten digits and the punctuation is
// presentation. The check here mirrors Myss.Api.Domain.PhoneNumber so a
// mistake is caught before the round trip; the API re-checks every number.

/** The kinds of number ICM keeps for a contact, one of each per person. */
export const PHONE_TYPES = [
  { type: "Home", label: "Home Phone" },
  { type: "Cell", label: "Cell Phone" },
  { type: "Work", label: "Work Phone" },
  { type: "Message", label: "Message Phone" },
] as const;

export type PhoneType = (typeof PHONE_TYPES)[number]["type"];

export const PHONE_FORMAT_MESSAGE = "Phone number is invalid";

const PHONE_PATTERN = /^\(?([2-9][0-9][0-9])\)?[\s.-]?([0-9]{3})[\s.-]?([0-9]{4})$/;

/** The label shown for a phone type, e.g. "Home Phone". */
export function phoneTypeLabel(type: PhoneType): string {
  return PHONE_TYPES.find((option) => option.type === type)?.label ?? type;
}

/**
 * The ten digits of a number matching the legacy MySS format, or null.
 */
export function phoneDigits(input: string): string | null {
  if (!PHONE_PATTERN.test(input)) return null;
  return input.replace(/\D/g, "");
}

/** Ten stored digits as the citizen sees them: (250) 555-0123. */
export function formatPhone(digits: string): string {
  if (!/^\d{10}$/.test(digits)) return digits;
  return `(${digits.slice(0, 3)}) ${digits.slice(3, 6)}-${digits.slice(6)}`;
}
