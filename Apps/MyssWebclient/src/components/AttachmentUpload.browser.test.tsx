import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { userEvent } from "@vitest/browser/context";
import { render } from "vitest-browser-react";
import { afterEach, expect, test, vi } from "vitest";

import AttachmentUpload from "@/components/AttachmentUpload";

// Stubs the attachments API: GET serves the current list, POST appends to it
// (or rejects with a canned ProblemDetails), mirroring the real controller.
function stubAttachmentsApi(options?: {
  rejectWith?: { status: number; keyword: string; detail: string };
}) {
  const list: Array<Record<string, unknown>> = [];
  const uploads: File[] = [];
  vi.spyOn(window, "fetch").mockImplementation(async (input, init) => {
    const url = String(input);
    if (!url.endsWith("/v1/attachments")) {
      throw new Error(`Unexpected fetch in test: ${url}`);
    }
    if (init?.method === "POST") {
      const file = (init.body as FormData).get("file") as File;
      uploads.push(file);
      if (options?.rejectWith) {
        const { status, keyword, detail } = options.rejectWith;
        return new Response(JSON.stringify({ status, keyword, detail }), {
          status,
          headers: { "Content-Type": "application/problem+json" },
        });
      }
      const stored = {
        id: "99999999-8888-7777-6666-555555555555",
        fileName: file.name,
        contentType: file.type,
        sizeBytes: file.size,
        status: "Released",
        submissionId: null,
        uploadedAt: new Date().toISOString(),
      };
      list.push(stored);
      return new Response(JSON.stringify({ payload: stored }), {
        status: 200,
        headers: { "Content-Type": "application/json" },
      });
    }
    return new Response(JSON.stringify({ payload: [...list] }), {
      status: 200,
      headers: { "Content-Type": "application/json" },
    });
  });
  return uploads;
}

function renderUpload() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <AttachmentUpload />
    </QueryClientProvider>,
  );
}

function pdfFile(name: string) {
  return new File(["%PDF-1.7 test"], name, { type: "application/pdf" });
}

afterEach(() => {
  vi.restoreAllMocks();
});

test("uploads the chosen file and shows it in the list", async () => {
  const uploads = stubAttachmentsApi();
  const screen = await renderUpload();

  await expect.element(screen.getByText("None yet.")).toBeVisible();
  await userEvent.upload(
    screen.getByLabelText("Choose a file to upload"),
    pdfFile("statement.pdf"),
  );

  await expect
    .element(screen.getByText("scanned and stored", { exact: false }))
    .toBeVisible();
  await expect
    .element(screen.getByText("statement.pdf — 13 B", { exact: false }))
    .toBeVisible();

  // The request carried the file as the multipart "file" field.
  expect(uploads).toHaveLength(1);
  expect(uploads[0].name).toBe("statement.pdf");
});

test("shows the keyword-mapped message when the scan rejects the file", async () => {
  stubAttachmentsApi({
    rejectWith: {
      status: 422,
      keyword: "DOC.UPLOAD.INFECTED",
      detail: "The file was flagged by the virus scan (Test-Signature).",
    },
  });
  const screen = await renderUpload();

  await userEvent.upload(
    screen.getByLabelText("Choose a file to upload"),
    pdfFile("eicar.pdf"),
  );

  await expect
    .element(
      screen.getByText("The file failed the virus scan and was not stored."),
    )
    .toBeVisible();
  await expect.element(screen.getByText("None yet.")).toBeVisible();
});
