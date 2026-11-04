import { describe, expect, it } from "vitest";

import {
  ACKNOWLEDGEMENT_KEYWORDS,
  ACKNOWLEDGEMENT_LIMITS,
  BUS_PASS_ACKNOWLEDGEMENT,
  normaliseAcknowledgementText,
  validateAcknowledgement,
  type AcknowledgementField,
  type SubmissionAcknowledgement,
} from "./acknowledgements";

// The admin API's SubmissionAcknowledgementValidatorTests check the same cases
// against the same wording.

const PLACEHOLDER = "{referenceNumber}";

function withText(
  field: AcknowledgementField,
  value: string,
): SubmissionAcknowledgement {
  return { ...BUS_PASS_ACKNOWLEDGEMENT, [field]: value };
}

// A message keeps the placeholder, so only its length can fail.
function textOfLength(field: AcknowledgementField, length: number): string {
  return field === "body"
    ? PLACEHOLDER + "x".repeat(length - PLACEHOLDER.length)
    : "x".repeat(length);
}

describe("validateAcknowledgement", () => {
  it("accepts the built-in bus pass message", () => {
    expect(validateAcknowledgement(BUS_PASS_ACKNOWLEDGEMENT)).toEqual([]);
  });

  it("limits the heading and title to 100 characters and the message to 1,000", () => {
    expect(ACKNOWLEDGEMENT_LIMITS).toEqual({ heading: 100, title: 100, body: 1000 });
  });

  it.each([
    ["heading", "Enter a page heading."],
    ["title", "Enter a message title."],
    ["body", "Enter the message."],
  ] as const)("requires the %s", (field, message) => {
    for (const blank of ["", "   ", "\r\n\t"]) {
      expect(validateAcknowledgement(withText(field, blank))).toEqual([
        { field, keyword: ACKNOWLEDGEMENT_KEYWORDS.textRequired, message },
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
      expect(
        validateAcknowledgement(withText(field, textOfLength(field, limit))),
      ).toEqual([]);
      expect(
        validateAcknowledgement(withText(field, textOfLength(field, limit + 1))),
      ).toEqual([
        {
          field,
          keyword: ACKNOWLEDGEMENT_KEYWORDS.textTooLong,
          message: `${label} must be ${shownLimit} characters or fewer. It has ${shownLength}.`,
        },
      ]);
    },
  );

  it("counts a line break once and not the spaces at either end", () => {
    const body = `${PLACEHOLDER}\r\n${"x".repeat(1000 - PLACEHOLDER.length - 1)}`;

    expect(body).toHaveLength(1001);
    expect(validateAcknowledgement(withText("body", `  ${body}\r\n  `))).toEqual([]);
  });

  it("gives an over-long message's count in full", () => {
    const [error] = validateAcknowledgement(
      withText("body", textOfLength("body", 1012)),
    );

    expect(error.message).toBe(
      "The message must be 1,000 characters or fewer. It has 1,012.",
    );
  });

  it("refuses a message without the placeholder", () => {
    expect(
      validateAcknowledgement(withText("body", "Your reference number is on its way.")),
    ).toEqual([
      {
        field: "body",
        keyword: ACKNOWLEDGEMENT_KEYWORDS.referenceMissing,
        message:
          "Add {referenceNumber} where the applicant's reference number should appear.",
      },
    ]);
  });

  it("names a misspelt placeholder once, besides the missing one", () => {
    const errors = validateAcknowledgement(
      withText("body", "Your number is {reference}. Again: {reference}."),
    );

    expect(errors.map((error) => error.keyword)).toEqual([
      ACKNOWLEDGEMENT_KEYWORDS.referenceMissing,
      ACKNOWLEDGEMENT_KEYWORDS.placeholderUnknown,
    ]);
    expect(errors[1].message).toBe(
      "{reference} is not a placeholder this message can use. The only one is {referenceNumber}.",
    );
  });

  it.each([
    "[the program](https://www2.gov.bc.ca/gov/content/bus-pass)",
    "[email us](mailto:buspass@example.com)",
    "[call us](tel:18005551234)",
    "[shouting](HTTPS://www2.gov.bc.ca)",
  ])("accepts the link %s", (link) => {
    expect(
      validateAcknowledgement(withText("body", `See ${link}. ${PLACEHOLDER}`)),
    ).toEqual([]);
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
    expect(
      validateAcknowledgement(withText("body", `See ${link}. ${PLACEHOLDER}`)),
    ).toEqual([
      {
        field: "body",
        keyword: ACKNOWLEDGEMENT_KEYWORDS.linkInvalid,
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
      validateAcknowledgement(withText("body", `See ${link}.`)).map(
        (error) => error.keyword,
      ),
    ).toEqual([ACKNOWLEDGEMENT_KEYWORDS.referenceMissing]);
  });

  it("does not check an over-long message further", () => {
    expect(
      validateAcknowledgement(withText("body", "[".repeat(1001))).map(
        (error) => error.keyword,
      ),
    ).toEqual([ACKNOWLEDGEMENT_KEYWORDS.textTooLong]);
  });

  it("reports every problem together", () => {
    const errors = validateAcknowledgement({
      heading: "  ",
      title: "t".repeat(101),
      body: "No placeholder, and [a link](http://example.com).",
    });

    expect(errors.map(({ field, keyword }) => [field, keyword])).toEqual([
      ["heading", ACKNOWLEDGEMENT_KEYWORDS.textRequired],
      ["title", ACKNOWLEDGEMENT_KEYWORDS.textTooLong],
      ["body", ACKNOWLEDGEMENT_KEYWORDS.referenceMissing],
      ["body", ACKNOWLEDGEMENT_KEYWORDS.linkInvalid],
    ]);
  });
});

describe("normaliseAcknowledgementText", () => {
  it("makes every line break a line feed and trims", () => {
    expect(normaliseAcknowledgementText(" a\r\nb\rc \n")).toBe("a\nb\nc");
  });
});
