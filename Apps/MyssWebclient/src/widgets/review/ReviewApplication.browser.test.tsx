import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router";
import { render } from "vitest-browser-react";
import { afterEach, expect, test, vi } from "vitest";

import type { FormSpecPayload } from "@/api/forms";
import type { ReviewApplicationPayload } from "@/api/review";
import { REVIEW_KEYWORDS } from "@/api/review";
import ReviewApplication from "@/widgets/review/ReviewApplication";

// One application under review: the submitted answers read-only, the buttons
// the server allows the caller, internal notes for the assignee, the history,
// and each write sending the stream version it was decided against. A 409
// reloads the file instead of pretending.

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

function submitted(
  overrides: Partial<ReviewApplicationPayload> = {},
): ReviewApplicationPayload {
  return {
    id: ID,
    referenceNumber: "IA-11111111",
    status: "SUBMITTED",
    submittedAt: "2026-09-28T11:00:00Z",
    streamVersion: 1,
    answers: { firstName: "Ada", lastName: "Lovelace" },
    formSpecId: "income-assistance-poc",
    formSpecVersion: 1,
    spec,
    availableActions: ["Review"],
    assignee: null,
    inQueue: true,
    canAddNote: false,
    notes: [],
    history: [
      {
        code: "SUBMIT",
        actorKind: "APPLICANT",
        idir: null,
        occurredAt: "2026-09-28T11:00:00Z",
      },
    ],
    ...overrides,
  };
}

const ASSIGNEE_ACTIONS: ReviewApplicationPayload["availableActions"] = [
  "RequestMoreInfo",
  "Escalate",
  "Deny",
  "Accept",
  "Release",
];

/** A file the mock worker (MWORKER) has picked up. */
function underReview(
  overrides: Partial<ReviewApplicationPayload> = {},
): ReviewApplicationPayload {
  return submitted({
    status: "UNDER_REVIEW",
    streamVersion: 2,
    assignee: "MWORKER",
    inQueue: false,
    canAddNote: true,
    availableActions: ASSIGNEE_ACTIONS,
    ...overrides,
  });
}

type Answer = { status: number; body: unknown };

interface Stub {
  /** What the API holds now; GET serves it, successful actions move it. */
  application: ReviewApplicationPayload;
  /** A canned answer for the next action; consumed once. */
  nextAction?: Answer;
  /** When set, a note's response waits for this before going out. */
  holdNote?: Promise<void>;
}

function json(status: number, body: unknown) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

/** Action responses do not repeat the archived spec; only the read by id does. */
function withoutSpec(application: ReviewApplicationPayload) {
  const copy: Partial<ReviewApplicationPayload> = { ...application };
  delete copy.spec;
  return copy;
}

const NEXT: Record<
  string,
  {
    status: ReviewApplicationPayload["status"];
    actions: ReviewApplicationPayload["availableActions"];
  }
> = {
  review: { status: "UNDER_REVIEW", actions: ASSIGNEE_ACTIONS },
  "request-more-info": { status: "RETURNED", actions: [] },
  escalate: { status: "ESCALATED", actions: [] },
  accept: { status: "ACCEPTED", actions: [] },
  deny: { status: "DENIED", actions: [] },
  release: { status: "SUBMITTED", actions: ["Review"] },
};

/**
 * A stateful stand-in for the review API: reads serve the current
 * application, a successful action moves its status, available actions and
 * stream version. Canned refusals go out once.
 */
function stubApi(stub: Stub) {
  const actions: Array<{ route: string; body: unknown }> = [];
  vi.spyOn(window, "fetch").mockImplementation(async (input, init) => {
    const url = String(input);
    const method = init?.method ?? "GET";
    const base = `/v1/intake/review/applications/${ID}`;

    if (url.endsWith(base) && method === "GET") {
      return json(200, { payload: stub.application });
    }
    if (url.endsWith(`${base}/notes`) && method === "POST") {
      const body = JSON.parse(String(init?.body));
      actions.push({ route: "notes", body });
      if (stub.holdNote) await stub.holdNote;
      if (stub.nextAction) {
        const answer = stub.nextAction;
        stub.nextAction = undefined;
        return json(answer.status, answer.body);
      }
      stub.application = {
        ...stub.application,
        notes: [
          ...stub.application.notes,
          {
            text: body.text,
            author: "MWORKER",
            createdAt: "2026-09-28T12:00:00Z",
          },
        ],
        streamVersion: stub.application.streamVersion + 1,
      };
      return json(200, { payload: withoutSpec(stub.application) });
    }
    const action =
      /\/(review|request-more-info|escalate|accept|deny|release)$/.exec(
        url,
      )?.[1];
    if (action && url.endsWith(`${base}/${action}`) && method === "POST") {
      const body = JSON.parse(String(init?.body));
      actions.push({ route: action, body });
      if (stub.nextAction) {
        const answer = stub.nextAction;
        stub.nextAction = undefined;
        return json(answer.status, answer.body);
      }
      const held = action === "review";
      stub.application = {
        ...stub.application,
        status: NEXT[action].status,
        availableActions: NEXT[action].actions,
        assignee: held ? "MWORKER" : null,
        canAddNote: held,
        inQueue: ["RETURNED", "SUBMITTED"].includes(NEXT[action].status),
        streamVersion: stub.application.streamVersion + 1,
      };
      return json(200, { payload: withoutSpec(stub.application) });
    }
    if (url.endsWith("/v1/intake/review/applications") && method === "GET") {
      return json(200, { payload: [] });
    }
    throw new Error(`Unexpected fetch in test: ${method} ${url}`);
  });
  return actions;
}

function renderReview() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[`/worker/applications/${ID}`]}>
        <ReviewApplication applicationId={ID} />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

afterEach(() => {
  vi.restoreAllMocks();
});

test("shows the submission read-only with only the action the file allows", async () => {
  stubApi({ application: submitted() });
  const screen = await renderReview();

  await expect.element(screen.getByText("IA-11111111")).toBeVisible();
  await expect
    .element(screen.getByRole("gridcell", { name: "Submitted" }))
    .toBeVisible();
  await expect
    .element(screen.getByRole("link", { name: "← Back to list" }))
    .toHaveAttribute("href", "/worker/applications");

  const firstName = screen.getByRole("textbox", { name: /First name/ });
  await expect.element(firstName).toHaveValue("Ada");
  await expect.element(firstName).toBeDisabled();
  await expect
    .element(screen.getByRole("button", { name: "Submit" }))
    .not.toBeInTheDocument();

  await expect
    .element(screen.getByRole("button", { name: "Pick up for review" }))
    .toBeEnabled();
  await expect
    .element(screen.getByRole("button", { name: "Accept" }))
    .not.toBeInTheDocument();
  await expect
    .element(screen.getByRole("button", { name: "Deny" }))
    .not.toBeInTheDocument();
});

test("moves the file to Under Review, then decides it, sending the stream version each time", async () => {
  const actions = stubApi({ application: submitted() });
  const screen = await renderReview();

  await screen.getByRole("button", { name: "Pick up for review" }).click();

  await expect
    .element(screen.getByRole("gridcell", { name: "Under Review - MWORKER" }))
    .toBeVisible();
  await expect
    .element(screen.getByRole("status"))
    .toHaveTextContent(
      "The application is now under review and assigned to you.",
    );
  await expect
    .element(screen.getByRole("button", { name: "Pick up for review" }))
    .not.toBeInTheDocument();

  await screen.getByRole("button", { name: "Accept" }).click();

  await expect
    .element(screen.getByRole("gridcell", { name: "Accepted" }))
    .toBeVisible();
  await expect
    .element(screen.getByRole("button", { name: "Accept" }))
    .not.toBeInTheDocument();
  await expect
    .element(screen.getByRole("button", { name: "Deny" }))
    .not.toBeInTheDocument();

  expect(actions).toEqual([
    { route: "review", body: { streamVersion: 1 } },
    { route: "accept", body: { streamVersion: 2 } },
  ]);
});

test("denies a file under review", async () => {
  const actions = stubApi({ application: underReview() });
  const screen = await renderReview();

  await screen.getByRole("button", { name: "Deny" }).click();

  await expect
    .element(screen.getByRole("gridcell", { name: "Denied" }))
    .toBeVisible();
  expect(actions).toEqual([{ route: "deny", body: { streamVersion: 2 } }]);
});

test("a decided file offers no actions", async () => {
  stubApi({
    application: submitted({
      status: "DENIED",
      streamVersion: 3,
      availableActions: [],
    }),
  });
  const screen = await renderReview();

  await expect
    .element(screen.getByRole("gridcell", { name: "Denied" }))
    .toBeVisible();
  await expect.element(screen.getByRole("button")).not.toBeInTheDocument();
});

test("a conflict reloads the file and says another worker acted first", async () => {
  const stub: Stub = {
    application: submitted(),
    nextAction: {
      status: 409,
      body: {
        status: 409,
        keyword: REVIEW_KEYWORDS.eventConflict,
        currentVersion: 3,
      },
    },
  };
  const actions = stubApi(stub);
  const screen = await renderReview();

  // What the other worker did, which the refetch after the 409 reveals.
  stub.application = submitted({
    status: "DENIED",
    streamVersion: 3,
    availableActions: [],
  });

  await screen.getByRole("button", { name: "Pick up for review" }).click();

  await expect
    .element(screen.getByRole("alert"))
    .toHaveTextContent("changed by another worker");
  await expect
    .element(screen.getByRole("gridcell", { name: "Denied" }))
    .toBeVisible();
  await expect
    .element(screen.getByRole("button", { name: "Pick up for review" }))
    .not.toBeInTheDocument();
  expect(actions).toEqual([{ route: "review", body: { streamVersion: 1 } }]);
});

test("a missing or draft application says so", async () => {
  vi.spyOn(window, "fetch").mockImplementation(async () =>
    json(404, { status: 404 }),
  );
  const screen = await renderReview();

  await expect
    .element(screen.getByRole("alert"))
    .toHaveTextContent("This application could not be found.");
});

test.each([
  ["Request more information", "request-more-info", "Returned"],
  ["Escalate", "escalate", "Review"],
  ["Release to queue", "release", "Submitted"],
] as const)("the assignee can %s", async (label, route, statusLabel) => {
  const actions = stubApi({ application: underReview() });
  const screen = await renderReview();

  await screen.getByRole("button", { name: label }).click();

  await expect
    .element(screen.getByRole("gridcell", { name: statusLabel }))
    .toBeVisible();
  expect(actions).toEqual([{ route, body: { streamVersion: 2 } }]);
});

test("the assignee adds an internal note, sent with the stream version", async () => {
  const actions = stubApi({ application: underReview() });
  const screen = await renderReview();

  const add = screen.getByRole("button", { name: "Add note" });
  await expect.element(add).toBeDisabled();
  await screen
    .getByRole("textbox", { name: /Add a note/ })
    .fill("  Income proof checked.  ");
  await add.click();

  await expect.element(screen.getByText("Income proof checked.")).toBeVisible();
  await expect
    .element(screen.getByRole("textbox", { name: /Add a note/ }))
    .toHaveValue("");
  expect(actions).toEqual([
    {
      route: "notes",
      body: { streamVersion: 2, text: "Income proof checked." },
    },
  ]);
});

test("a file another worker holds is read-only, with notes shown but no box", async () => {
  stubApi({
    application: underReview({
      assignee: "MOTHER",
      canAddNote: false,
      availableActions: [],
      notes: [
        {
          text: "Waiting on a document.",
          author: "MOTHER",
          createdAt: "2026-09-28T12:00:00Z",
        },
      ],
    }),
  });
  const screen = await renderReview();

  await expect
    .element(screen.getByRole("gridcell", { name: "Under Review - MOTHER" }))
    .toBeVisible();
  await expect
    .element(screen.getByText(/under review by MOTHER/))
    .toBeVisible();
  await expect
    .element(screen.getByText("Waiting on a document."))
    .toBeVisible();
  await expect
    .element(screen.getByRole("textbox", { name: /Add a note/ }))
    .not.toBeInTheDocument();
  await expect.element(screen.getByRole("button")).not.toBeInTheDocument();
});

test("losing a pick-up to another worker says so and reloads", async () => {
  const stub: Stub = {
    application: submitted(),
    nextAction: {
      status: 409,
      body: {
        status: 409,
        keyword: REVIEW_KEYWORDS.assignedToAnother,
        currentVersion: 2,
      },
    },
  };
  stubApi(stub);
  const screen = await renderReview();
  stub.application = underReview({
    assignee: "MOTHER",
    canAddNote: false,
    availableActions: [],
  });

  await screen.getByRole("button", { name: "Pick up for review" }).click();

  await expect
    .element(screen.getByRole("alert"))
    .toHaveTextContent("Another worker picked up this application first.");
  await expect
    .element(screen.getByRole("gridcell", { name: "Under Review - MOTHER" }))
    .toBeVisible();
});

test("shows the history with who did what", async () => {
  stubApi({
    application: underReview({
      history: [
        {
          code: "SUBMIT",
          actorKind: "APPLICANT",
          idir: null,
          occurredAt: "2026-09-28T11:00:00Z",
        },
        {
          code: "PICK_UP",
          actorKind: "WORKER",
          idir: "MWORKER",
          occurredAt: "2026-09-28T11:30:00Z",
        },
      ],
    }),
  });
  const screen = await renderReview();

  const history = screen.getByRole("region", { name: "History" });
  await expect
    .element(history.getByRole("cell", { name: "Picked up for review" }))
    .toBeVisible();
  await expect
    .element(history.getByRole("cell", { name: "MWORKER" }))
    .toBeVisible();
  await expect
    .element(history.getByRole("cell", { name: "Applicant" }))
    .toBeVisible();
});

test("a decision is held back while a note is written but not added", async () => {
  const actions = stubApi({ application: underReview() });
  const screen = await renderReview();

  await screen
    .getByRole("textbox", { name: /Add a note/ })
    .fill("Checked the income proof.");
  await screen.getByRole("button", { name: "Accept" }).click();

  await expect
    .element(screen.getByRole("alert"))
    .toHaveTextContent("You have a note that has not been added.");
  await expect
    .element(screen.getByRole("textbox", { name: /Add a note/ }))
    .toHaveValue("Checked the income proof.");
  expect(actions).toEqual([]);

  // Adding it clears the hold; the decision then goes out.
  await screen.getByRole("button", { name: "Add note" }).click();
  await expect
    .element(screen.getByText("Checked the income proof."))
    .toBeVisible();
  await screen.getByRole("button", { name: "Accept" }).click();

  await expect
    .element(screen.getByRole("gridcell", { name: "Accepted" }))
    .toBeVisible();
  expect(actions).toEqual([
    {
      route: "notes",
      body: { streamVersion: 2, text: "Checked the income proof." },
    },
    { route: "accept", body: { streamVersion: 3 } },
  ]);
});

test("the actions wait while a note is being saved", async () => {
  let release!: () => void;
  const stub: Stub = {
    application: underReview(),
    holdNote: new Promise<void>((resolve) => {
      release = resolve;
    }),
  };
  stubApi(stub);
  const screen = await renderReview();

  await screen.getByRole("textbox", { name: /Add a note/ }).fill("Called.");
  await screen.getByRole("button", { name: "Add note" }).click();

  await expect
    .element(screen.getByRole("button", { name: "Accept" }))
    .toBeDisabled();
  await expect
    .element(screen.getByRole("button", { name: "Deny" }))
    .toBeDisabled();

  release();

  await expect.element(screen.getByText("Called.")).toBeVisible();
  await expect
    .element(screen.getByRole("button", { name: "Accept" }))
    .toBeEnabled();
});
