import { describe, expect, it } from "vitest";

import {
  BUS_PASS_CONFIRMATION,
  builtInFormMessage,
  FORM_MESSAGE_KEYWORDS,
  FORM_MESSAGE_LIMITS,
  normaliseFormMessageText,
  validateFormMessage,
  type FormMessageField,
  type FormMessagePlaceholder,
  type FormMessageTexts,
} from "./formMessages";

// The admin API's FormMessageValidatorTests check the same cases
// against the same wording.

const PLACEHOLDER = "{referenceNumber}";

const BUS_PASS_PLACEHOLDERS: readonly FormMessagePlaceholder[] = [
  {
    name: "referenceNumber",
    description: "the applicant's reference number",
    sample: "1-0123456789",
    required: true,
  },
];

const CASE_NUMBER: FormMessagePlaceholder = {
  name: "caseNumber",
  description: "the case number",
  sample: "C-123",
  required: true,
};

const THREE_PLACEHOLDERS: readonly FormMessagePlaceholder[] = [
  CASE_NUMBER,
  {
    name: "submittedAt",
    description: "the time it was submitted",
    sample: "14 Jul 2026 at 10:00 a.m.",
    required: false,
  },
  {
    name: "office",
    description: "the office that handles it",
    sample: "Victoria",
    required: false,
  },
];

const TWO_REQUIRED: readonly FormMessagePlaceholder[] = [
  CASE_NUMBER,
  {
    name: "office",
    description: "the office that handles it",
    sample: "Victoria",
    required: true,
  },
];

function withText(field: FormMessageField, value: string): FormMessageTexts {
  return { ...BUS_PASS_CONFIRMATION, [field]: value };
}

function validate(texts: FormMessageTexts) {
  return validateFormMessage(texts, BUS_PASS_PLACEHOLDERS);
}

// A message keeps the placeholder, so only its length can fail.
function textOfLength(field: FormMessageField, length: number): string {
  return field === "body"
    ? PLACEHOLDER + "x".repeat(length - PLACEHOLDER.length)
    : "x".repeat(length);
}

describe("validateFormMessage", () => {
  it("accepts the built-in bus pass confirmation", () => {
    expect(validate(BUS_PASS_CONFIRMATION)).toEqual([]);
  });

  it("limits the heading and title to 100 characters and the message to 1,000", () => {
    expect(FORM_MESSAGE_LIMITS).toEqual({ heading: 100, title: 100, body: 1000 });
  });

  it.each([
    ["heading", "Enter a page heading."],
    ["title", "Enter a message title."],
    ["body", "Enter the message."],
  ] as const)("requires the %s", (field, message) => {
    for (const blank of ["", "   ", "\r\n\t"]) {
      expect(validate(withText(field, blank))).toEqual([
        { field, keyword: FORM_MESSAGE_KEYWORDS.textRequired, message },
      ]);
    }
  });

  it.each([
    ["heading", 100, "The page heading", "100", "101"],
    ["title", 100, "The message title", "100", "101"],
    ["body", 1000, "The message", "1,000", "1,001"],
  ] as const)(
    "limits the %s to %i characters",
    (field, limit, label, shownLimit, shownLength) => {
      expect(validate(withText(field, textOfLength(field, limit)))).toEqual([]);
      expect(validate(withText(field, textOfLength(field, limit + 1)))).toEqual([
        {
          field,
          keyword: FORM_MESSAGE_KEYWORDS.textTooLong,
          message: `${label} must be ${shownLimit} characters or fewer. It has ${shownLength}.`,
        },
      ]);
    },
  );

  it("counts a line break once and not the spaces at either end", () => {
    const body = `${PLACEHOLDER}\r\n${"x".repeat(1000 - PLACEHOLDER.length - 1)}`;

    expect(body).toHaveLength(1001);
    expect(validate(withText("body", `  ${body}\r\n  `))).toEqual([]);
  });

  it("gives an over-long message's count in full", () => {
    const errors = validate(withText("body", textOfLength("body", 1012)));

    expect(errors.map((error) => error.message)).toEqual([
      "The message must be 1,000 characters or fewer. It has 1,012.",
    ]);
  });

  it("refuses a message without the placeholder", () => {
    expect(validate(withText("body", "Your reference number is on its way."))).toEqual([
      {
        field: "body",
        keyword: FORM_MESSAGE_KEYWORDS.placeholderMissing,
        message:
          "Add {referenceNumber} where the applicant's reference number should appear.",
      },
    ]);
  });

  it("names a misspelt placeholder once, besides the missing one", () => {
    const errors = validate(
      withText("body", "Your number is {reference}. Again: {reference}."),
    );

    expect(errors.map((error) => error.keyword)).toEqual([
      FORM_MESSAGE_KEYWORDS.placeholderMissing,
      FORM_MESSAGE_KEYWORDS.placeholderUnknown,
    ]);
    expect(errors[1].message).toBe(
      "{reference} is not a placeholder this message can use. The only one is {referenceNumber}.",
    );
  });

  it("lets an optional placeholder be left out", () => {
    expect(
      validateFormMessage(
        withText("body", "Your case is {caseNumber}."),
        THREE_PLACEHOLDERS,
      ),
    ).toEqual([]);
  });

  it("names a missing required placeholder with its description", () => {
    expect(
      validateFormMessage(
        withText("body", "Sent {submittedAt} to {office}."),
        THREE_PLACEHOLDERS,
      ),
    ).toEqual([
      {
        field: "body",
        keyword: FORM_MESSAGE_KEYWORDS.placeholderMissing,
        message: "Add {caseNumber} where the case number should appear.",
      },
    ]);
  });

  it("reports every missing required placeholder, in the definition's order", () => {
    expect(
      validateFormMessage(withText("body", "Thank you."), TWO_REQUIRED).map(
        (error) => error.message,
      ),
    ).toEqual([
      "Add {caseNumber} where the case number should appear.",
      "Add {office} where the office that handles it should appear.",
    ]);
  });

  it("lists the placeholders a message can use when it finds an unknown one", () => {
    expect(
      validateFormMessage(
        withText("body", "Your case is {caseNumber}, sent {when}."),
        THREE_PLACEHOLDERS,
      ),
    ).toEqual([
      {
        field: "body",
        keyword: FORM_MESSAGE_KEYWORDS.placeholderUnknown,
        message:
          "{when} is not a placeholder this message can use. The ones it can use are {caseNumber}, {submittedAt} and {office}.",
      },
    ]);
  });

  it("names both placeholders of a message that has two", () => {
    expect(
      validateFormMessage(
        withText("body", "{caseNumber} at {office}, {when}."),
        TWO_REQUIRED,
      ).map((error) => error.message),
    ).toEqual([
      "{when} is not a placeholder this message can use. The ones it can use are {caseNumber} and {office}.",
    ]);
  });

  it("reports each unknown placeholder once, in the order it first appears", () => {
    expect(
      validate(withText("body", `${PLACEHOLDER} {b} then {a} then {b}.`)).map(
        (error) => error.message,
      ),
    ).toEqual([
      "{b} is not a placeholder this message can use. The only one is {referenceNumber}.",
      "{a} is not a placeholder this message can use. The only one is {referenceNumber}.",
    ]);
  });

  it("refuses an unknown placeholder inside a link", () => {
    expect(
      validate(
        withText(
          "body",
          `${PLACEHOLDER}. [Track it](https://www2.gov.bc.ca/?r={track}).`,
        ),
      ),
    ).toEqual([
      {
        field: "body",
        keyword: FORM_MESSAGE_KEYWORDS.placeholderUnknown,
        message:
          "{track} is not a placeholder this message can use. The only one is {referenceNumber}.",
      },
    ]);
  });

  it("uses the keywords the admin API returns", () => {
    expect(FORM_MESSAGE_KEYWORDS).toEqual({
      textRequired: "FORM_MESSAGE.TEXT.REQUIRED",
      textTooLong: "FORM_MESSAGE.TEXT.TOO_LONG",
      placeholderMissing: "FORM_MESSAGE.BODY.PLACEHOLDER_MISSING",
      placeholderUnknown: "FORM_MESSAGE.BODY.PLACEHOLDER_UNKNOWN",
      linkInvalid: "FORM_MESSAGE.BODY.LINK_INVALID",
      changed: "FORM_MESSAGE.CHANGED",
    });
  });

  it("refuses any placeholder in a message that has none, and needs none", () => {
    expect(validateFormMessage(withText("body", "Welcome."), [])).toEqual([]);
    expect(validateFormMessage(withText("body", "Welcome, {name}."), [])).toEqual([
      {
        field: "body",
        keyword: FORM_MESSAGE_KEYWORDS.placeholderUnknown,
        message: "{name} is not a placeholder this message can use.",
      },
    ]);
  });

  it.each([
    "[the program](https://www2.gov.bc.ca/gov/content/bus-pass)",
    "[email us](mailto:buspass@example.com)",
    "[call us](tel:18005551234)",
    "[shouting](HTTPS://www2.gov.bc.ca)",
  ])("accepts the link %s", (link) => {
    expect(validate(withText("body", `See ${link}. ${PLACEHOLDER}`))).toEqual([]);
  });

  it.each([
    "[the program](http://www2.gov.bc.ca)",
    "[the program](javascript:alert(1)",
    "[the program](/buspass)",
    "[the program]()",
    "[the program](https://)",
    "[the program](https://www2.gov.bc.ca and more)",
    "[the program](tel:1 800 555 1234)",
  ])("refuses the link %s", (link) => {
    expect(validate(withText("body", `See ${link}. ${PLACEHOLDER}`))).toEqual([
      {
        field: "body",
        keyword: FORM_MESSAGE_KEYWORDS.linkInvalid,
        message:
          "The link “the program” must start with https://, mailto: or tel:, with no spaces.",
      },
    ]);
  });

  it.each([
    "[track it](https://www2.gov.bc.ca/?r={referenceNumber})",
    "[{referenceNumber}](https://www2.gov.bc.ca)",
  ])("does not count the placeholder inside %s", (link) => {
    expect(
      validate(withText("body", `See ${link}.`)).map((error) => error.keyword),
    ).toEqual([FORM_MESSAGE_KEYWORDS.placeholderMissing]);
  });

  it("does not check an over-long message further", () => {
    expect(
      validate(withText("body", "[".repeat(1001))).map((error) => error.keyword),
    ).toEqual([FORM_MESSAGE_KEYWORDS.textTooLong]);
  });

  it("reports every problem together", () => {
    const errors = validate({
      heading: "  ",
      title: "t".repeat(101),
      body: "No placeholder, and [a link](http://example.com).",
    });

    expect(errors.map(({ field, keyword }) => [field, keyword])).toEqual([
      ["heading", FORM_MESSAGE_KEYWORDS.textRequired],
      ["title", FORM_MESSAGE_KEYWORDS.textTooLong],
      ["body", FORM_MESSAGE_KEYWORDS.placeholderMissing],
      ["body", FORM_MESSAGE_KEYWORDS.linkInvalid],
    ]);
  });
});

describe("builtInFormMessage", () => {
  it("gives the bus pass confirmation's built-in texts", () => {
    expect(builtInFormMessage("bc-bus-pass", "confirmation")).toBe(
      BUS_PASS_CONFIRMATION,
    );
  });

  it.each([
    ["bc-bus-pass", "intro"],
    ["registration", "confirmation"],
    ["bc-bus-pass", "Confirmation"],
  ])("has none for %s %s", (formSpecId, messageKey) => {
    expect(builtInFormMessage(formSpecId, messageKey)).toBeUndefined();
  });
});

describe("normaliseFormMessageText", () => {
  it("makes every line break a line feed and trims", () => {
    expect(normaliseFormMessageText(" a\r\nb\rc \n")).toBe("a\nb\nc");
  });
});
