// Form.io names an input after where its answer goes: `data[firstName]` for a
// field on the form, `data[children][1][childFirstName]` for a field in the
// second row of a repeat section. The API names a failure by the same place in
// its own notation: `firstName`, `children[1].childFirstName`.

/** The API's field name for a Form.io input name, or undefined for another name. */
export function fieldOfInputName(name: string): string | undefined {
  const match = /^data((?:\[[^\]]+\])+)$/.exec(name);
  if (!match) return undefined;
  return Array.from(match[1].matchAll(/\[([^\]]+)\]/g), (part) => part[1]).reduce(
    (field, part) => {
      if (/^\d+$/.test(part)) return `${field}[${part}]`;
      return field ? `${field}.${part}` : part;
    },
    "",
  );
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
