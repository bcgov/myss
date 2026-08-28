/**
 * Deep JSON equality that ignores object key order.
 *
 * The seed bootstrap compares a stored form spec / rate table against the seed
 * file to decide whether to re-publish it. Strapi stores these in JSON (jsonb)
 * columns that do NOT preserve object key order, so a plain
 * `JSON.stringify(a) === JSON.stringify(b)` would report a difference on every
 * boot even when nothing changed. Canonicalising keys first makes the check
 * reliable. Array order is preserved (it is significant for form components and
 * rate rows).
 */
export function canonicalize(value: unknown): unknown {
  if (Array.isArray(value)) return value.map(canonicalize);
  if (value !== null && typeof value === "object") {
    return Object.keys(value as Record<string, unknown>)
      .sort()
      .reduce<Record<string, unknown>>((acc, key) => {
        acc[key] = canonicalize((value as Record<string, unknown>)[key]);
        return acc;
      }, {});
  }
  return value;
}

export function jsonEqual(a: unknown, b: unknown): boolean {
  return JSON.stringify(canonicalize(a)) === JSON.stringify(canonicalize(b));
}
