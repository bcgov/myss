import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router";
import { render } from "vitest-browser-react";
import { afterEach, expect, test, vi } from "vitest";

import PocForm from "@/components/PocForm";

// The submission payload should carry the spec version the form was
// rendered with.

// Uses a distinctive version number so the assertions can tell where the
// version stamp came from.
const currentSpecV7 = {
  formSpecId: "poc-test-form",
  version: 7,
  title: "POC test form",
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

function stubFormApi() {
  const posts: Array<{ url: string; body: unknown }> = [];
  vi.spyOn(window, "fetch").mockImplementation(async (input, init) => {
    const url = String(input);
    if (url.endsWith("/v1/forms/poc-test-form/spec")) {
      return new Response(JSON.stringify({ payload: currentSpecV7 }), {
        status: 200,
        headers: { "Content-Type": "application/json" },
      });
    }
    if (
      url.endsWith("/v1/forms/poc-test-form/submissions") &&
      init?.method === "POST"
    ) {
      const body = JSON.parse(String(init.body));
      posts.push({ url, body });
      return new Response(
        JSON.stringify({
          payload: {
            id: "99999999-8888-7777-6666-555555555555",
            formSpecId: "poc-test-form",
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
        <PocForm />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

afterEach(() => {
  vi.restoreAllMocks();
});

test("renders the fetched spec and shows its version", async () => {
  stubFormApi();
  const screen = await renderForm();

  await expect.element(screen.getByText("(spec v7)")).toBeVisible();
  await expect
    .element(screen.getByRole("textbox", { name: "First name" }))
    .toBeVisible();
});

test("submits the version it rendered with, and links to the stored submission", async () => {
  const posts = stubFormApi();
  const screen = await renderForm();

  await screen.getByRole("textbox", { name: "First name" }).fill("Ada");
  await screen.getByRole("button", { name: "Submit" }).click();

  await expect.element(screen.getByText("Submission received")).toBeVisible();
  await expect.element(screen.getByText("poc-test-form v7")).toBeVisible();
  const viewLink = screen.getByRole("link", { name: "View this submission" });
  await expect.element(viewLink).toBeVisible();

  // The posted request should carry the fetched spec's version.
  expect(posts).toHaveLength(1);
  expect(posts[0].body).toMatchObject({
    formSpecVersion: 7,
    answers: { firstName: "Ada" },
  });
});
