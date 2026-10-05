import type { CurrentUser } from "@/auth/currentUser";

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

/** "+1 (250) 555-0100" → "250-555-0100"; undefined unless ten NANP digits. */
export function formatPhone(raw: string): string | undefined {
  let digits = raw.replace(/\D/g, "");
  if (digits.length === 11 && digits.startsWith("1")) digits = digits.slice(1);
  if (digits.length !== 10) return undefined;
  return `${digits.slice(0, 3)}-${digits.slice(3, 6)}-${digits.slice(6)}`;
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
): string | undefined {
  switch (attribute) {
    case "givenName":
      return identity.givenName;
    case "familyName":
      return identity.familyName;
    case "email":
      return identity.email;
    case "phoneNumber":
      return identity.phoneNumber && formatPhone(identity.phoneNumber);
    case "birthdate":
      // OIDC's birthdate is YYYY-MM-DD; anything else (a year only, say) is
      // not a date the picker can show. Given as local midnight: a bare date
      // is read as UTC midnight, which the picker shows as the day before
      // anywhere west of Greenwich (1990-01-01 became 1989-12-31 in Pacific).
      return identity.birthdate &&
        /^\d{4}-\d{2}-\d{2}$/.test(identity.birthdate)
        ? `${identity.birthdate}T00:00:00`
        : undefined;
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
): Component {
  const prepared: Component = { ...component };

  if (Array.isArray(component.components)) {
    prepared.components = prepareComponents(component.components, identity);
  }

  const properties = isRecord(component.properties)
    ? component.properties
    : undefined;
  const attribute = properties?.myssPrefill;
  if (typeof attribute === "string") {
    const value = prefillValue(component, attribute, identity);
    if (value !== undefined) {
      prepared.defaultValue = value;
      if (properties?.myssPrefillLock === "true") prepared.disabled = true;
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
): unknown[] {
  return components
    .filter((component) => !(isRecord(component) && isSubmitButton(component)))
    .map((component) =>
      isRecord(component) ? prepareComponent(component, identity) : component,
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
    components: prepareComponents(spec.components, identity),
  } as T;
}
