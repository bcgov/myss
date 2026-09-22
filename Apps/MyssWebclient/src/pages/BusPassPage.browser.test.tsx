import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router";
import { render } from "vitest-browser-react";
import { afterEach, expect, test, vi } from "vitest";

import BusPassPage from "@/pages/BusPassPage";

const currentSpec = {
  formSpecId: "bc-bus-pass",
  version: 2,
  title: "BC Bus Pass",
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
        type: "button",
        key: "submit",
        action: "submit",
        label: "Submit",
        input: true,
      },
    ],
  },
};

afterEach(() => {
  vi.restoreAllMocks();
});

test("submits the bus pass page through the public bus-pass endpoint", async () => {
  const posts: Array<{ url: string; body: unknown }> = [];
  vi.spyOn(window, "fetch").mockImplementation(async (input, init) => {
    const url = String(input);
    if (url.endsWith("/v1/forms/bc-bus-pass/spec")) {
      return new Response(JSON.stringify({ payload: currentSpec }), {
        status: 200,
        headers: { "Content-Type": "application/json" },
      });
    }
    if (url.endsWith("/v1/forms/bc-bus-pass/submissions")) {
      return new Response(JSON.stringify({ payload: [] }), {
        status: 200,
        headers: { "Content-Type": "application/json" },
      });
    }
    if (url.endsWith("/v1/bus-pass/submissions") && init?.method === "POST") {
      const body = JSON.parse(String(init.body));
      posts.push({ url, body });
      return new Response(
        JSON.stringify({
          payload: {
            submissionId: "11111111-2222-3333-4444-555555555555",
            formSpecId: "bc-bus-pass",
            formSpecVersion: body.formSpecVersion,
            referenceNumber: "1-TEST-0001",
            outcome: "Accepted",
          },
        }),
        { status: 200, headers: { "Content-Type": "application/json" } },
      );
    }
    throw new Error(`Unexpected fetch in test: ${url}`);
  });

  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const screen = await render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <BusPassPage />
      </MemoryRouter>
    </QueryClientProvider>,
  );

  await screen.getByRole("textbox", { name: "First name" }).fill("Ada");
  await screen.getByRole("button", { name: "Submit" }).click();

  await expect
    .element(screen.getByRole("heading", { name: "Request submitted" }))
    .toBeVisible();
  expect(posts).toHaveLength(1);
  expect(posts[0].url).toContain("/v1/bus-pass/submissions");
  expect(posts[0].body).toMatchObject({
    formSpecVersion: 2,
    answers: { firstName: "Ada" },
  });
});