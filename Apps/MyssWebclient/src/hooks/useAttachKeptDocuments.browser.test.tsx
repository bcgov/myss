import { StrictMode } from "react";
import { renderHook } from "vitest-browser-react";
import { afterEach, describe, expect, it, vi } from "vitest";

import { useAttachKeptDocuments } from "@/hooks/useAttachKeptDocuments";
import {
  type AccountCreationAnswers,
  type AccountDocument,
  openingAnswers,
} from "@/lib/accountContext";
import {
  clearKept,
  keepFile,
  readKeptAnswers,
  takeKeptFile,
} from "@/lib/keptAccountCreation";

// With the real tab store, and the attachments API stubbed.

function pdf(name: string) {
  return new File(["%PDF-1.7 test"], name, { type: "application/pdf" });
}

// Refuses the files named, as the API does; stores the rest.
function stubUploads(
  refusals: Record<string, { status: number; keyword: string }> = {},
) {
  const uploads: string[] = [];
  vi.spyOn(window, "fetch").mockImplementation(async (input, init) => {
    if (!String(input).endsWith("/v1/attachments") || init?.method !== "POST") {
      throw new Error(`Unexpected fetch in test: ${String(input)}`);
    }
    const file = (init.body as FormData).get("file") as File;
    uploads.push(file.name);
    const refusal = refusals[file.name];
    if (refusal) {
      return new Response(JSON.stringify(refusal), {
        status: refusal.status,
        headers: { "Content-Type": "application/problem+json" },
      });
    }
    const stored = {
      id: `attachment-${file.name}`,
      fileName: file.name,
      contentType: file.type,
      sizeBytes: file.size,
      status: "Released",
      uploadedAt: new Date().toISOString(),
    };
    return new Response(JSON.stringify({ payload: stored }), {
      status: 200,
      headers: { "Content-Type": "application/json" },
    });
  });
  return uploads;
}

async function kept(name: string): Promise<AccountDocument> {
  const file = pdf(name);
  return {
    id: (await keepFile(file))!,
    fileName: name,
    sizeBytes: file.size,
    state: "kept",
  };
}

function legalRepresentative(
  documents: AccountDocument[],
): AccountCreationAnswers {
  return {
    ...openingAnswers(),
    context: "legalRepresentative",
    representativeFirstName: "Pat",
    representativeLastName: "Lee",
    representativePhone: "2505550123",
    powerOfAttorneyDocuments: documents,
  };
}

afterEach(async () => {
  vi.restoreAllMocks();
  await clearKept();
  sessionStorage.clear();
});

describe("useAttachKeptDocuments", () => {
  it("uploads the kept files in order, and empties the tab of them", async () => {
    const uploads = stubUploads();
    const documents = [await kept("first.pdf"), await kept("second.pdf")];
    const answers = legalRepresentative(documents);

    const onDone = vi.fn();

    const { result } = await renderHook(() =>
      useAttachKeptDocuments(answers, onDone),
    );

    await expect.poll(() => result.current.state).toBe("done");
    expect(onDone).toHaveBeenCalledExactlyOnceWith(result.current);
    expect(uploads).toEqual(["first.pdf", "second.pdf"]);
    expect(result.current).toMatchObject({ done: 2, total: 2, refused: [] });
    expect(result.current.attached.powerOfAttorneyDocuments).toEqual([
      {
        id: "attachment-first.pdf",
        fileName: "first.pdf",
        sizeBytes: 13,
        state: "attached",
      },
      {
        id: "attachment-second.pdf",
        fileName: "second.pdf",
        sizeBytes: 13,
        state: "attached",
      },
    ]);
    expect(await takeKeptFile(documents[0].id)).toBeNull();
    expect(await takeKeptFile(documents[1].id)).toBeNull();
    expect(readKeptAnswers()?.answers.powerOfAttorneyDocuments).toEqual(
      result.current.attached.powerOfAttorneyDocuments,
    );
  });

  it("names a refused file with the scan's message, and still uploads the others", async () => {
    stubUploads({
      "eicar.pdf": { status: 422, keyword: "DOC.UPLOAD.INFECTED" },
    });
    const documents = [await kept("eicar.pdf"), await kept("clean.pdf")];

    const { result } = await renderHook(() =>
      useAttachKeptDocuments(legalRepresentative(documents)),
    );

    await expect.poll(() => result.current.state).toBe("done");
    expect(result.current.refused).toEqual([
      {
        field: "powerOfAttorneyDocuments",
        fileName: "eicar.pdf",
        message: "The file failed the virus scan and was not stored.",
      },
    ]);
    expect(
      result.current.attached.powerOfAttorneyDocuments?.map((d) => d.fileName),
    ).toEqual(["clean.pdf"]);
    expect(await takeKeptFile(documents[0].id)).toBeNull();
  });

  it("gives the scanner's message when the scan cannot run", async () => {
    stubUploads({
      "scan.pdf": { status: 503, keyword: "DOC.SCAN.UNAVAILABLE" },
    });

    const answers = legalRepresentative([await kept("scan.pdf")]);

    const { result } = await renderHook(() => useAttachKeptDocuments(answers));

    await expect.poll(() => result.current.state).toBe("done");
    expect(result.current.refused[0].message).toBe(
      "The virus scanner is unavailable right now. Try again in a few minutes.",
    );
  });

  it("reports a file the tab can no longer read as not kept", async () => {
    const uploads = stubUploads();
    const answers = legalRepresentative([await kept("lost.pdf")]);
    sessionStorage.removeItem("myss.accountCreation.key");

    const { result } = await renderHook(() => useAttachKeptDocuments(answers));

    await expect.poll(() => result.current.state).toBe("done");
    expect(result.current.refused).toEqual([
      {
        field: "powerOfAttorneyDocuments",
        fileName: "lost.pdf",
        message: "This browser did not keep the file while you signed in.",
      },
    ]);
    expect(uploads).toEqual([]);
  });

  it("uploads each file once, however often it renders", async () => {
    const uploads = stubUploads();
    const answers = legalRepresentative([await kept("once.pdf")]);

    const { result, rerender } = await renderHook(
      () => useAttachKeptDocuments(answers),
      {
        wrapper: StrictMode,
      },
    );
    await rerender();

    await expect.poll(() => result.current.state).toBe("done");
    expect(uploads).toEqual(["once.pdf"]);
  });

  it("uploads only the documents the context asks for", async () => {
    const uploads = stubUploads();
    const answers: AccountCreationAnswers = {
      ...openingAnswers(),
      context: "withHelper",
      helperAuthorized: "no",
      consentDocuments: [await kept("not-needed.pdf")],
    };

    const { result } = await renderHook(() => useAttachKeptDocuments(answers));

    expect(result.current).toMatchObject({ state: "done", total: 0 });
    expect(uploads).toEqual([]);
  });
});
