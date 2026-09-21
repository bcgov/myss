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
        type: "textfield",
        key: "socialInsuranceNumber",
        label: "Social Insurance Number (SIN)",
        input: true,
        properties: { myssValidator: "sin" },
      },
      {
        type: "textfield",
        key: "phoneNumber",
        label: "Phone number",
        input: true,
        validate: { required: true },
        inputMask: "(999) 999-9999",
        placeholder: "(999) 999-9999",
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

interface ApiValidationError {
  field: string;
  keyword: string;
  message: string;
}

function stubFormApi(options: { rejectWith?: ApiValidationError[] } = {}) {
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

      if (options.rejectWith) {
        return new Response(JSON.stringify({ payload: options.rejectWith }), {
          status: 422,
          headers: { "Content-Type": "application/json" },
        });
      }

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
        {
          status: 200,
          headers: { "Content-Type": "application/json" },
        },
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

  await expect
    .element(screen.getByRole("textbox", { name: "Full name" }))
    .toBeVisible();

  await screen.getByRole("textbox", { name: "Full name" }).fill("Ada Lovelace");
  const phoneNumber = screen.getByRole("textbox", { name: "Phone number" });
  await expect.element(phoneNumber).toHaveAttribute("placeholder", "(999) 999-9999");
  await phoneNumber.fill("(250) 234-5678");
  await screen.getByRole("button", { name: "Submit" }).click();

  await expect.element(screen.getByText("Submission received")).toBeVisible();
  await expect.element(screen.getByText("bc-bus-pass v2")).toBeVisible();
  expect(posts).toHaveLength(1);
  expect(posts[0].body).toMatchObject({
    formSpecVersion: 2,
    answers: {
      fullName: "Ada Lovelace",
      phoneNumber: "(250) 234-5678",
    },
  });
});

test("shows backend errors inline and focuses the first errored field", async () => {
  const posts = stubFormApi({
    rejectWith: [
      {
        field: "socialInsuranceNumber",
        keyword: "IDA.SIN.WRONG_LENGTH",
        message: "A Social Insurance Number must be 9 digits.",
      },
      {
        field: "fullName",
        keyword: "FORM.FIELD.INVALID",
        message: "Full name is invalid.",
      },
    ],
  });
  const screen = await renderForm();

  await screen.getByRole("textbox", { name: "Full name" }).fill("Ada Lovelace");
  await screen
    .getByRole("textbox", { name: "Social Insurance Number (SIN)" })
    .fill("123456789");
  await screen
    .getByRole("textbox", { name: "Phone number" })
    .fill("(250) 234-5678");

  const sinComponent = document.querySelector(
    ".formio-component-socialInsuranceNumber",
  );
  expect(sinComponent).not.toBeNull();
  expect(sinComponent?.textContent).not.toContain("SIN must be valid");

  await screen.getByRole("button", { name: "Submit" }).click();

  const fullNameComponent = document.querySelector(
    ".formio-component-fullName",
  );
  const fullNameInput = document.querySelector('[name="data[fullName]"]');

  expect(fullNameComponent).not.toBeNull();
  await vi.waitFor(() => {
    expect(sinComponent?.textContent).toContain("SIN must be valid");
    expect(fullNameComponent?.textContent).toContain("Full name is invalid.");
    expect(document.activeElement).toBe(fullNameInput);
  });
  await new Promise((resolve) => window.setTimeout(resolve, 50));
  expect(sinComponent?.textContent).toContain("SIN must be valid");
  expect(fullNameComponent?.textContent).toContain("Full name is invalid.");

  expect(document.querySelector(".poc-form-errors")).toBeNull();
  expect(document.querySelector(".alert-success")).toBeNull();
  expect(posts).toHaveLength(1);
  expect(posts[0].body).toMatchObject({
    formSpecVersion: 2,
    answers: {
      socialInsuranceNumber: "123456789",
    },
  });

  await screen
    .getByRole("textbox", { name: "Social Insurance Number (SIN)" })
    .fill("046454286");
  await vi.waitFor(() => {
    expect(sinComponent?.textContent).not.toContain("SIN must be valid");
    expect(fullNameComponent?.textContent).toContain("Full name is invalid.");
  });
  await screen.getByRole("textbox", { name: "Full name" }).fill("Grace Hopper");
  await vi.waitFor(() => {
    expect(sinComponent?.textContent).not.toContain("SIN must be valid");
    expect(fullNameComponent?.textContent).not.toContain(
      "Full name is invalid.",
    );
  });
  await screen.getByRole("button", { name: "Submit" }).click();
  await vi.waitFor(() => expect(posts).toHaveLength(2));
});
