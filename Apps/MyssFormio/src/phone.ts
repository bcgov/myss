// Phone numbers on Account Info and forms. The Ministry format is
// (area code) ###-####: the API stores the ten digits and the punctuation is
// presentation. The check here mirrors Myss.FormSpec.Domain.PhoneNumber so a
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

/** Recognize phone fields in current and older published Form.io specs. */
export function isPhoneField(component: Record<string, unknown>): boolean {
  const properties = component.properties as Record<string, unknown> | undefined;
  if (typeof properties?.myssValidator === "string") {
    return properties.myssValidator === "phone";
  }
  return (
    component.type === "phoneNumber" ||
    (component.type === "textfield" &&
      (component.inputMask === "(999) 999-9999" ||
        component.inputMask === "999-999-9999"))
  );
}

/** The label shown for a phone type, e.g. "Home Phone". */
export function phoneTypeLabel(type: PhoneType): string {
  return PHONE_TYPES.find((option) => option.type === type)?.label ?? type;
}

/**
 * The ten digits of a number matching the legacy format after whitespace is stripped, or null.
 */
export function phoneDigits(input: string): string | null {
  const normalized = input.replace(/\s/g, "");
  return PHONE_PATTERN.test(normalized) ? normalized.replace(/\D/g, "") : null;
}

/** The most digits a phone field takes: area code, exchange and line. */
export const PHONE_MAX_DIGITS = 10;

/**
 * Format the digits entered so far. Digits past the tenth are dropped; any
 * other character is kept, unformatted, so the phone rule reports it.
 */
export function formatPhoneInput(input: string): string {
  const compact = input.replace(/\s/g, "");
  if (!/^[\d().-]*$/.test(compact)) return compact;
  const digits = compact.replace(/\D/g, "").slice(0, PHONE_MAX_DIGITS);
  if (digits.length === 0) return "";
  if (digits.length < 3) return `(${digits}`;
  if (digits.length <= 6) return `(${digits.slice(0, 3)}) ${digits.slice(3)}`.trimEnd();
  return `(${digits.slice(0, 3)}) ${digits.slice(3, 6)}-${digits.slice(6)}`;
}

/** Ten stored digits as the citizen sees them: (250) 555-0123. */
export function formatPhone(digits: string): string {
  if (!/^\d{10}$/.test(digits)) return digits;
  return `(${digits.slice(0, 3)}) ${digits.slice(3, 6)}-${digits.slice(6)}`;
}
