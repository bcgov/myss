import { describe, expect, it } from "vitest";

import { fieldOfInputName, inputNameOfField } from "./fieldNames";

const PAIRS: [string, string][] = [
  ["data[firstName]", "firstName"],
  ["data[children][1][childFirstName]", "children[1].childFirstName"],
  ["data[children][0][pets][2][petName]", "children[0].pets[2].petName"],
  ["data[2024]", "2024"],
  ["data[children][0][5]", "children[0].5"],
];

describe("fieldOfInputName", () => {
  it.each(PAIRS)("reads %s as %s", (name, field) => {
    expect(fieldOfInputName(name)).toBe(field);
  });

  it.each(["", "firstName", "data", "data[]", "other[firstName]"])(
    "reads %j as no field",
    (name) => {
      expect(fieldOfInputName(name)).toBeUndefined();
    },
  );
});

describe("inputNameOfField", () => {
  it.each(PAIRS)("names %s for %s", (name, field) => {
    expect(inputNameOfField(field)).toBe(name);
  });
});
