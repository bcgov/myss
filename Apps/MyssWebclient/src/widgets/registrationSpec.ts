import type { CurrentUser } from "@/auth/currentUser";
import { formatPhone as displayPhone, phoneDigits } from "@/lib/phone";

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
