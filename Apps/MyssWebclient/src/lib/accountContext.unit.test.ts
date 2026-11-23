/// <reference types="node" />
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { afterEach, describe, expect, it } from "vitest";

import { setErrorCatalogue } from "@myss/formio/errorCatalogue";
import {
  ACCOUNT_CONTEXTS,
  ACCOUNT_CREATION_DEFAULT_MESSAGES,
  ACCOUNT_CREATION_KEYWORDS as K,
  type AccountCreationAnswers,
  type AccountDocument,
  accountCreationMessage,
  accountCreationRecapRows,
  allowsBcServicesCard,
  checkAccountCreation,
  contextFromSlug,
  contextsFor,
  documentFieldsFor,
  isAccountCreationField,
  openingAnswers,
  registrationPathFor,
  toAccountCreationRequest,
} from "./accountContext";

// Read from disk rather than imported: the image build cannot import a file
// outside Apps/.
const sharedMessages = JSON.parse(
  readFileSync(
    fileURLToPath(
      new URL(
        "../../../../Shared/validation/error-messages.json",
        import.meta.url,
      ),
    ),
    "utf8",
  ),
) as { messages: { keyword: string; message: string }[] };

function answers(
  overrides: Partial<AccountCreationAnswers>,
): AccountCreationAnswers {
  return { ...openingAnswers(), ...overrides };
}

function representative(
  context: "legalRepresentative" | "parentForMinor",
  overrides: Partial<AccountCreationAnswers> = {},
): AccountCreationAnswers {
  return answers({
    context,
    representativeFirstName: "Pat",
    representativeLastName: "Lee",
    representativePhone: "(250) 555-0123",
    ...overrides,
  });
}

function documents(
  count: number,
  state: AccountDocument["state"] = "kept",
): AccountDocument[] {
  return Array.from({ length: count }, (_, i) => ({
    id: `doc-${i}`,
    fileName: `file-${i}.pdf`,
    sizeBytes: 100,
    state,
  }));
}

const canAttach = { canAttach: true };

afterEach(() => setErrorCatalogue({}));

describe("the contexts", () => {
  it.each(ACCOUNT_CONTEXTS)(
    "$slug round-trips through the address",
    (context) => {
      expect(contextFromSlug(context.slug)).toBe(context);
      expect(registrationPathFor(context)).toBe(
        `/registration?context=${context.slug}`,
      );
    },
  );

  it.each([null, undefined, "", "helper", "Self", "with_helper", "withHelper"])(
    "%s names no context",
    (slug) => {
      expect(contextFromSlug(slug)).toBeNull();
    },
  );

  it("only the representative contexts disallow BC Services Card", () => {
    expect(
      ACCOUNT_CONTEXTS.filter((c) => !allowsBcServicesCard(c.value)).map(
        (c) => c.value,
      ),
    ).toEqual(["legalRepresentative", "parentForMinor"]);
  });

  it("a BC Services Card sign-in can register Self and With Helper; Basic BCeID all four", () => {
    expect(contextsFor(false).map((c) => c.value)).toEqual([
      "self",
      "withHelper",
    ]);
    expect(contextsFor(true)).toEqual(ACCOUNT_CONTEXTS);
  });

  it("the questions open with Self, and No for the helper", () => {
    const opening = openingAnswers();
    expect(opening.context).toBe("self");
    expect(opening.helperAuthorized).toBe("no");
  });
});

describe("checkAccountCreation", () => {
  it("asks for a context when none is chosen", () => {
    expect(checkAccountCreation(answers({ context: null }), canAttach)).toEqual(
      [
        {
          field: "accountContext",
          keyword: K.contextRequired,
          message: "Select the option that describes you.",
        },
      ],
    );
  });

  it("Self needs nothing more", () => {
    expect(checkAccountCreation(openingAnswers(), canAttach)).toEqual([]);
  });

  it("ignores answers outside the context", () => {
    const self = answers({
      helperAuthorized: "yes",
      representativePhone: "123",
      powerOfAttorneyDocuments: documents(6),
    });
    expect(checkAccountCreation(self, canAttach)).toEqual([]);
  });

  it("With Helper asks for the authorization when it is unanswered", () => {
    const [error] = checkAccountCreation(
      answers({ context: "withHelper", helperAuthorized: null }),
      canAttach,
    );
    expect(error).toEqual({
      field: "helperAuthorized",
      keyword: K.helperAuthorizationRequired,
      message: "Select whether we may share your information with this person.",
    });
  });

  it("With Helper, No, needs no document", () => {
    expect(
      checkAccountCreation(answers({ context: "withHelper" }), canAttach),
    ).toEqual([]);
  });

  it("With Helper, Yes, needs a consent form", () => {
    expect(
      checkAccountCreation(
        answers({ context: "withHelper", helperAuthorized: "yes" }),
        canAttach,
      ),
    ).toEqual([
      {
        field: "consentDocuments",
        keyword: K.consentRequired,
        message:
          "Attach your signed Consent to Disclosure of Information form.",
      },
    ]);
  });

  it("does not ask for the consent form while files cannot be attached", () => {
    expect(
      checkAccountCreation(
        answers({ context: "withHelper", helperAuthorized: "yes" }),
        {
          canAttach: false,
        },
      ),
    ).toEqual([]);
  });

  it.each([1, 5])("With Helper, Yes, accepts %i consent forms", (count) => {
    const helper = answers({
      context: "withHelper",
      helperAuthorized: "yes",
      consentDocuments: documents(count),
    });
    expect(checkAccountCreation(helper, canAttach)).toEqual([]);
  });

  it("refuses six consent forms", () => {
    const helper = answers({
      context: "withHelper",
      helperAuthorized: "yes",
      consentDocuments: documents(6),
    });
    expect(checkAccountCreation(helper, canAttach)).toEqual([
      {
        field: "consentDocuments",
        keyword: K.tooManyDocuments,
        message: "Attach no more than 5 documents.",
      },
    ]);
  });

  it.each(["legalRepresentative", "parentForMinor"] as const)(
    "%s with complete details passes",
    (context) => {
      expect(checkAccountCreation(representative(context), canAttach)).toEqual(
        [],
      );
    },
  );

  it("asks for each blank detail, in page order", () => {
    const blank = representative("legalRepresentative", {
      representativeFirstName: "  ",
      representativeLastName: "",
      representativePhone: " ",
    });
    expect(checkAccountCreation(blank, canAttach)).toEqual([
      {
        field: "representativeFirstName",
        keyword: K.fieldRequired,
        message: "Enter your first name.",
      },
      {
        field: "representativeLastName",
        keyword: K.fieldRequired,
        message: "Enter your last name.",
      },
      {
        field: "representativePhone",
        keyword: K.fieldRequired,
        message: "Enter your phone number.",
      },
    ]);
  });

  it("accepts names of 200 characters after trimming, and refuses 201", () => {
    const fits = representative("parentForMinor", {
      representativeFirstName: `  ${"a".repeat(200)}  `,
    });
    expect(checkAccountCreation(fits, canAttach)).toEqual([]);

    const long = representative("parentForMinor", {
      representativeLastName: "b".repeat(201),
    });
    expect(checkAccountCreation(long, canAttach)).toEqual([
      {
        field: "representativeLastName",
        keyword: K.fieldMaxLength,
        message: "This answer is too long.",
      },
    ]);
  });

  it.each(["", "   ", "a".repeat(200)])("accepts the agency %j", (agency) => {
    expect(
      checkAccountCreation(
        representative("legalRepresentative", { representativeAgency: agency }),
        canAttach,
      ),
    ).toEqual([]);
  });

  it("refuses an agency of 201 characters from a legal representative only", () => {
    const agency = "a".repeat(201);
    expect(
      checkAccountCreation(
        representative("legalRepresentative", { representativeAgency: agency }),
        canAttach,
      ),
    ).toEqual([
      {
        field: "representativeAgency",
        keyword: K.fieldMaxLength,
        message: "This answer is too long.",
      },
    ]);
    expect(
      checkAccountCreation(
        representative("parentForMinor", { representativeAgency: agency }),
        canAttach,
      ),
    ).toEqual([]);
  });

  it.each(["(250) 555-0123", "2505550123", "250 555 0123", "250-555-0123"])(
    "accepts the phone %s",
    (phone) => {
      expect(
        checkAccountCreation(
          representative("parentForMinor", { representativePhone: phone }),
          canAttach,
        ),
      ).toEqual([]);
    },
  );

  it.each(["12345", "(150) 555-0123", "(250) 555-012a", "(250) 555-01234"])(
    "refuses the phone %s",
    (phone) => {
      expect(
        checkAccountCreation(
          representative("legalRepresentative", { representativePhone: phone }),
          canAttach,
        ),
      ).toEqual([
        {
          field: "representativePhone",
          keyword: K.phoneInvalid,
          message: "Phone number is invalid",
        },
      ]);
    },
  );

  it("accepts 5 power of attorney documents and refuses 6", () => {
    expect(
      checkAccountCreation(
        representative("legalRepresentative", {
          powerOfAttorneyDocuments: documents(5),
        }),
        canAttach,
      ),
    ).toEqual([]);
    expect(
      checkAccountCreation(
        representative("legalRepresentative", {
          powerOfAttorneyDocuments: documents(6),
        }),
        canAttach,
      ),
    ).toEqual([
      {
        field: "powerOfAttorneyDocuments",
        keyword: K.tooManyDocuments,
        message: "Attach no more than 5 documents.",
      },
    ]);
  });

  it("ignores power of attorney documents for a parent", () => {
    expect(
      checkAccountCreation(
        representative("parentForMinor", {
          powerOfAttorneyDocuments: documents(6),
        }),
        canAttach,
      ),
    ).toEqual([]);
  });

  it("takes the catalogue's wording when one is published", () => {
    setErrorCatalogue({ [K.phoneInvalid]: "Authored phone wording" });
    const [error] = checkAccountCreation(
      representative("parentForMinor", { representativePhone: "1" }),
      canAttach,
    );
    expect(error.message).toBe("Authored phone wording");
  });
});

describe("documentFieldsFor", () => {
  it("names the consent forms after Yes, and the power of attorney for a legal representative", () => {
    expect(documentFieldsFor(answers({ context: "withHelper" }))).toEqual([]);
    expect(
      documentFieldsFor(
        answers({ context: "withHelper", helperAuthorized: "yes" }),
      ),
    ).toEqual(["consentDocuments"]);
    expect(documentFieldsFor(representative("legalRepresentative"))).toEqual([
      "powerOfAttorneyDocuments",
    ]);
    expect(documentFieldsFor(representative("parentForMinor"))).toEqual([]);
  });
});

describe("toAccountCreationRequest", () => {
  it("sends Self alone", () => {
    expect(
      toAccountCreationRequest({
        ...representative("legalRepresentative"),
        context: "self",
      }),
    ).toEqual({ context: "self" });
  });

  it("sends the authorization, and the uploaded consent forms after Yes", () => {
    const helper = answers({
      context: "withHelper",
      helperAuthorized: "yes",
      consentDocuments: [
        ...documents(1, "attached"),
        { id: "kept", fileName: "k.pdf", sizeBytes: 1, state: "kept" },
      ],
    });
    expect(
      toAccountCreationRequest({ ...helper, context: "withHelper" }),
    ).toEqual({
      context: "withHelper",
      helperAuthorized: true,
      consentDocumentIds: ["doc-0"],
    });
    expect(
      toAccountCreationRequest({
        ...helper,
        helperAuthorized: "no",
        context: "withHelper",
      }),
    ).toEqual({ context: "withHelper", helperAuthorized: false });
  });

  it("sends a legal representative's trimmed details, agency and documents", () => {
    const legal = representative("legalRepresentative", {
      representativeFirstName: " Pat ",
      representativeAgency: "  Northern Trustee Services ",
      powerOfAttorneyDocuments: documents(2, "attached"),
      consentDocuments: documents(1, "attached"),
    });
    expect(
      toAccountCreationRequest({ ...legal, context: "legalRepresentative" }),
    ).toEqual({
      context: "legalRepresentative",
      representative: {
        firstName: "Pat",
        lastName: "Lee",
        phone: "(250) 555-0123",
        agency: "Northern Trustee Services",
      },
      powerOfAttorneyDocumentIds: ["doc-0", "doc-1"],
    });
  });

  it("leaves out a blank agency, and any agency or documents for a parent", () => {
    const blankAgency = representative("legalRepresentative", {
      representativeAgency: "  ",
    });
    expect(
      toAccountCreationRequest({
        ...blankAgency,
        context: "legalRepresentative",
      }).representative,
    ).not.toHaveProperty("agency");

    const parent = representative("parentForMinor", {
      representativeAgency: "Ignored",
      powerOfAttorneyDocuments: documents(1, "attached"),
    });
    expect(
      toAccountCreationRequest({ ...parent, context: "parentForMinor" }),
    ).toEqual({
      context: "parentForMinor",
      representative: {
        firstName: "Pat",
        lastName: "Lee",
        phone: "(250) 555-0123",
      },
    });
  });
});

describe("accountCreationRecapRows", () => {
  it("lists only the chosen context's answers", () => {
    const rows = accountCreationRecapRows(
      representative("legalRepresentative", {
        representativePhone: "2505550123",
        representativeAgency: "  ",
        consentDocuments: [
          { id: "c", fileName: "ignored.pdf", sizeBytes: 1, state: "kept" },
        ],
        powerOfAttorneyDocuments: [
          { id: "p", fileName: "poa.pdf", sizeBytes: 421_888, state: "kept" },
        ],
      }),
    );

    expect(rows).toEqual([
      {
        label: "Who the account is for",
        value:
          "I’m a legal representative who is creating an account on behalf of someone else",
      },
      { label: "Your name (authorized representative)", value: "Pat Lee" },
      { label: "Your phone number", value: "(250) 555-0123" },
      { label: "Agency", value: "Not provided" },
      { label: "Power of attorney documents", value: ["poa.pdf (412 KB)"] },
    ]);
  });

  it("lists With Helper's authorization and its consent forms", () => {
    const rows = accountCreationRecapRows(
      answers({ context: "withHelper", helperAuthorized: "yes" }),
    );

    expect(rows).toEqual([
      {
        label: "Who the account is for",
        value: "I’m making an account for myself with help from someone else",
      },
      {
        label: "May we share your information with this person?",
        value: "Yes",
      },
      { label: "Consent to Disclosure of Information", value: "None attached" },
    ]);
  });

  it("lists only who the account is for, for Self", () => {
    expect(accountCreationRecapRows(openingAnswers())).toEqual([
      {
        label: "Who the account is for",
        value: "I’m making an account for myself (and family, if applicable)",
      },
    ]);
  });
});

describe("isAccountCreationField", () => {
  it("knows the account-creation fields and no others", () => {
    expect(isAccountCreationField("powerOfAttorneyDocuments")).toBe(true);
    expect(isAccountCreationField("representativePhone")).toBe(true);
    expect(isAccountCreationField("firstName")).toBe(false);
  });
});

describe("the wording", () => {
  it("matches the shared catalogue, keyword for keyword", () => {
    const shared = new Map(
      sharedMessages.messages.map((m) => [m.keyword, m.message]),
    );
    for (const [keyword, message] of Object.entries(
      ACCOUNT_CREATION_DEFAULT_MESSAGES,
    )) {
      expect(shared.get(keyword), keyword).toBe(message);
    }
  });

  it("falls back to the keyword itself for one it does not know", () => {
    expect(accountCreationMessage("NOT.A.KEYWORD")).toBe("NOT.A.KEYWORD");
  });
});
