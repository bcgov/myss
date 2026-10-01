import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { render } from "vitest-browser-react";
import { MemoryRouter, Route, Routes, useParams } from "react-router";

import type { ApplicationSummaryPayload } from "@/api/intake";
import type { Session } from "@/auth/useSession";
import { paths } from "@/routes/paths";

const session = vi.hoisted(
  () =>
    ({
      user: undefined,
      isAuthenticated: true,
      isLoading: false,
      isMeLoading: false,
      hasProfile: true,
      profileFirstName: null,
      login: vi.fn(),
      logout: vi.fn(),
    }) as Session & { logout: ReturnType<typeof vi.fn> },
);

vi.mock("@/auth/useSession", () => ({
  useSession: () => session,
}));

import DashboardPage from "./DashboardPage";

const DRAFT_ID = "11111111-2222-3333-4444-555555555555";
const SUBMITTED_ID = "22222222-3333-4444-5555-666666666666";
const NEW_ID = "33333333-4444-5555-6666-777777777777";

const applications: ApplicationSummaryPayload[] = [
  {
    id: DRAFT_ID,
    referenceNumber: "IA-11111111",
    status: "DRAFT",
    version: 2,
    formSpecId: "income-assistance-poc",
    formSpecVersion: 1,
    createdAt: "2026-09-28T10:00:00Z",
    updatedAt: "2026-09-28T10:05:00Z",
    submittedAt: null,
  },
  {
    id: SUBMITTED_ID,
    referenceNumber: "IA-22222222",
    status: "SUBMITTED",
    version: 4,
    formSpecId: "income-assistance-poc",
    formSpecVersion: 1,
    createdAt: "2026-09-20T10:00:00Z",
    updatedAt: "2026-09-21T10:00:00Z",
    submittedAt: "2026-09-21T10:00:00Z",
  },
];

function json(status: number, body: unknown) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

function stubApi(list: ApplicationSummaryPayload[] = applications) {
  const posts: string[] = [];
  vi.spyOn(window, "fetch").mockImplementation(async (input, init) => {
    const url = String(input);
    const method = init?.method ?? "GET";
    if (url.endsWith("/v1/intake/applications") && method === "GET") {
      return json(200, { payload: list });
    }
    if (url.endsWith("/v1/intake/applications") && method === "POST") {
      posts.push(url);
      return json(201, {
        payload: {
          id: NEW_ID,
          referenceNumber: "IA-33333333",
          status: "DRAFT",
          version: 1,
          formSpecId: "income-assistance-poc",
          formSpecVersion: 1,
          answers: {},
          createdAt: "2026-09-28T12:00:00Z",
          updatedAt: "2026-09-28T12:00:00Z",
          submittedAt: null,
        },
      });
    }
    throw new Error(`Unexpected fetch in test: ${method} ${url}`);
  });
  return posts;
}

function ApplicationLanding() {
  const { id } = useParams<{ id: string }>();
  return <p>Application landing {id}</p>;
}

function renderDashboard() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[paths.dashboard]}>
        <Routes>
          <Route path={paths.dashboard} element={<DashboardPage />} />
          <Route path={paths.register} element={<p>Registration landing</p>} />
          <Route path={paths.application} element={<ApplicationLanding />} />
          <Route
            path={paths.workerApplications}
            element={<p>Worker landing</p>}
          />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe("DashboardPage", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    session.user = { sub: "user-1", name: "Alice", roles: [] };
    session.isMeLoading = false;
    session.hasProfile = true;
    session.profileFirstName = null;
    session.logout.mockClear();
  });

  it("shows account checking while the profile lookup is pending", async () => {
    stubApi();
    session.isMeLoading = true;
    const screen = await renderDashboard();

    await expect
      .element(screen.getByRole("status"))
      .toHaveTextContent("Checking your MySS account…");
  });

  it("sends a Ministry worker to the worker view instead of registration", async () => {
    // A worker has no citizen profile; without the role check first they
    // would be bounced to the registration form.
    stubApi();
    session.user = {
      sub: "idir-user",
      name: "Wanda",
      roles: ["WORKER"],
      idirUsername: "WWORKER",
    };
    session.hasProfile = false;
    const screen = await renderDashboard();

    await expect
      .element(screen.getByText("Worker landing"))
      .toBeInTheDocument();
  });

  it("waits for the roles before deciding where a signed-in user lands", async () => {
    stubApi();
    session.user = { sub: "idir-user", roles: [], idirUsername: "WWORKER" };
    session.isMeLoading = true;
    session.hasProfile = undefined;
    const screen = await renderDashboard();

    await expect
      .element(screen.getByRole("status"))
      .toHaveTextContent("Checking your MySS account…");
  });

  it("redirects an authenticated user without a profile to registration", async () => {
    stubApi();
    session.hasProfile = false;
    const screen = await renderDashboard();

    await expect
      .element(screen.getByText("Registration landing"))
      .toBeInTheDocument();
  });

  it("shows profile information and signs out a registered user", async () => {
    stubApi();
    session.profileFirstName = "Ada";
    const screen = await renderDashboard();

    await expect
      .element(screen.getByRole("heading", { name: "Hello Alice" }))
      .toBeInTheDocument();
    await expect
      .element(
        screen.getByText("Your MySS account profile is registered to Ada."),
      )
      .toBeInTheDocument();

    await screen.getByRole("button", { name: "Log out" }).click();
    expect(session.logout).toHaveBeenCalledOnce();
  });

  it("lists every application with its status and a way into it", async () => {
    stubApi();
    const screen = await renderDashboard();

    await expect
      .element(screen.getByRole("heading", { name: "My applications" }))
      .toBeVisible();
    // The status pill is the design system's Tag, which renders as a grid cell.
    await expect
      .element(screen.getByRole("gridcell", { name: "Draft" }))
      .toBeVisible();
    await expect
      .element(screen.getByRole("gridcell", { name: "Submitted" }))
      .toBeVisible();

    const draftLink = screen.getByRole("link", { name: /^Continue/ });
    await expect
      .element(draftLink)
      .toHaveAttribute("href", `/applications/${DRAFT_ID}`);
    const submittedLink = screen.getByRole("link", { name: /^View/ });
    await expect
      .element(submittedLink)
      .toHaveAttribute("href", `/applications/${SUBMITTED_ID}`);
  });

  it("says so when there are no applications yet", async () => {
    stubApi([]);
    const screen = await renderDashboard();

    await expect
      .element(screen.getByText("You have not started an application yet."))
      .toBeVisible();
  });

  it("starts a new application and opens it", async () => {
    const posts = stubApi();
    const screen = await renderDashboard();

    await screen.getByRole("button", { name: "Start new application" }).click();

    await expect
      .element(screen.getByText(`Application landing ${NEW_ID}`))
      .toBeVisible();
    expect(posts).toHaveLength(1);
  });
});
