import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render } from "vitest-browser-react";
import { afterEach, expect, test, vi } from "vitest";
import { userEvent } from "vitest/browser";

import type { AttachmentPayload } from "@/api/attachments";
import ApplicationDocuments from "@/widgets/intake/ApplicationDocuments";

// Supporting documents on an application: added with the button (file
// picker) or by dropping files, listed, removed while editable, and
// read-only once submitted. API refusals show in place.

const ID = "11111111-2222-3333-4444-555555555555";
const BASE = `/v1/intake/applications/${ID}/attachments`;

function json(status: number, body: unknown) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

function pdf(name: string) {
  return new File(["%PDF-1.7 test"], name, { type: "application/pdf" });
}

interface Stub {
  documents: AttachmentPayload[];
  /** A canned answer for the next upload; consumed once. */
  nextUpload?: { status: number; body: unknown };
}

function stubApi(stub: Stub) {
  const uploads: string[] = [];
  vi.spyOn(window, "fetch").mockImplementation(async (input, init) => {
    const url = String(input);
    const method = init?.method ?? "GET";
    if (url.endsWith(BASE) && method === "GET") {
      return json(200, { payload: stub.documents });
    }
    if (url.endsWith(BASE) && method === "POST") {
      const file = (init?.body as FormData).get("file") as File;
      uploads.push(file.name);
      if (stub.nextUpload) {
        const answer = stub.nextUpload;
        stub.nextUpload = undefined;
        return json(answer.status, answer.body);
      }
      const doc: AttachmentPayload = {
        id: `doc-${uploads.length}`,
        fileName: file.name,
        contentType: file.type,
        sizeBytes: file.size,
        status: "Released",
        submissionId: ID,
        uploadedAt: "2026-11-20T10:00:00Z",
      };
      stub.documents = [...stub.documents, doc];
      return json(200, { payload: doc });
    }
    const removed = url.match(/\/attachments\/([^/]+)$/);
    if (removed && method === "DELETE") {
      stub.documents = stub.documents.filter((d) => d.id !== removed[1]);
      return new Response(null, { status: 204 });
    }
    throw new Error(`Unexpected fetch in test: ${method} ${url}`);
  });
  return uploads;
}

function renderDocuments(editable: boolean) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <ApplicationDocuments applicationId={ID} editable={editable} />
    </QueryClientProvider>,
  );
}

function fileInput(container: HTMLElement) {
  return container.querySelector<HTMLInputElement>('input[type="file"]')!;
}

afterEach(() => {
  vi.restoreAllMocks();
});

test("a draft has an Upload document button that opens a multi-file picker for the accepted types", async () => {
  stubApi({ documents: [] });
  const screen = await renderDocuments(true);

  await expect
    .element(screen.getByRole("button", { name: "Upload document" }))
    .toBeVisible();
  const input = fileInput(screen.container);
  expect(input.multiple).toBe(true);
  expect(input.accept).toBe("application/pdf,image/png,image/jpeg");
});

test("chosen files are uploaded and listed", async () => {
  const stub: Stub = { documents: [] };
  const uploads = stubApi(stub);
  const screen = await renderDocuments(true);

  await userEvent.upload(fileInput(screen.container), [
    pdf("paystub.pdf"),
    pdf("lease.pdf"),
  ]);

  const list = screen.getByRole("list", { name: "Uploaded documents" });
  await expect.element(list).toHaveTextContent("paystub.pdf");
  await expect.element(list).toHaveTextContent("lease.pdf");
  expect(uploads).toEqual(["paystub.pdf", "lease.pdf"]);
  await expect
    .element(screen.getByText("paystub.pdf uploaded. lease.pdf uploaded."))
    .toBeVisible();
});

test("files dropped on the area are uploaded", async () => {
  const stub: Stub = { documents: [] };
  const uploads = stubApi(stub);
  const screen = await renderDocuments(true);

  const area = screen.getByTestId("document-drop-area").element();
  const transfer = new DataTransfer();
  transfer.items.add(pdf("bank-statement.pdf"));
  area.dispatchEvent(
    new DragEvent("dragover", {
      dataTransfer: transfer,
      bubbles: true,
      cancelable: true,
    }),
  );
  area.dispatchEvent(
    new DragEvent("drop", {
      dataTransfer: transfer,
      bubbles: true,
      cancelable: true,
    }),
  );

  await expect
    .element(screen.getByRole("list", { name: "Uploaded documents" }))
    .toHaveTextContent("bank-statement.pdf");
  expect(uploads).toEqual(["bank-statement.pdf"]);
});

test("a file of the wrong type is refused before it is sent", async () => {
  const uploads = stubApi({ documents: [] });
  const screen = await renderDocuments(true);

  await userEvent.upload(
    fileInput(screen.container),
    new File(["hello"], "notes.txt", { type: "text/plain" }),
  );

  await expect
    .element(
      screen.getByText(
        "notes.txt was not uploaded. Only PDF, PNG and JPEG files are accepted.",
      ),
    )
    .toBeVisible();
  expect(uploads).toEqual([]);
});

test("a file the scan flags shows the refusal", async () => {
  stubApi({
    documents: [],
    nextUpload: { status: 422, body: { keyword: "DOC.UPLOAD.INFECTED" } },
  });
  const screen = await renderDocuments(true);

  await userEvent.upload(fileInput(screen.container), pdf("bad.pdf"));

  await expect
    .element(
      screen.getByText(
        "bad.pdf was not uploaded. The file failed the virus scan and was not stored.",
      ),
    )
    .toBeVisible();
});

test("a document can be removed from a draft", async () => {
  const stub: Stub = {
    documents: [
      {
        id: "doc-a",
        fileName: "paystub.pdf",
        contentType: "application/pdf",
        sizeBytes: 2048,
        status: "Released",
        submissionId: ID,
        uploadedAt: "2026-11-20T10:00:00Z",
      },
    ],
  };
  stubApi(stub);
  const screen = await renderDocuments(true);

  await screen.getByRole("button", { name: "Remove paystub.pdf" }).click();

  await expect.element(screen.getByText("paystub.pdf removed.")).toBeVisible();
  await expect
    .element(screen.getByRole("list", { name: "Uploaded documents" }))
    .not.toBeInTheDocument();
  await expect
    .element(screen.getByRole("button", { name: "Upload document" }))
    .toHaveFocus();
});

test("a submitted application lists its documents read-only", async () => {
  stubApi({
    documents: [
      {
        id: "doc-a",
        fileName: "paystub.pdf",
        contentType: "application/pdf",
        sizeBytes: 2048,
        status: "Released",
        submissionId: ID,
        uploadedAt: "2026-11-20T10:00:00Z",
      },
    ],
  });
  const screen = await renderDocuments(false);

  await expect
    .element(screen.getByRole("list", { name: "Uploaded documents" }))
    .toHaveTextContent("paystub.pdf");
  await expect
    .element(screen.getByRole("button", { name: "Upload document" }))
    .not.toBeInTheDocument();
  await expect
    .element(screen.getByRole("button", { name: "Remove paystub.pdf" }))
    .not.toBeInTheDocument();
});

test("a file dropped elsewhere on the page is ignored rather than opened", async () => {
  const uploads = stubApi({ documents: [] });
  await renderDocuments(true);

  const transfer = new DataTransfer();
  transfer.items.add(pdf("stray.pdf"));
  const drop = new DragEvent("drop", {
    dataTransfer: transfer,
    bubbles: true,
    cancelable: true,
  });
  document.body.dispatchEvent(drop);

  expect(drop.defaultPrevented).toBe(true);
  expect(uploads).toEqual([]);
});
