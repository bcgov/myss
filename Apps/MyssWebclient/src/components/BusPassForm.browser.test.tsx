import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router";
import { render } from "vitest-browser-react";
import { afterEach, expect, test, vi } from "vitest";

import BusPassForm from "@/components/BusPassForm";

const currentSpecV2 = {
  formSpecId: "bc-bus-pass",
  version: 2,
  title: "BC Bus Pass",
  spec: {
    display: "form",
    components: [
      {
        type: "textfield",
        key: "fullName",
        label: "Full name",
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

function stubFormApi() {
  const posts: Array<{ url: string; body: unknown }> = [];
  vi.spyOn(window, "fetch").mockImplementation(async (input, init) => {
    const url = String(input);
    if (url.endsWith("/v1/forms/bc-bus-pass/spec")) {
      return new Response(JSON.stringify({ payload: currentSpecV2 }), {
        status: 200,
        headers: { "Content-Type": "application/json" },
      });
    }
    if (
      url.endsWith("/v1/forms/bc-bus-pass/submissions") &&
      init?.method === "POST"
    ) {
      const body = JSON.parse(String(init.body));
      posts.push({ url, body });
      return new Response(
        JSON.stringify({
          payload: {
            id: "11111111-2222-3333-4444-555555555555",
            formSpecId: "bc-bus-pass",
            formSpecVersion: body.formSpecVersion,
            answers: body.answers,
            submittedAt: new Date().toISOString(),
          },
        }),
        { status: 200, headers: { "Content-Type": "application/json" } },
      );
    }
    throw new Error(`Unexpected fetch in test: ${url}`);
  });
  return posts;
}

function renderForm() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <BusPassForm />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

afterEach(() => {
  vi.restoreAllMocks();
});

test("loads the BC Bus Pass spec and submits with the rendered version", async () => {
  const posts = stubFormApi();
  const screen = await renderForm();

  await expect.element(screen.getByText("(spec v2)")).toBeVisible();
  await expect
    .element(screen.getByRole("textbox", { name: "Full name" }))
    .toBeVisible();

  await screen.getByRole("textbox", { name: "Full name" }).fill("Ada Lovelace");
  await screen.getByRole("button", { name: "Submit" }).click();

  await expect.element(screen.getByText("Submission received")).toBeVisible();
  await expect.element(screen.getByText("bc-bus-pass v2")).toBeVisible();
  expect(posts).toHaveLength(1);
  expect(posts[0].body).toMatchObject({
    formSpecVersion: 2,
    answers: { fullName: "Ada Lovelace" },
  });
});
