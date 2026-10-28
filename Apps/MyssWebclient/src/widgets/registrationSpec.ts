import type { CurrentUser } from "@/auth/currentUser";
import { formatPhone as displayPhone, phoneDigits } from "@myss/formio/phone";

// Prepares the Strapi registration spec for rendering: fills fields from the
// sign-in identity and drops the spec's own submit button, because the widget
// renders BC Gov Design System buttons instead. Pure, so it is unit-tested
// without Form.io.
//
// A field opts in through its Form.io `properties` map:
//   myssPrefill: "givenName" | "familyName" | "email" | "phoneNumber"
//                | "birthdate" | "gender"
//   myssPrefillLock: "true"   — read-only, but ONLY when a value was filled.
// Locking only filled fields means a claim the identity provider did not
// release leaves an editable field, never a required one the citizen is
// stuck on. Locking is a UX guard, not a control: the API validates and
// stores whatever is submitted.
//
// Basic BCeID releases one full name, in given_name, and no family_name
// (MYSS-299). That is split into first and last name here, and the two are
// left editable even where the spec locks them, because the split is a guess.

export type IdentityDetails = Pick<
  CurrentUser,
  "givenName" | "familyName" | "email" | "phoneNumber" | "birthdate" | "gender"
>;

type Component = Record<string, unknown>;

// BC Services Card's gender claim values, mapped to the registration form's
// option values. "unknown" and anything unrecognised are left for the citizen.
const GENDER_OPTION: Readonly<Record<string, string>> = {
  male: "man",
  female: "woman",
  diverse: "nonBinary",
};

function isRecord(value: unknown): value is Component {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

/** The name fields as the citizen should see them first. */
interface Names {
  givenName?: string;
  familyName?: string;
  /** True when both came from splitting one full name, so may be wrong. */
  guessed: boolean;
}

/**
 * Splits a full name at its last space, when that is all the identity has:
 * "Ada King Lovelace" is first name "Ada King", last name "Lovelace". A name
 * that is one word, or an identity with a family name, is left as it is.
 */
export function namesFrom(identity: IdentityDetails): Names {
  const { givenName, familyName } = identity;
  const words = givenName?.trim().split(/\s+/) ?? [];
  if (familyName || words.length < 2) {
    return { givenName, familyName, guessed: false };
  }
  return {
    givenName: words.slice(0, -1).join(" "),
    familyName: words.at(-1),
    guessed: true,
  };
}

const NAME_ATTRIBUTES: ReadonlySet<string> = new Set([
  "givenName",
  "familyName",
]);

/** Formats a valid phone claim; an invalid one must stay editable. */
export function formatPhone(raw: string): string | undefined {
  const digits = phoneDigits(raw);
  return digits === null ? undefined : displayPhone(digits);
}

function optionValues(component: Component): string[] {
  const values = component.values;
  if (!Array.isArray(values)) return [];
  return values
    .map((option) => (isRecord(option) ? option.value : undefined))
    .filter((value): value is string => typeof value === "string");
}

/** The value to fill `component` with, already in the shape it expects. */
function prefillValue(
  component: Component,
  attribute: string,
  identity: IdentityDetails,
  names: Names,
): string | undefined {
  switch (attribute) {
    case "givenName":
      return names.givenName;
    case "familyName":
      return names.familyName;
    case "email":
      return identity.email;
    case "phoneNumber":
      return identity.phoneNumber && formatPhone(identity.phoneNumber);
    case "birthdate": {
      // OIDC's birthdate is YYYY-MM-DD; anything else (a year only, say) is
      // not a date the picker can show. The shape is not enough: 1990-02-31
      // fits it, the API would refuse it, and the field is locked, so the
      // citizen could not fix it. A real date survives the round trip;
      // JavaScript rolls an impossible one over (to 1990-03-03).
      const value = identity.birthdate;
      if (!value || !/^\d{4}-\d{2}-\d{2}$/.test(value)) return undefined;
      const parsed = new Date(`${value}T00:00:00Z`);
      if (
        Number.isNaN(parsed.getTime()) ||
        !parsed.toISOString().startsWith(`${value}T`)
      ) {
        return undefined;
      }
      // Given as local midnight: a bare date is read as UTC midnight, which
      // the picker shows as the day before anywhere west of Greenwich
      // (1990-01-01 became 1989-12-31 in Pacific).
      return `${value}T00:00:00`;
    }
    case "gender": {
      const option = identity.gender
        ? GENDER_OPTION[identity.gender.toLowerCase()]
        : undefined;
      return option && optionValues(component).includes(option)
        ? option
        : undefined;
    }
    default:
      return undefined;
  }
}

function prepareComponent(
  component: Component,
  identity: IdentityDetails,
  names: Names,
): Component {
  const prepared: Component = { ...component };

  if (Array.isArray(component.components)) {
    prepared.components = prepareComponents(
      component.components,
      identity,
      names,
    );
  }

  const properties = isRecord(component.properties)
    ? component.properties
    : undefined;
  const attribute = properties?.myssPrefill;
  if (typeof attribute === "string") {
    const value = prefillValue(component, attribute, identity, names);
    if (value !== undefined) {
      prepared.defaultValue = value;
      const guessed = names.guessed && NAME_ATTRIBUTES.has(attribute);
      if (properties?.myssPrefillLock === "true" && !guessed) {
        prepared.disabled = true;
      }
    }
  }

  return prepared;
}

function isSubmitButton(component: Component): boolean {
  return (
    component.type === "button" &&
    (component.action === undefined || component.action === "submit")
  );
}

function prepareComponents(
  components: unknown[],
  identity: IdentityDetails,
  names: Names,
): unknown[] {
  return components
    .filter((component) => !(isRecord(component) && isSubmitButton(component)))
    .map((component) =>
      isRecord(component)
        ? prepareComponent(component, identity, names)
        : component,
    );
}

/**
 * A copy of `spec` ready to render: prefilled from `identity`, prefilled
 * fields locked where the spec asks, and submit buttons removed. The input is
 * not mutated.
 */
export function prepareRegistrationSpec<T>(
  spec: T,
  identity: IdentityDetails,
): T {
  if (!isRecord(spec) || !Array.isArray(spec.components)) return spec;
  return {
    ...spec,
    components: prepareComponents(
      spec.components,
      identity,
      namesFrom(identity),
    ),
  } as T;
}

// ---- The three registration steps (MYSS-295) ----------------------------
//
// Step 1 renders the spec without its consent, Step 3 asks for the consent on
// its own and reviews the answers. The consent stays authored in the spec
// (its label carries the Terms of Use and Privacy Policy links); only where it
// is shown changes, so no new spec version is needed and the API still
// validates the whole form against the version the citizen saw.

/** The spec's consent and its heading: asked for in Step 3, not Step 1. */
export const CONSENT_KEYS: ReadonlySet<string> = new Set([
  "consentHeading",
  "consent",
]);

function keyOf(component: unknown): string | undefined {
  return isRecord(component) && typeof component.key === "string"
    ? component.key
    : undefined;
}

/**
 * Splits a (prepared) spec into Step 1's part, without the consent, and Step
 * 3's, the consent alone; `consent` is null for a spec that has none. Only top
 * level components move: that is where every version has the consent.
 */
export function splitRegistrationSpec<T>(spec: T): {
  info: T;
  consent: T | null;
} {
  if (!isRecord(spec) || !Array.isArray(spec.components)) {
    return { info: spec, consent: null };
  }
  const isConsent = (component: unknown) =>
    CONSENT_KEYS.has(keyOf(component) ?? "");
  const consent = spec.components.filter(isConsent);
  return {
    info: {
      ...spec,
      components: spec.components.filter((c) => !isConsent(c)),
    } as T,
    consent: consent.some((c) => keyOf(c) === "consent")
      ? ({ ...spec, components: consent } as T)
      : null,
  };
}

/**
 * Every component in document order, inside panels, columns and table rows
 * too. A local walk rather than @myss/formio's forEachComponent: that comes
 * from the package's main entry, which loads the Form.io components and CSS
 * this module's node unit tests cannot.
 */
function eachComponent(nodes: unknown, visit: (c: Component) => void): void {
  if (!Array.isArray(nodes)) return;
  for (const node of nodes) {
    if (!isRecord(node)) continue;
    visit(node);
    eachComponent(node.components, visit);
    if (Array.isArray(node.columns)) {
      for (const column of node.columns) {
        if (isRecord(column)) eachComponent(column.components, visit);
      }
    }
    if (Array.isArray(node.rows)) {
      for (const row of node.rows) {
        if (Array.isArray(row)) {
          for (const cell of row) {
            if (isRecord(cell)) eachComponent(cell.components, visit);
          }
        }
      }
    }
  }
}

/** One line of the Step 3 review table. */
export interface ReviewRow {
  key: string;
  label: string;
  value: string;
}

const LONG_DATE = new Intl.DateTimeFormat("en-CA", {
  year: "numeric",
  month: "long",
  day: "numeric",
  timeZone: "UTC",
});

function displayValue(component: Component, value: unknown): string {
  if (typeof value !== "string" && typeof value !== "number") return "";
  const text = String(value).trim();
  if (text === "") return "";

  const options = component.values;
  if (Array.isArray(options)) {
    const option = options.find(
      (candidate) => isRecord(candidate) && candidate.value === value,
    );
    if (isRecord(option) && typeof option.label === "string") {
      return option.label;
    }
  }

  const properties = isRecord(component.properties) ? component.properties : {};
  const rule = properties.myssValidator;
  if (component.type === "datetime" || rule === "date") {
    // The date part only, read as a calendar date: no time zone shifts it.
    const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(text);
    if (match) {
      const [, year, month, day] = match.map(Number);
      return LONG_DATE.format(new Date(Date.UTC(year, month - 1, day)));
    }
  }
  if (component.type === "phoneNumber" || rule === "phone") {
    const digits = phoneDigits(text);
    if (digits !== null) return displayPhone(digits);
  }
  if (rule === "sin" || component.key === "sin") {
    const digits = text.replace(/\D/g, "");
    if (digits.length === 9) {
      return `${digits.slice(0, 3)}-${digits.slice(3, 6)}-${digits.slice(6)}`;
    }
  }
  return text;
}

/**
 * The Step 3 review table: each answered input of the spec, in form order,
 * labelled as the form labels it and shown as the citizen would read it
 * (option labels, long dates, formatted phone and SIN). The consent is left
 * out: it is asked for beside the table.
 */
export function reviewRows(
  spec: unknown,
  answers: Readonly<Record<string, unknown>>,
): ReviewRow[] {
  const rows: ReviewRow[] = [];
  eachComponent(isRecord(spec) ? spec.components : undefined, (component) => {
    const key = keyOf(component);
    if (
      !key ||
      component.input !== true ||
      CONSENT_KEYS.has(key) ||
      component.type === "button"
    ) {
      return;
    }
    const value = displayValue(component, answers[key]);
    if (value === "") return;
    const label =
      typeof component.label === "string"
        ? component.label.replace(/<[^>]*>/g, "").trim()
        : key;
    rows.push({ key, label, value });
  });
  return rows;
}
