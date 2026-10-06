import { afterEach, describe, expect, it } from "vitest";

// The shared vectors are the contract between this file and MyssApi's C#
// rules: both suites read them, so a divergence is a failing test rather than
// two systems disagreeing about a citizen's answer. See the file's header.
import sharedMessages from "../../../../../Shared/validation/error-messages.json";
import sharedVectors from "../../../../../Shared/validation/validation-vectors.json";

import { setErrorCatalogue } from "@/lib/errorCatalogue";
import {
  checkMatches,
  checkRule,
  DEFAULT_MESSAGES,
  RULE_KEYWORDS,
  ruleMessage,
  VALIDATION_RULES,
} from "./validationRules";

interface Vector {
  value: string;
  keyword?: string;
  note?: string;
}

interface ConfirmationVector {
  value: string;
  confirmation: string;
  keyword?: string;
}

const vectors = sharedVectors as unknown as Record<
  string,
  Record<string, Vector[]>
>;

const context = { data: {}, component: {} };

function describeRule(rule: string, section: string = rule) {
  describe(`${rule} rule`, () => {
    const valid = vectors[section].valid;
    const invalid = vectors[section].invalid;

    it("has vectors to run", () => {
      expect(valid.length).toBeGreaterThan(0);
      expect(invalid.length).toBeGreaterThan(0);
    });

    it.each(valid.map((v) => [v.value, v.note ?? ""]))(
      "accepts %j (%s)",
      (value) => {
        expect(checkRule(rule, value, context)).toBeNull();
      },
    );

    it.each(invalid.map((v) => [v.value, v.keyword ?? "", v.note ?? ""]))(
      "refuses %j with %s (%s)",
      (value, keyword) => {
        const result = checkRule(rule, value, context);
        expect(result?.keyword).toBe(keyword);
        expect(result?.rule).toBe(rule);
      },
    );
  });
}

describeRule("sin");
describeRule("email");
describeRule("phone");
describeRule("postalCode");
describeRule("date");

describe("email confirmation", () => {
  const matching = vectors.emailConfirmation
    .matching as unknown as ConfirmationVector[];
  const mismatching = vectors.emailConfirmation
    .mismatching as unknown as ConfirmationVector[];

  it.each(matching.map((v) => [v.value, v.confirmation]))(
    "accepts %j confirmed as %j",
    (value, confirmation) => {
      expect(checkMatches(confirmation, value)).toBeNull();
    },
  );

  it.each(mismatching.map((v) => [v.value, v.confirmation, v.keyword ?? ""]))(
    "refuses %j confirmed as %j",
    (value, confirmation, keyword) => {
      expect(checkMatches(confirmation, value)?.keyword).toBe(keyword);
    },
  );
});

describe("dateParts rule, shared vectors", () => {
  const component = {
    properties: { myssDateParts: { month: "birthMonth", year: "birthYear" } },
  };
  const sections = vectors.dateParts as unknown as Record<
    string,
    {
      day: string;
      month: string;
      year: string;
      keyword?: string;
      note?: string;
    }[]
  >;
  const run = (day: string, month: string, year: string) =>
    checkRule("dateParts", day, {
      data: { birthMonth: month, birthYear: year },
      component,
    });

  it.each(sections.valid.map((v) => [v.day, v.month, v.year, v.note ?? ""]))(
    "accepts %s/%s/%s (%s)",
    (day, month, year) => {
      expect(run(day, month, year)).toBeNull();
    },
  );

  it.each(
    sections.invalid.map((v) => [v.day, v.month, v.year, v.keyword ?? ""]),
  )("refuses %s/%s/%s with %s", (day, month, year, keyword) => {
    expect(run(day, month, year)?.keyword).toBe(keyword);
  });

  it.each(
    sections.incomplete.map((v) => [v.day, v.month, v.year, v.note ?? ""]),
  )("leaves %s/%s/%s to the other rules (%s)", (day, month, year) => {
    expect(run(day, month, year)).toBeNull();
  });
});

describe("dateParts rule", () => {
  const component = {
    properties: { myssDateParts: { month: "birthMonth", year: "birthYear" } },
  };

  it("accepts a real date composed from the sibling fields", () => {
    expect(
      checkRule("dateParts", "29", {
        data: { birthMonth: "02", birthYear: "2000" },
        component,
      }),
    ).toBeNull();
  });

  it("refuses an impossible date on the day field", () => {
    const result = checkRule("dateParts", "31", {
      data: { birthMonth: "02", birthYear: "2001" },
      component,
    });
    expect(result?.keyword).toBe(RULE_KEYWORDS.dateInvalid);
  });

  it("leaves an incomplete group to the required rule", () => {
    expect(
      checkRule("dateParts", "31", {
        data: { birthMonth: "", birthYear: "2001" },
        component,
      }),
    ).toBeNull();
    expect(
      checkRule("dateParts", "31", { data: {}, component: {} }),
    ).toBeNull();
  });
});

describe("checkRule", () => {
  it("treats an unknown rule name as no rule", () => {
    expect(checkRule("noSuchRule", "anything", context)).toBeNull();
  });

  it("exposes every rule a form can name", () => {
    expect(Object.keys(VALIDATION_RULES).sort()).toEqual([
      "date",
      "dateParts",
      "email",
      "phone",
      "postalCode",
      "sin",
    ]);
  });
});

describe("DEFAULT_MESSAGES", () => {
  it("matches the shared error message catalogue word for word", () => {
    const shared = sharedMessages as {
      messages: { keyword: string; message: string }[];
    };
    const byKeyword = new Map(
      shared.messages.map((row) => [row.keyword, row.message]),
    );
    for (const [keyword, message] of Object.entries(DEFAULT_MESSAGES)) {
      expect(byKeyword.get(keyword), keyword).toBe(message);
    }
  });
});

describe("ruleMessage", () => {
  afterEach(() => setErrorCatalogue(undefined));

  const failed = {
    rule: "sin",
    keyword: RULE_KEYWORDS.sinInvalidChecksum,
    message: "compiled",
  };

  it("prefers the form's wording for the rule on this field", () => {
    setErrorCatalogue({ [RULE_KEYWORDS.sinInvalidChecksum]: "catalogue" });
    expect(
      ruleMessage(
        { errors: { sin: "authored" }, validate: { customMessage: "custom" } },
        failed,
      ),
    ).toBe("authored");
  });

  it("then the field's catch-all custom message", () => {
    setErrorCatalogue({ [RULE_KEYWORDS.sinInvalidChecksum]: "catalogue" });
    expect(ruleMessage({ validate: { customMessage: "custom" } }, failed)).toBe(
      "custom",
    );
  });

  it("then the catalogue, then the compiled wording", () => {
    setErrorCatalogue({ [RULE_KEYWORDS.sinInvalidChecksum]: "catalogue" });
    expect(ruleMessage({}, failed)).toBe("catalogue");
    setErrorCatalogue(undefined);
    expect(ruleMessage({}, failed)).toBe("compiled");
  });
});
