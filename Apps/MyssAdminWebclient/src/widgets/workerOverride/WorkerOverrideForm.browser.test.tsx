import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router";
import { render } from "vitest-browser-react";
import { afterEach, expect, test, vi } from "vitest";
import type { FormSpecPayload } from "@myss/web-common/api";

import type { WorkerOverrideApplicationPayload } from "@/api/workerOverride";
import { WORKER_OVERRIDE_KEYWORDS } from "@/api/workerOverride";
import WorkerOverrideForm from "@/widgets/workerOverride/WorkerOverrideForm";

// One worker override application (MYSS-321): the draft editable with Save
// beside the form's own Submit, each write sending the row version it was
// read at, the API's refusals shown, and once submitted the request number,
// the account and a link to the file in the review queue.

const ID = "11111111-2222-3333-4444-555555555555";
const ACCOUNT = "99999999-8888-7777-6666-555555555555";

// Two of the seeded form's fields are enough for the widget; the API's tests
// cover the six.
const spec = {
  formSpecId: "worker-override-afa",
  version: 1,
  title: "Worker override: Application for Assistance",
  spec: {
    display: "form",
    components: [
      {
        type: "textfield",
        key: "firstName",
        label: "First name",
        input: true,
        validate: { required: true },
      },
      {
        type: "textfield",
        key: "lastName",
        label: "Last name",
        input: true,
        validate: { required: true },
      },
      {
        type: "button",
        key: "submit",
        action: "submit",
        label: "Submit",
        input: true,
      },
    ],
  },
} as unknown as FormSpecPayload;

function draft(
  overrides: Partial<WorkerOverrideApplicationPayload> = {},
): WorkerOverrideApplicationPayload {
  return {
    id: ID,
    referenceNumber: "IA-11111111",
    status: "DRAFT",
    version: 1,
    formSpecId: "worker-override-afa",
    formSpecVersion: 1,
    createdAt: "2026-11-03T10:00:00Z",
    updatedAt: "2026-11-03T10:00:00Z",
    submittedAt: null,
    answers: {},
    spec,
    accountId: null,
    ...overrides,
  };
}

type Answer = { status: number; body: unknown };

interface Stub {
  /** What the API holds now; GET serves it, successful writes move it. */
  application: WorkerOverrideApplicationPayload;
  /** A canned answer for the next write; consumed once. */
  nextWrite?: Answer;
}

function json(status: number, body: unknown) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

/** Write responses do not repeat the spec; only the read by id does. */
function withoutSpec(application: WorkerOverrideApplicationPayload) {
  const copy: Partial<WorkerOverrideApplicationPayload> = { ...application };
  delete copy.spec;
  return copy;
}

/** A stateful stand-in for the worker override API. */
function stubApi(stub: Stub) {
  const writes: Array<{ route: string; body: { version: number; answers: Record<string, unknown> } }> = [];
  vi.spyOn(window, "fetch").mockImplementation(async (input, init) => {
    const url = String(input);
    const method = init?.method ?? "GET";
    const base = `/v1/intake/worker-override/applications/${ID}`;

    if (url.endsWith(base) && method === "GET") {
      return json(200, { payload: stub.application });
    }
    const route = /\/(answers|submit)$/.exec(url)?.[1];
    if (route && url.endsWith(`${base}/${route}`)) {
      const body = JSON.parse(String(init?.body));
      writes.push({ route, body });
      if (stub.nextWrite) {
        const answer = stub.nextWrite;
        stub.nextWrite = undefined;
        return json(answer.status, answer.body);
      }
      stub.application = {
        ...stub.application,
        answers: body.answers,
        version: stub.application.version + 1,
        updatedAt: "2026-11-03T10:05:00Z",
        ...(route === "submit"
          ? {
              status: "SUBMITTED" as const,
              submittedAt: "2026-11-03T10:05:00Z",
              accountId: ACCOUNT,
            }
          : {}),
      };
      return json(200, { payload: withoutSpec(stub.application) });
    }
    if (url.includes("/v1/intake/review/applications")) {
      return json(200, { payload: [] });
    }
    throw new Error(`Unexpected fetch in test: ${method} ${url}`);
  });
  return writes;
}

function renderForm() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[`/worker-override/${ID}`]}>
        <WorkerOverrideForm applicationId={ID} />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

afterEach(() => {
  vi.restoreAllMocks();
});

test("saves a half-filled draft with the version it was read at", async () => {
  const writes = stubApi({ application: draft() });
  const screen = await renderForm();

  await screen.getByRole("textbox", { name: /First name/ }).fill("Ada");
  await screen.getByRole("button", { name: "Save draft" }).click();

  await expect.element(screen.getByText(/Saved/)).toBeVisible();
  expect(writes).toHaveLength(1);
  expect(writes[0].route).toBe("answers");
  expect(writes[0].body.version).toBe(1);
  expect(writes[0].body.answers.firstName).toBe("Ada");
});

test("submits, then shows the request number but not the account or a queue link", async () => {
  const writes = stubApi({ application: draft() });
  const screen = await renderForm();

  await screen.getByRole("textbox", { name: /First name/ }).fill("Ada");
  await screen.getByRole("textbox", { name: /Last name/ }).fill("Lovelace");
  await screen.getByRole("button", { name: "Submit" }).click();

  await expect
    .element(screen.getByRole("heading", { name: "Application submitted" }))
    .toBeVisible();
  await expect.element(screen.getByText("IA-11111111")).toBeVisible();
  await expect.element(screen.getByText(ACCOUNT)).not.toBeInTheDocument();
  await expect
    .element(screen.getByRole("link", { name: /review queue/ }))
    .not.toBeInTheDocument();
  await expect
    .element(screen.getByRole("textbox", { name: /First name/ }))
    .toBeDisabled();
  expect(writes.map((w) => w.route)).toEqual(["submit"]);
  expect(writes[0].body.version).toBe(1);
  expect(writes[0].body.answers).toMatchObject({
    firstName: "Ada",
    lastName: "Lovelace",
  });
});

test("does not send a submit the form itself refuses", async () => {
  const writes = stubApi({ application: draft() });
  const screen = await renderForm();

  await screen.getByRole("button", { name: "Submit" }).click();

  // Form.io's own checks block it; the summary lists what is missing.
  await expect.element(screen.getByRole("alert")).toBeVisible();
  await expect
    .element(screen.getByRole("button", { name: /First name/ }).first())
    .toBeVisible();
  expect(writes).toHaveLength(0);
});

test("lists the field errors the API refuses a submit with", async () => {
  const writes = stubApi({
    application: draft(),
    nextWrite: {
      status: 422,
      body: {
        payload: [
          {
            field: "lastName",
            keyword: "FORM.FIELD.REQUIRED",
            message: "Enter the client's last name.",
          },
        ],
      },
    },
  });
  const screen = await renderForm();

  await screen.getByRole("textbox", { name: /First name/ }).fill("Ada");
  await screen.getByRole("textbox", { name: /Last name/ }).fill("L");
  await screen.getByRole("button", { name: "Submit" }).click();

  await expect
    .element(screen.getByText("Enter the client's last name.").first())
    .toBeVisible();
  await expect
    .element(screen.getByRole("heading", { name: "Application submitted" }))
    .not.toBeInTheDocument();
  expect(writes).toHaveLength(1);
});

test("says so when another worker saved first", async () => {
  stubApi({
    application: draft(),
    nextWrite: {
      status: 409,
      body: { keyword: WORKER_OVERRIDE_KEYWORDS.conflict, currentVersion: 2 },
    },
  });
  const screen = await renderForm();

  await screen.getByRole("textbox", { name: /First name/ }).fill("Ada");
  await screen.getByRole("button", { name: "Save draft" }).click();

  await expect
    .element(screen.getByRole("alert"))
    .toHaveTextContent(/Another worker, tab or window saved a newer version/);
});

test("asks for an update when the application was returned", async () => {
  stubApi({
    application: draft({
      status: "RETURNED",
      version: 2,
      submittedAt: "2026-11-03T10:05:00Z",
      accountId: ACCOUNT,
      answers: { firstName: "Ada", lastName: "Lovelace" },
    }),
  });
  const screen = await renderForm();

  await expect
    .element(screen.getByText(/asked for more information/))
    .toBeVisible();
  await expect
    .element(screen.getByRole("textbox", { name: /First name/ }))
    .toHaveValue("Ada");
  await expect
    .element(screen.getByRole("textbox", { name: /First name/ }))
    .toBeEnabled();
});

test("says when the application does not exist", async () => {
  vi.spyOn(window, "fetch").mockResolvedValue(json(404, {}));
  const screen = await renderForm();

  await expect
    .element(screen.getByRole("alert"))
    .toHaveTextContent("This worker override application could not be found.");
});
