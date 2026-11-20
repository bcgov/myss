import { useState } from "react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { userEvent } from "@vitest/browser/context";
import { render } from "vitest-browser-react";
import { afterEach, expect, test, vi } from "vitest";

import type { AccountDocument } from "@/lib/accountContext";
import DocumentUpload from "@/widgets/registration/DocumentUpload";

const PDF_BYTES = "%PDF-1.7 test";

function pdf(name = "consent.pdf") {
  return new File([PDF_BYTES], name, { type: "application/pdf" });
}

// Stubs POST /v1/attachments: stores the file, or refuses it as the API does.
function stubUploads(refusal?: { status: number; keyword?: string }) {
  const uploads: File[] = [];
  const fetchSpy = vi
    .spyOn(window, "fetch")
    .mockImplementation(async (input, init) => {
      if (
        !String(input).endsWith("/v1/attachments") ||
        init?.method !== "POST"
      ) {
        throw new Error(`Unexpected fetch in test: ${String(input)}`);
      }
      const file = (init.body as FormData).get("file") as File;
      uploads.push(file);
      if (refusal) {
        return new Response(
          JSON.stringify({ status: refusal.status, keyword: refusal.keyword }),
          {
            status: refusal.status,
            headers: { "Content-Type": "application/problem+json" },
          },
        );
      }
      const stored = {
        id: `attachment-${uploads.length}`,
        fileName: file.name,
        contentType: file.type,
        sizeBytes: file.size,
        status: "Released",
        submissionId: null,
        uploadedAt: new Date().toISOString(),
      };
      return new Response(JSON.stringify({ payload: stored }), {
        status: 200,
        headers: { "Content-Type": "application/json" },
      });
    });
  return { uploads, fetchSpy };
}

function documents(count: number): AccountDocument[] {
  return Array.from({ length: count }, (_, i) => ({
    id: `kept-${i}`,
    fileName: `page-${i}.pdf`,
    sizeBytes: 2048,
    state: "kept",
  }));
}

interface HarnessProps {
  mode: "keep" | "upload";
  canKeep?: boolean;
  error?: string;
  initial?: AccountDocument[];
  keepFails?: boolean;
}

// Holds the list, as the page's answers do.
function Harness({
  mode,
  canKeep,
  error,
  initial = [],
  keepFails,
}: HarnessProps) {
  const [listed, setListed] = useState<AccountDocument[]>(initial);
  return (
    <DocumentUpload
      field="consentDocuments"
      mode={mode}
      canKeep={canKeep}
      error={error}
      documents={listed}
      onKeep={async (file) => {
        if (keepFails) throw new Error("quota");
        setListed((d) => [
          ...d,
          {
            id: `kept-${d.length}`,
            fileName: file.name,
            sizeBytes: file.size,
            state: "kept",
          },
        ]);
      }}
      onUploaded={(a) =>
        setListed((d) => [
          ...d,
          {
            id: a.id,
            fileName: a.fileName,
            sizeBytes: a.sizeBytes,
            state: "attached",
          },
        ])
      }
      onRemove={(doc) => setListed((d) => d.filter((x) => x.id !== doc.id))}
    />
  );
}

async function renderUpload(props: HarnessProps) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const screen = await render(
    <QueryClientProvider client={queryClient}>
      <Harness {...props} />
    </QueryClientProvider>,
  );
  const input =
    screen.container.querySelector<HTMLInputElement>('input[type="file"]');
  const live = () => screen.container.querySelector('[aria-live="polite"]');
  return { screen, input, live };
}

afterEach(() => {
  vi.restoreAllMocks();
});

test("keeps an accepted file in the tab without sending it", async () => {
  const { fetchSpy } = stubUploads();
  const { screen, input, live } = await renderUpload({ mode: "keep" });

  await userEvent.upload(input!, pdf());

  await expect.element(screen.getByText("Uploads after sign-in")).toBeVisible();
  await expect
    .element(screen.getByText("consent.pdf", { exact: true }))
    .toBeVisible();
  expect(live()?.textContent).toBe(
    "consent.pdf is ready. It is uploaded after you sign in.",
  );
  expect(fetchSpy).not.toHaveBeenCalled();
});

test.each([
  [
    "an empty file",
    new File([], "blank.pdf", { type: "application/pdf" }),
    "The selected file is empty.",
  ],
  [
    "a file over 5 MB",
    new File([PDF_BYTES, new Uint8Array(5_242_880)], "scan.pdf", {
      type: "application/pdf",
    }),
    "The file is too large. The limit is 5 MB.",
  ],
  [
    "a text file renamed as a PDF",
    new File(["just text"], "notes-renamed.pdf", { type: "application/pdf" }),
    "Only PDF, PNG and JPEG files are accepted.",
  ],
])("refuses %s before it is kept", async (_, file, message) => {
  stubUploads();
  const { screen, input, live } = await renderUpload({ mode: "keep" });

  await userEvent.upload(input!, file);

  await expect
    .poll(() => live()?.textContent)
    .toBe(`${file.name} was not attached. ${message}`);
  expect(screen.container.querySelector("ul")).toBeNull();
});

test("says the document is attached after sign-in when the tab cannot keep files", async () => {
  const { screen } = await renderUpload({ mode: "keep", canKeep: false });

  await expect
    .element(
      screen.getByText("This browser cannot keep files while you sign in", {
        exact: false,
      }),
    )
    .toBeVisible();
  expect(screen.container.querySelector('input[type="file"]')).toBeNull();
  expect(screen.container.querySelector("button")).toBeNull();
});

test("gives the general message when the tab fails to keep a file", async () => {
  const { input, live } = await renderUpload({ mode: "keep", keepFails: true });

  await userEvent.upload(input!, pdf());

  await expect
    .poll(() => live()?.textContent)
    .toBe(
      "consent.pdf was not attached. The document could not be attached. Try again later.",
    );
});

test("uploads a file after sign-in and lists it as attached", async () => {
  const { uploads } = stubUploads();
  const { screen, input, live } = await renderUpload({ mode: "upload" });

  await userEvent.upload(input!, pdf());

  await expect
    .element(screen.getByText("Attached", { exact: true }))
    .toBeVisible();
  expect(live()?.textContent).toBe("consent.pdf attached.");
  expect(uploads.map((f) => f.name)).toEqual(["consent.pdf"]);
});

test("shows the scan's message for an infected file and does not list it", async () => {
  stubUploads({ status: 422, keyword: "DOC.UPLOAD.INFECTED" });
  const { screen, input, live } = await renderUpload({ mode: "upload" });

  await userEvent.upload(input!, pdf("eicar.pdf"));

  await expect
    .poll(() => live()?.textContent)
    .toBe(
      "eicar.pdf was not attached. The file failed the virus scan and was not stored.",
    );
  expect(screen.container.querySelector("ul")).toBeNull();
});

test("gives the general message for a refusal without a known keyword", async () => {
  stubUploads({ status: 404 });
  const { input, live } = await renderUpload({ mode: "upload" });

  await userEvent.upload(input!, pdf());

  await expect
    .poll(() => live()?.textContent)
    .toBe(
      "consent.pdf was not attached. The document could not be attached. Try again later.",
    );
});

test("Remove takes a file off the list and says so", async () => {
  const { screen, live } = await renderUpload({
    mode: "keep",
    initial: documents(2),
  });

  await screen.getByRole("button", { name: "Remove page-0.pdf" }).click();

  await expect
    .element(screen.getByText("page-0.pdf", { exact: true }))
    .not.toBeInTheDocument();
  await expect
    .element(screen.getByText("page-1.pdf", { exact: true }))
    .toBeVisible();
  expect(live()?.textContent).toBe("page-0.pdf removed.");
  await expect
    .element(screen.getByRole("button", { name: "Attach document" }))
    .toHaveFocus();
});

test("keeps focus on the button while a file is checked", async () => {
  stubUploads();
  const { screen, input } = await renderUpload({ mode: "upload" });
  const button = screen.getByRole("button", { name: "Attach document" });
  (button.element() as HTMLElement).focus();

  await userEvent.upload(input!, pdf());

  await expect
    .element(screen.getByText("Attached", { exact: true }))
    .toBeVisible();
  await expect.element(button).toHaveFocus();
});

test("an attached file's removal says the stored copy stays", async () => {
  stubUploads();
  const { screen, input, live } = await renderUpload({ mode: "upload" });
  await userEvent.upload(input!, pdf());
  await expect
    .element(screen.getByText("Attached", { exact: true }))
    .toBeVisible();

  await screen.getByRole("button", { name: "Remove consent.pdf" }).click();

  expect(live()?.textContent).toBe(
    "consent.pdf removed. The stored copy is not deleted.",
  );
});

test("stops at five documents", async () => {
  const { screen } = await renderUpload({
    mode: "keep",
    initial: documents(5),
  });

  await expect
    .element(screen.getByRole("button", { name: "Attach document" }))
    .toBeDisabled();
  await expect
    .element(
      screen.getByText(
        "5 of 5 documents attached. Remove one to attach another.",
      ),
    )
    .toBeVisible();
});

test("carries the field's name and its error to the button", async () => {
  const { screen } = await renderUpload({
    mode: "keep",
    error: "Attach your signed Consent to Disclosure of Information form.",
  });

  const button = screen.getByRole("button", { name: "Attach document" });
  await expect.element(button).toHaveAttribute("id", "consentDocuments");
  await expect
    .element(button)
    .toHaveAccessibleDescription(
      "PDF, PNG or JPEG, up to 5 MB each. Up to 5 documents. They are uploaded after you sign in. Attach your signed Consent to Disclosure of Information form.",
    );
});

test("ties a refusal to the button until the next file", async () => {
  stubUploads();
  const { screen, input } = await renderUpload({ mode: "keep" });

  await userEvent.upload(
    input!,
    new File([], "blank.pdf", { type: "application/pdf" }),
  );

  await expect
    .element(screen.getByRole("button", { name: "Attach document" }))
    .toHaveAccessibleDescription(
      "PDF, PNG or JPEG, up to 5 MB each. Up to 5 documents. They are uploaded after you sign in. blank.pdf was not attached. The selected file is empty.",
    );
});
