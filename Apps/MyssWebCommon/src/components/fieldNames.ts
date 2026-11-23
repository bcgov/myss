// Form.io names an input after where its answer goes: `data[firstName]` for a
// field on the form, `data[children][1][childFirstName]` for a field in the
// second row of a repeat section. The API names a failure by the same place in
// its own notation: `firstName`, `children[1].childFirstName`.

/**
 * The API's field name for a Form.io input name, or undefined for another name.
 * Digits are a row's position only straight after a key, so a key made of
 * digits stays a key: `data[2024]` is `2024`, `data[children][0][5]` is
 * `children[0].5`.
 */
export function fieldOfInputName(name: string): string | undefined {
  const match = /^data((?:\[[^\]]+\])+)$/.exec(name);
  if (!match) return undefined;
  let field = "";
  let afterKey = false;
  for (const [, part] of match[1].matchAll(/\[([^\]]+)\]/g)) {
    if (afterKey && /^\d+$/.test(part)) {
      field += `[${part}]`;
      afterKey = false;
    } else {
      field = field ? `${field}.${part}` : part;
      afterKey = true;
    }
  }
  return field;
}

/** The Form.io input name for one of the API's field names. */
export function inputNameOfField(field: string): string {
  return (
    "data" +
    field
      .split(/\.|(?=\[)/)
      .map((part) => (part.startsWith("[") ? part : `[${part}]`))
      .join("")
  );
}
