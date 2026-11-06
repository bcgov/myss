import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router";
import { render } from "vitest-browser-react";
import { afterEach, beforeAll, beforeEach, describe, expect, test, vi } from "vitest";
import { page } from "vitest/browser";

import type { FormSpecPayload } from "@/api/forms";
import type { ApplicationPayload } from "@/api/intake";
import ApplicationForm from "@/widgets/intake/ApplicationForm";
import { loadPageStyles } from "@/testing/pageStyles";

// The draft edits and saves without Form.io's client validation getting in
// the way, submits through the spec's own button, and turns read-only with
// an acknowledgement once the API says SUBMITTED. Every refusal the API can
// give a draft is shown in place.

const ID = "11111111-2222-3333-4444-555555555555";

// The seeded income-assistance-poc v1 spec (MyssContent form-spec-seed-data).
const spec = {
  formSpecId: "income-assistance-poc",
  version: 1,
  title: "Income Assistance application",
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
        key: "middleName",
        label: "Middle name (if any)",
        input: true,
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
  overrides: Partial<ApplicationPayload> = {},
): ApplicationPayload {
  return {
    id: ID,
    referenceNumber: "IA-11111111",
    status: "DRAFT",
    version: 1,
    formSpecId: "income-assistance-poc",
    formSpecVersion: 1,
    answers: { firstName: "Ada" },
    createdAt: "2026-09-28T10:00:00Z",
    updatedAt: "2026-09-28T10:00:00Z",
    submittedAt: null,
    spec,
    ...overrides,
  };
}

type Answer = { status: number; body: unknown };

interface Stub {
  /** What the API holds now; GET serves it, successful writes move it. */
  application: ApplicationPayload;
  /** A canned answer for the next PUT; consumed once, then saves succeed. */
  nextSave?: Answer;
  /** A canned answer for the next POST /submit; consumed once. */
  nextSubmit?: Answer;
  nextWithdraw?: Answer;
}

function json(status: number, body: unknown) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

/** Write responses do not repeat the archived spec; only the read by id does. */
function withoutSpec(application: ApplicationPayload) {
  const copy: Partial<ApplicationPayload> = { ...application };
  delete copy.spec;
  return copy;
}

/**
 * A stateful stand-in for the intake API: reads serve the current
 * application, a successful save bumps its version and stores the answers, a
 * successful submit flips it to SUBMITTED. Canned refusals go out once.
 */
function stubApi(stub: Stub) {
  const writes: Array<{ method: string; url: string; body: unknown }> = [];
  vi.spyOn(window, "fetch").mockImplementation(async (input, init) => {
    const url = String(input);
    const method = init?.method ?? "GET";
    const base = `/v1/intake/applications/${ID}`;

    if (url.endsWith(base) && method === "GET") {
      return json(200, { payload: stub.application });
    }
    if (url.endsWith(`${base}/answers`) && method === "PUT") {
      const body = JSON.parse(String(init?.body)) as {
        version: number;
        answers: Record<string, unknown>;
      };
      writes.push({ method, url, body });
      if (stub.nextSave) {
        const answer = stub.nextSave;
        stub.nextSave = undefined;
        return json(answer.status, answer.body);
      }
      stub.application = {
        ...stub.application,
        version: stub.application.version + 1,
        answers: body.answers as ApplicationPayload["answers"],
        updatedAt: "2026-09-28T10:05:00Z",
      };
      return json(200, { payload: withoutSpec(stub.application) });
    }
    if (url.endsWith(`${base}/submit`) && method === "POST") {
      const body = JSON.parse(String(init?.body));
      writes.push({ method, url, body });
      if (stub.nextSubmit) {
        const answer = stub.nextSubmit;
        stub.nextSubmit = undefined;
        return json(answer.status, answer.body);
      }
      stub.application = {
        ...stub.application,
        status:
          stub.application.status === "RETURNED" ? "RESUBMITTED" : "SUBMITTED",
        answers: body.answers,
        submittedAt: stub.application.submittedAt ?? "2026-09-28T11:00:00Z",
      };
      return json(200, { payload: withoutSpec(stub.application) });
    }
    if (url.endsWith(`${base}/withdraw`) && method === "POST") {
      writes.push({ method, url, body: init?.body });
      if (stub.nextWithdraw) {
        const answer = stub.nextWithdraw;
        stub.nextWithdraw = undefined;
        return json(answer.status, answer.body);
      }
      stub.application = { ...stub.application, status: "WITHDRAWN" };
      return json(200, { payload: withoutSpec(stub.application) });
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
      <MemoryRouter>
        <ApplicationForm applicationId={ID} />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

afterEach(() => {
  vi.restoreAllMocks();
});

test.each(["DRAFT", "SUBMITTED", "RETURNED"] as const)(
  "%s can confirm withdrawal and switches to a persisted read-only Withdrawn status",
  async (status) => {
    const stub: Stub = { application: draft({ status }) };
    const writes = stubApi(stub);
    const screen = await renderForm();

    await screen.getByRole("button", { name: /Withdraw application/ }).click();
    const dialog = screen.getByRole("alertdialog", { name: "Withdraw application" });
    await expect.element(dialog).toHaveTextContent(
      "Your application will no longer be considered for assistance. Please confirm you no longer need assistance?",
    );
    await expect.element(dialog.getByRole("button", { name: "Cancel" })).toHaveFocus();
    expect(writes).toHaveLength(0);
    await dialog.getByRole("button", { name: "Confirm" }).click();

    await expect.element(screen.getByRole("gridcell", { name: "Withdrawn" })).toBeVisible();
    await expect.element(dialog).not.toBeInTheDocument();
    await expect.element(screen.getByText("Application withdrawn.", { exact: true })).toHaveFocus();
    await expect.element(screen.getByRole("button", { name: /Withdraw application/ })).not.toBeInTheDocument();
    await expect.element(screen.getByRole("button", { name: "Save" })).not.toBeInTheDocument();
    await expect.element(screen.getByRole("textbox", { name: "First name" })).toBeDisabled();
    expect(writes).toHaveLength(1);
    expect(writes[0].url).toContain("/withdraw");
    expect(writes[0].body).toBeUndefined();
    expect(stub.application.status).toBe("WITHDRAWN");
  },
);

test("Cancel closes withdrawal without changing the page or unsaved answers", async () => {
  const writes = stubApi({ application: draft() });
  const screen = await renderForm();
  await screen.getByRole("textbox", { name: "First name" }).fill("Augusta");
  const withdraw = screen.getByRole("button", { name: /Withdraw application/ });
  await withdraw.click();
  await screen.getByRole("button", { name: "Cancel" }).click();
  await expect.element(screen.getByRole("alertdialog")).not.toBeInTheDocument();
  await expect.element(withdraw).toHaveFocus();
  await expect.element(screen.getByRole("textbox", { name: "First name" })).toHaveValue("Augusta");
  expect(writes).toHaveLength(0);
});

test.each([
  "UNDER_REVIEW", "RESUBMITTED", "ESCALATED", "ACCEPTED",
  "DENIED", "PROMOTION_FAILED", "WITHDRAWN",
] as const)("does not offer withdrawal for %s", async (status) => {
  stubApi({ application: draft({ status }) });
  const screen = await renderForm();
  await expect.element(screen.getByRole("heading", { name: /Application/ })).toBeVisible();
  await expect.element(screen.getByRole("button", { name: /Withdraw application/ })).not.toBeInTheDocument();
});

test("withdrawal errors remain visible and can be retried", async () => {
  const stub: Stub = {
    application: draft(),
    nextWithdraw: { status: 503, body: { detail: "Service unavailable" } },
  };
  const writes = stubApi(stub);
  const screen = await renderForm();
  await screen.getByRole("button", { name: /Withdraw application/ }).click();
  await screen.getByRole("button", { name: "Confirm" }).click();
  await expect.element(screen.getByRole("alert")).toHaveTextContent(
    "Could not withdraw the application: Service unavailable",
  );
  expect(stub.application.status).toBe("DRAFT");
  await screen.getByRole("button", { name: "Confirm" }).click();
  await expect.element(screen.getByRole("gridcell", { name: "Withdrawn" })).toBeVisible();
  expect(writes).toHaveLength(2);
});

test("a withdrawal conflict refreshes the status and does not report success", async () => {
  const stub: Stub = {
    application: draft({ status: "SUBMITTED" }),
    nextWithdraw: { status: 409, body: { detail: "Its current status does not allow this action." } },
  };
  stubApi(stub);
  const screen = await renderForm();
  await screen.getByRole("button", { name: /Withdraw application/ }).click();
  stub.application = { ...stub.application, status: "UNDER_REVIEW" };
  await screen.getByRole("button", { name: "Confirm" }).click();
  await expect.element(screen.getByRole("alert")).toHaveTextContent(/Could not withdraw/);
  await screen.getByRole("button", { name: "Cancel" }).click();
  await expect.element(screen.getByRole("gridcell", { name: "Under Review" })).toBeVisible();
  await expect.element(screen.getByText("Application withdrawn.", { exact: true })).not.toBeInTheDocument();
  await expect.element(screen.getByRole("button", { name: /Withdraw application/ })).not.toBeInTheDocument();
});

test("renders the draft's saved answers and Save sends them without client validation", async () => {
  const stub: Stub = { application: draft() };
  const writes = stubApi(stub);
  const screen = await renderForm();

  const firstName = screen.getByRole("textbox", { name: "First name" });
  await expect.element(firstName).toHaveValue("Ada");
  await expect.element(firstName).toBeEnabled();

  // Last name is required by the spec, and still empty: a save must go
  // through anyway, because a partial draft is the point of saving.
  await screen.getByRole("button", { name: "Save" }).click();

  await expect.element(screen.getByText(/^Saved/)).toBeVisible();
  expect(writes).toHaveLength(1);
  expect(writes[0].method).toBe("PUT");
  expect(writes[0].body).toMatchObject({
    version: 1,
    answers: { firstName: "Ada" },
  });

  // The next save carries the version the first one came back with.
  await screen.getByRole("textbox", { name: "Last name" }).fill("Lovelace");
  await screen.getByRole("button", { name: "Save" }).click();

  await expect.poll(() => writes.length).toBe(2);
  expect(writes[1].body).toMatchObject({
    version: 2,
    answers: { firstName: "Ada", lastName: "Lovelace" },
  });
});

test("submitting posts the answers and switches to the focused acknowledgement, read-only", async () => {
  const stub: Stub = { application: draft() };
  const writes = stubApi(stub);
  const screen = await renderForm();

  await screen.getByRole("textbox", { name: "Last name" }).fill("Lovelace");
  await screen.getByRole("button", { name: "Submit" }).click();

  const heading = screen.getByRole("heading", {
    name: "Application submitted",
  });
  await expect.element(heading).toBeVisible();
  await expect.element(heading).toHaveFocus();
  expect(writes.map((write) => write.method)).toEqual(["POST"]);
  expect(writes[0].body).toMatchObject({
    version: 1,
    answers: { firstName: "Ada", lastName: "Lovelace" },
  });

  // Read-only: values shown, editing disabled, no way to write again.
  const lastName = screen.getByRole("textbox", { name: "Last name" });
  await expect.element(lastName).toHaveValue("Lovelace");
  await expect.element(lastName).toBeDisabled();
  await expect
    .element(screen.getByRole("button", { name: "Submit" }))
    .not.toBeInTheDocument();
  await expect
    .element(screen.getByRole("button", { name: "Save" }))
    .not.toBeInTheDocument();
});

test("an already submitted application renders read-only with no save or submit control", async () => {
  stubApi({
    application: draft({
      status: "SUBMITTED",
      answers: { firstName: "Ada", lastName: "Lovelace" },
      submittedAt: "2026-09-28T11:00:00Z",
    }),
  });
  const screen = await renderForm();

  await expect
    .element(screen.getByRole("heading", { name: "Application submitted" }))
    .toBeVisible();
  await expect.element(screen.getByText("IA-11111111")).toBeVisible();

  const firstName = screen.getByRole("textbox", { name: "First name" });
  await expect.element(firstName).toHaveValue("Ada");
  await expect.element(firstName).toBeDisabled();
  await expect
    .element(screen.getByRole("textbox", { name: "Last name" }))
    .toBeDisabled();
  await expect
    .element(screen.getByRole("button", { name: "Submit" }))
    .not.toBeInTheDocument();
  await expect
    .element(screen.getByRole("button", { name: "Save" }))
    .not.toBeInTheDocument();
});

test("a refused submission lists every reason, focuses the field on request, and can be sent again", async () => {
  const stub: Stub = {
    application: draft(),
    nextSubmit: {
      status: 422,
      body: {
        payload: [
          {
            field: "lastName",
            keyword: "FORM.FIELD.REQUIRED",
            message: "Last name is required",
          },
        ],
      },
    },
  };
  const writes = stubApi(stub);
  const screen = await renderForm();

  // Form.io's own required check would stop the submit before the API saw
  // it, so give it something; the API is the one refusing here.
  await screen.getByRole("textbox", { name: "Last name" }).fill("x");
  await screen.getByRole("button", { name: "Submit" }).click();

  await expect.element(screen.getByText("There is a problem")).toBeVisible();
  await screen.getByRole("button", { name: "Last name is required" }).click();
  const lastName = screen.getByRole("textbox", { name: "Last name" });
  await expect.element(lastName).toHaveFocus();

  // Editing the field clears the server error, and the next submit goes out.
  await lastName.fill("Lovelace");
  await screen.getByRole("button", { name: "Submit" }).click();

  await expect
    .element(screen.getByRole("heading", { name: "Application submitted" }))
    .toBeVisible();
  expect(writes).toHaveLength(2);
});

test("a stale save shows the conflict notice and the next save carries the fresh version", async () => {
  const stub: Stub = {
    application: draft(),
    nextSave: {
      status: 409,
      body: {
        status: 409,
        title: "The application changed; reload and retry.",
        keyword: "INTAKE.APPLICATION.CONFLICT",
        currentVersion: 3,
      },
    },
  };
  const writes = stubApi(stub);
  const screen = await renderForm();

  await screen.getByRole("textbox", { name: "Last name" }).fill("Lovelace");
  // The other tab moved the row on; the refetch after the 409 sees this.
  stub.application = draft({ version: 3, answers: { firstName: "Grace" } });
  await screen.getByRole("button", { name: "Save" }).click();

  await expect
    .element(screen.getByRole("alert"))
    .toHaveTextContent(/Another tab saved a newer version/);
  expect(writes[0].body).toMatchObject({ version: 1 });

  // What was typed here is kept, and goes out against the current version.
  await expect
    .element(screen.getByRole("textbox", { name: "Last name" }))
    .toHaveValue("Lovelace");
  await screen.getByRole("button", { name: "Save" }).click();

  await expect.poll(() => writes.length).toBe(2);
  expect(writes[1].body).toMatchObject({
    version: 3,
    answers: { lastName: "Lovelace" },
  });
  await expect.element(screen.getByText(/^Saved/)).toBeVisible();
});

test("a missing application says so instead of failing silently", async () => {
  vi.spyOn(window, "fetch").mockImplementation(async () =>
    json(404, { status: 404, title: "Not found" }),
  );
  const screen = await renderForm();

  await expect
    .element(screen.getByRole("alert"))
    .toHaveTextContent("This application could not be found.");
});

test("a returned application is editable again and resubmits", async () => {
  const stub: Stub = {
    application: draft({
      status: "RETURNED",
      version: 2,
      answers: { firstName: "Ada", lastName: "Lovelace" },
      submittedAt: "2026-09-28T11:00:00Z",
    }),
  };
  const writes = stubApi(stub);
  const screen = await renderForm();

  await expect
    .element(screen.getByText(/We need more information/))
    .toBeVisible();
  const lastName = screen.getByRole("textbox", { name: "Last name" });
  await expect.element(lastName).toHaveValue("Lovelace");
  await expect.element(lastName).toBeEnabled();

  await lastName.fill("King");
  await screen.getByRole("button", { name: "Submit" }).click();

  const heading = screen.getByRole("heading", {
    name: "Application resubmitted",
  });
  await expect.element(heading).toBeVisible();
  await expect.element(heading).toHaveFocus();
  await expect
    .element(screen.getByRole("gridcell", { name: "Re-submitted" }))
    .toBeVisible();
  expect(writes[0].body).toMatchObject({
    version: 2,
    answers: { firstName: "Ada", lastName: "King" },
  });
  await expect
    .element(screen.getByRole("button", { name: "Save" }))
    .not.toBeInTheDocument();
});

test.each([
  ["UNDER_REVIEW", "Under Review"],
  ["ESCALATED", "Review"],
  ["ACCEPTED", "Accepted"],
  ["DENIED", "Denied"],
  ["PROMOTION_FAILED", "Promotion Failed"],
] as const)(
  "a %s application is read-only with its status",
  async (status, label) => {
    stubApi({
      application: draft({
        status,
        answers: { firstName: "Ada", lastName: "Lovelace" },
        submittedAt: "2026-09-28T11:00:00Z",
      }),
    });
    const screen = await renderForm();

    await expect
      .element(screen.getByRole("heading", { name: "Application status" }))
      .toBeVisible();
    await expect
      .element(screen.getByRole("gridcell", { name: label }))
      .toBeVisible();
    await expect
      .element(screen.getByRole("textbox", { name: "First name" }))
      .toBeDisabled();
    await expect
      .element(screen.getByRole("button", { name: "Save" }))
      .not.toBeInTheDocument();
    await expect
      .element(screen.getByRole("button", { name: "Submit" }))
      .not.toBeInTheDocument();
  },
);

describe("layout", () => {
  let initial = { width: 0, height: 0 };

  beforeAll(loadPageStyles);

  beforeEach(() => {
    initial = { width: window.innerWidth, height: window.innerHeight };
  });

  afterEach(async () => {
    await page.viewport(initial.width, initial.height);
  });

  test("fits a phone, with Save and Submit on the screen", async () => {
    await page.viewport(320, 640);
    stubApi({ application: draft() });
    const screen = await renderForm();
    await expect
      .element(screen.getByRole("textbox", { name: "First name" }))
      .toHaveValue("Ada");

    const root = document.documentElement;
    for (const name of ["Save", "Submit"]) {
      const button = screen.getByRole("button", { name });
      await expect.element(button).toBeVisible();
      expect(button.element().getBoundingClientRect().right).toBeLessThanOrEqual(
        root.clientWidth,
      );
    }
    expect(root.scrollWidth).toBeLessThanOrEqual(root.clientWidth);
  });
});
