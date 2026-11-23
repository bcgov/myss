import { renderHook } from "vitest-browser-react";
import { afterEach, describe, expect, it } from "vitest";

import { useAccountCreationAnswers } from "@/hooks/useAccountCreationAnswers";
import {
  type AccountCreationAnswers,
  openingAnswers,
} from "@/lib/accountContext";
import {
  clearKept,
  keepAnswers,
  readKeptAnswers,
  takeKeptFile,
} from "@/lib/keptAccountCreation";

// In the browser project: the hook keeps files in the tab's real store.

function pdf(name: string) {
  return new File(["%PDF-1.7 test"], name, { type: "application/pdf" });
}

function legal(
  overrides: Partial<AccountCreationAnswers> = {},
): AccountCreationAnswers {
  return {
    ...openingAnswers(),
    context: "legalRepresentative",
    representativeFirstName: "Pat",
    representativeLastName: "Lee",
    representativePhone: "2505550123",
    ...overrides,
  };
}

function renderAnswers(initial: AccountCreationAnswers = openingAnswers()) {
  return renderHook(() => useAccountCreationAnswers(() => initial));
}

afterEach(async () => {
  await clearKept();
  sessionStorage.clear();
});

describe("useAccountCreationAnswers", () => {
  it("starts from the answers it is given", async () => {
    const { result } = await renderAnswers();

    expect(result.current.answers).toEqual(openingAnswers());
    expect(result.current.errors).toEqual({});
    expect(result.current.summary).toBeNull();
  });

  it("restores the tab's kept answers", async () => {
    keepAnswers(legal({ representativeAgency: "Northern Trustee Services" }));

    const { result } = await renderHook(() =>
      useAccountCreationAnswers(
        () => readKeptAnswers()?.answers ?? openingAnswers(),
      ),
    );

    expect(result.current.answers.representativeAgency).toBe(
      "Northern Trustee Services",
    );
  });

  it("checks the answers, and lists the errors for the summary", async () => {
    const { result, act } = await renderAnswers(
      legal({ representativeFirstName: " ", representativePhone: "12" }),
    );

    let passed = true;
    await act(() => {
      passed = result.current.check({ canAttach: true });
    });

    expect(passed).toBe(false);
    expect(result.current.errors).toEqual({
      representativeFirstName: "Enter your first name.",
      representativePhone: "Phone number is invalid",
    });
    expect(result.current.summary?.errors.map((e) => e.field)).toEqual([
      "representativeFirstName",
      "representativePhone",
    ]);
  });

  it("passes complete answers and clears the summary", async () => {
    const { result, act } = await renderAnswers(
      legal({ representativeFirstName: "" }),
    );
    await act(() => {
      result.current.check({ canAttach: true });
    });

    await act(() => result.current.change({ representativeFirstName: "Pat" }));
    let passed = false;
    await act(() => {
      passed = result.current.check({ canAttach: true });
    });

    expect(passed).toBe(true);
    expect(result.current.summary).toBeNull();
  });

  it("drops a field's error once it changes", async () => {
    const { result, act } = await renderAnswers(
      legal({ representativeFirstName: "", representativeLastName: "" }),
    );
    await act(() => {
      result.current.check({ canAttach: true });
    });

    await act(() => result.current.change({ representativeFirstName: "P" }));

    expect(result.current.errors).toEqual({
      representativeLastName: "Enter your last name.",
    });
  });

  it("keeps a file in the tab, and deletes it from the tab when it is removed", async () => {
    const { result, act } = await renderAnswers(legal());

    await act(() =>
      result.current.addFile("powerOfAttorneyDocuments", pdf("poa.pdf")),
    );
    const [kept] = result.current.answers.powerOfAttorneyDocuments;
    expect(kept).toMatchObject({ fileName: "poa.pdf", state: "kept" });
    expect((await takeKeptFile(kept.id))?.name).toBe("poa.pdf");

    await act(() =>
      result.current.removeFile("powerOfAttorneyDocuments", kept),
    );

    expect(result.current.answers.powerOfAttorneyDocuments).toEqual([]);
    await expect.poll(() => takeKeptFile(kept.id)).toBeNull();
  });

  it("keeps no document for a question the context no longer asks, and deletes it from the tab", async () => {
    const { result, act } = await renderAnswers({
      ...openingAnswers(),
      context: "withHelper",
      helperAuthorized: "yes",
    });
    await act(() =>
      result.current.addFile("consentDocuments", pdf("consent.pdf")),
    );
    const [kept] = result.current.answers.consentDocuments;

    await act(() => result.current.change({ helperAuthorized: "no" }));
    await act(() => {
      result.current.keep();
    });

    expect(result.current.answers.consentDocuments).toEqual([]);
    expect(readKeptAnswers()?.answers.consentDocuments).toEqual([]);
    await expect.poll(() => takeKeptFile(kept.id)).toBeNull();
  });

  it("lists an uploaded file as attached", async () => {
    const { result, act } = await renderAnswers({
      ...openingAnswers(),
      context: "withHelper",
      helperAuthorized: "yes",
    });

    await act(() =>
      result.current.addUploaded("consentDocuments", {
        id: "attachment-1",
        fileName: "consent.pdf",
        contentType: "application/pdf",
        sizeBytes: 421_888,
        status: "Released",
        uploadedAt: new Date().toISOString(),
      }),
    );

    expect(result.current.answers.consentDocuments).toEqual([
      {
        id: "attachment-1",
        fileName: "consent.pdf",
        sizeBytes: 421_888,
        state: "attached",
      },
    ]);
  });

  it("shows only the API's refusals on the account-creation fields", async () => {
    const { result, act } = await renderAnswers(legal());

    await act(() =>
      result.current.showServerErrors([
        {
          field: "representativePhone",
          keyword: "IDA.PHONE.INVALID_FORMAT",
          message: "Phone number is invalid",
        },
        {
          field: "firstName",
          keyword: "FORM.FIELD.REQUIRED",
          message: "First name is required",
        },
      ]),
    );

    expect(result.current.errors).toEqual({
      representativePhone: "Phone number is invalid",
    });
  });
});
