import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router";
import { render } from "vitest-browser-react";
import { afterEach, expect, test, vi } from "vitest";

import type { FormSpecPayload } from "@/api/forms";
import type {
  ReviewApplicationPayload,
  ReviewAttachmentPayload,
} from "@/api/review";
import { REVIEW_KEYWORDS } from "@/api/review";
import ReviewApplication from "@/widgets/review/ReviewApplication";
import { registerBcgovComponents } from "@myss/formio";

// One application under review: the submitted answers read-only, the buttons
// the server allows the caller, internal notes for the assignee, the history,
// and each write sending the stream version it was decided against. A 409
// reloads the file instead of pretending.

// As main.tsx does at app start, so the submission renders as it does for workers.
registerBcgovComponents();

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

test("Withdrawn is read-only with applicant history and no Review or other worker action", async () => {
  stubApi({
    application: submitted({
      status: "WITHDRAWN",
      inQueue: false,
      availableActions: [],
      history: [
        { code: "SUBMIT", actorKind: "APPLICANT", occurredAt: "2026-09-28T11:00:00Z" },
        { code: "WITHDRAW", actorKind: "APPLICANT", occurredAt: "2026-09-28T12:00:00Z" },
      ],
    }),
  });
  const screen = await renderReview();
  await expect.element(screen.getByRole("gridcell", { name: "Withdrawn" })).toBeVisible();
  await expect.element(screen.getByRole("cell", { name: "Withdrawn", exact: true })).toBeVisible();
  await expect.element(screen.getByRole("button", { name: "Pick up for review", exact: true })).not.toBeInTheDocument();
  await expect.element(screen.getByRole("textbox", { name: "First name" })).toBeDisabled();
});

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
  /** The supporting documents the applicant sent; none unless set. */
  documents?: ReviewAttachmentPayload[];
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
    if (url.endsWith(`${base}/attachments`) && method === "GET") {
      return json(200, { payload: stub.documents ?? [] });
    }
    if (url.includes(`${base}/attachments/`) && url.endsWith("/content")) {
      actions.push({ route: "download", body: url });
      return new Response(new Blob(["%PDF-1.7"], { type: "application/pdf" }));
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
      <MemoryRouter initialEntries={[`/applications/${ID}`]}>
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
    .toHaveAttribute("href", "/applications");

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

test("a note written when a decision is chosen is saved with the decision", async () => {
  const actions = stubApi({ application: underReview() });
  const screen = await renderReview();

  await screen
    .getByRole("textbox", { name: /Add a note/ })
    .fill("  Checked the income proof.  ");
  await screen.getByRole("button", { name: "Accept" }).click();

  await expect
    .element(screen.getByRole("status"))
    .toHaveTextContent("Your note was saved with the decision.");
  await expect
    .element(screen.getByRole("textbox", { name: /Add a note/ }))
    .not.toBeInTheDocument();
  expect(actions).toEqual([
    {
      route: "accept",
      body: { streamVersion: 2, comment: "Checked the income proof." },
    },
  ]);
});

test("a decision without a note sends no comment", async () => {
  const actions = stubApi({ application: underReview() });
  const screen = await renderReview();

  await screen.getByRole("button", { name: "Accept" }).click();

  await expect
    .element(screen.getByRole("gridcell", { name: "Accepted" }))
    .toBeVisible();
  expect(actions).toEqual([{ route: "accept", body: { streamVersion: 2 } }]);
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

/** The application with a repeat section: one "Child Info" group per child counted. */
const repeatSectionSpec = {
  formSpecId: "income-assistance-poc",
  version: 2,
  title: "Income Assistance application",
  spec: {
    display: "form",
    components: [
      { type: "textfield", key: "firstName", label: "First name", input: true },
      { type: "number", key: "childCount", label: "How many children?", input: true },
      {
        type: "bcgovRepeatSection",
        key: "children",
        label: "Children",
        input: true,
        properties: { myssRepeatCount: "childCount", myssRepeatMax: 10, myssRowLabel: "Child Info" },
        components: [
          { type: "textfield", key: "childFirstName", label: "What is this child's first name?", input: true },
        ],
      },
      { type: "button", key: "submit", action: "submit", label: "Submit", input: true },
    ],
  },
} as unknown as FormSpecPayload;

const threeChildren = {
  firstName: "Ada",
  childCount: 3,
  children: [{ childFirstName: "Ann" }, { childFirstName: "Ben" }, { childFirstName: "Cal" }],
};

test("shows a submission's rows read-only, one Child Info group per row", async () => {
  stubApi({
    application: submitted({ spec: repeatSectionSpec, formSpecVersion: 2, answers: threeChildren }),
  });
  const screen = await renderReview();

  for (const [index, name] of [[1, "Ann"], [2, "Ben"], [3, "Cal"]] as const) {
    const field = screen
      .getByRole("group", { name: `Child Info ${index} of 3`, exact: true })
      .getByRole("textbox", { name: "What is this child's first name?" });
    await expect.element(field).toHaveValue(name);
    await expect.element(field).toBeDisabled();
  }
});

test("the supporting documents are listed and download through the API", async () => {
  const createObjectURL = vi
    .spyOn(URL, "createObjectURL")
    .mockReturnValue("blob:document");
  vi.spyOn(URL, "revokeObjectURL").mockImplementation(() => {});
  const actions = stubApi({
    application: submitted(),
    documents: [
      {
        id: "doc-a",
        fileName: "paystub.pdf",
        contentType: "application/pdf",
        sizeBytes: 2048,
        uploadedAt: "2026-11-20T10:00:00Z",
      },
    ],
  });
  const screen = await renderReview();

  const section = screen.getByRole("region", { name: "Supporting documents" });
  await expect.element(section).toHaveTextContent("paystub.pdf");
  await expect.element(section).toHaveTextContent("2 KB");

  await screen.getByRole("button", { name: "Download paystub.pdf" }).click();

  await vi.waitFor(() => expect(createObjectURL).toHaveBeenCalledOnce());
  expect(actions).toContainEqual({
    route: "download",
    body: expect.stringContaining(
      `/v1/intake/review/applications/${ID}/attachments/doc-a/content`,
    ),
  });
});

test("an application without documents says so", async () => {
  stubApi({ application: submitted() });
  const screen = await renderReview();

  await expect
    .element(screen.getByText("The applicant did not send any documents."))
    .toBeVisible();
});

test("the summary names the applicant", async () => {
  stubApi({ application: submitted({ applicantName: "Ada Lovelace" }) });
  const screen = await renderReview();

  await expect.element(screen.getByText("Ada Lovelace")).toBeVisible();
});
