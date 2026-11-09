import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router";
import { page } from "vitest/browser";
import { render } from "vitest-browser-react";
import {
  afterEach,
  beforeAll,
  beforeEach,
  describe,
  expect,
  test,
  vi,
} from "vitest";

import { loadPageStyles } from "@/testing/pageStyles";
import BusPassForm from "@/widgets/BusPassForm";
import { registerBcgovComponents } from "@myss/formio";
import { BUS_PASS_CONFIRMATION } from "@myss/web-common/api";

const currentSpecV1 = {
  formSpecId: "bc-bus-pass",
  version: 1,
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

const currentSpecV3 = {
  formSpecId: "bc-bus-pass",
  version: 3,
  title: "BC Bus Pass",
  spec: {
    display: "form",
    components: [
      {
        type: "bcgovRadio",
        key: "serviceRequestType",
        label: "What type of service would you like to request?",
        input: true,
        values: [
          {
            label:
              "I am an existing client and require an address change or replacement bus pass.",
            value: "existing",
            childrenLabel: "Existing client request",
            children: [
              {
                label: "I have moved and would like to update my address.",
                value: "addressUpdate",
              },
              {
                label:
                  "My bus pass has been lost or stolen. Please mail me a replacement pass application.",
                value: "replacement",
              },
            ],
          },
          {
            label: "I am a new applicant and would like to request a bus pass.",
            value: "newApplication",
          },
        ],
        validate: {
          required: true,
          customMessage: "Select the type of service you would like to request",
        },
      },
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

const currentSpecV4 = {
  formSpecId: "bc-bus-pass",
  version: 4,
  title: "BC Bus Pass",
  spec: {
    display: "form",
    components: [
      {
        type: "bcgovRadio",
        key: "serviceRequestType",
        label: "What type of service would you like to request?",
        input: true,
        values: [
          {
            label:
              "I am an existing client and require an address change or replacement bus pass.",
            value: "existing",
            childrenLabel: "Existing client request",
            children: [
              {
                label: "I have moved and would like to update my address.",
                value: "addressUpdate",
              },
              {
                label:
                  "My bus pass has been lost or stolen. Please mail me a replacement pass application.",
                value: "replacement",
              },
            ],
          },
          {
            label: "I am a new applicant and would like to request a bus pass.",
            value: "newApplication",
          },
        ],
        validate: {
          required: true,
          customMessage: "Select the type of service you would like to request",
        },
      },
      {
        type: "content",
        key: "replacementCostNote",
        html: "<p><strong>Note: You will be billed $10 if this is your first replacement pass, $20 if this is your second replacement pass, and $50 for each additional replacement pass during your annual eligibility period.</strong></p>",
        conditional: {
          eq: "replacement",
          show: true,
          when: "serviceRequestType",
        },
      },
      {
        type: "checkbox",
        key: "acknowledgedPassCancellation",
        input: true,
        label:
          "I acknowledge that my lost/stolen bus pass will be cancelled before a replacement application is mailed to me. Bus passes cannot be reactivated if found.",
        validate: {
          required: true,
          customMessage: "Acknowledgement is required",
        },
        conditional: {
          eq: "replacement",
          show: true,
          when: "serviceRequestType",
        },
      },
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

const phoneSpecV7 = {
  formSpecId: "bc-bus-pass",
  version: 7,
  title: "BC Bus Pass",
  spec: {
    display: "form",
    components: [
      {
        type: "textfield",
        key: "phoneNumber",
        label: "Phone number",
        input: true,
        placeholder: "(999) 999-9999",
        validateOn: "blur",
        properties: { myssValidator: "phone" },
        validate: {
          required: true,
          customMessage: "Phone number is invalid",
        },
      },
      {
        type: "textfield",
        key: "firstName",
        label: "First name",
        input: true,
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

const legacyPhoneSpec = {
  ...phoneSpecV7,
  version: 2,
  spec: {
    ...phoneSpecV7.spec,
    components: [
      {
        type: "textfield",
        key: "phoneNumber",
        label: "Phone number",
        input: true,
        inputMask: "(999) 999-9999",
        validate: { required: true },
      },
      ...phoneSpecV7.spec.components.slice(1),
    ],
  },
};

const requiredWhenEmailPreferred = [
  {
    name: "Required when email is the preferred method of communication",
    trigger: {
      type: "simple",
      simple: { show: true, when: "preferredCommunication", eq: "email" },
    },
    actions: [
      {
        name: "Require",
        type: "property",
        property: {
          label: "Required",
          value: "validate.required",
          type: "boolean",
        },
        state: true,
      },
    ],
  },
];

/** The contact questions of the seeded v9, where the preferred method became radio buttons. */
const contactSpecV9 = {
  formSpecId: "bc-bus-pass",
  version: 9,
  title: "BC Bus Pass",
  spec: {
    display: "form",
    components: [
      {
        key: "phoneNumber",
        type: "textfield",
        input: true,
        label: "Phone number",
        validate: { required: true, customMessage: "Phone number is invalid" },
        validateOn: "blur",
        properties: { myssValidator: "phone" },
      },
      {
        key: "phoneType",
        type: "radio",
        input: true,
        label: "Phone type",
        values: [
          { label: "Home", value: "home" },
          { label: "Work", value: "work" },
          { label: "Cell", value: "cell" },
        ],
        validate: { required: false },
      },
      {
        key: "email",
        type: "email",
        input: true,
        label: "Email",
        validate: { required: false },
        logic: requiredWhenEmailPreferred,
        errors: {
          required:
            "An email address is required when email is the preferred means of communication",
        },
      },
      {
        key: "emailVerification",
        type: "email",
        input: true,
        label: "Email verification",
        validate: { required: false },
        properties: { myssMatches: "email" },
        errors: {
          matches: "The two email addresses do not match",
          required: "Enter your email address again to confirm it",
        },
        logic: requiredWhenEmailPreferred,
      },
      {
        key: "preferredCommunication",
        type: "radio",
        input: true,
        label: "Preferred method of communication",
        values: [
          { label: "Phone", value: "phone" },
          { label: "Email", value: "email" },
        ],
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

/** An API answer; `wait`, when given, holds it back until it settles. */
type Answer = { status: number; body: unknown; wait?: Promise<unknown> };

const accepted: Answer = {
  status: 200,
  body: {
    payload: {
      submissionId: "11111111-2222-3333-4444-555555555555",
      formSpecId: "bc-bus-pass",
      formSpecVersion: 1,
      referenceNumber: "1-TEST-0001",
      outcome: "Accepted",
      keyword: null,
      errorCode: null,
    },
  },
};

const rejected: Answer = {
  status: 200,
  body: {
    payload: {
      submissionId: "11111111-2222-3333-4444-555555555555",
      formSpecId: "bc-bus-pass",
      formSpecVersion: 1,
      referenceNumber: "1-ERR-0002",
      outcome: "Rejected",
      keyword: "BUSPASS.SUBMIT.REJECTED",
      errorCode: "NO_MATCH",
    },
  },
};

function unavailable(mayHaveReachedIcm: boolean): Answer {
  return {
    status: 503,
    body: {
      type: "https://tools.ietf.org/html/rfc9110#section-15.6.4",
      title: "The request was saved but could not be sent to the ministry.",
      status: 503,
      detail: "Quote the submission id if you contact the ministry.",
      keyword: "BUSPASS.SUBMIT.ICM_UNAVAILABLE",
      errorCode: mayHaveReachedIcm
        ? "ICM.BUSPASS.TIMEOUT"
        : "ICM.BUSPASS.UNREACHABLE",
      submissionId: "11111111-2222-3333-4444-555555555555",
      mayHaveReachedIcm,
    },
  };
}

const throttled: Answer = {
  status: 429,
  body: {
    title: "Too many submissions from this address. Try again later.",
    status: 429,
    keyword: "BUSPASS.SUBMIT.RATE_LIMITED",
  },
};

const refused: Answer = {
  status: 422,
  body: {
    payload: [
      {
        field: "firstName",
        keyword: "BUSPASS.IDENTITY.IDENTIFIER_REQUIRED",
        message:
          "A Social Insurance Number or bus pass account number is required",
      },
    ],
  },
};

/** The confirmation message's answer; "never" leaves the request unanswered. */
type MessageAnswer = { status: number; body?: unknown } | "never";

const configuredMessage = {
  heading: "Your bus pass request is in",
  title: "We have your request",
  body: "Thank you. Your reference number is {referenceNumber}.",
};

function published(texts: typeof configuredMessage): MessageAnswer {
  return { status: 200, body: { payload: texts } };
}

/** How many times the page asked for the confirmation message. */
let messageReads = 0;

/**
 * The error message catalogue the stubbed API serves: empty by default, so the
 * page words every outcome from its compiled fallback; null answers 500, the
 * way an unreachable content engine would surface through the API. No
 * confirmation message is published unless `message` says otherwise.
 */
function stubApi(
  answer: Answer,
  spec: unknown = currentSpecV1,
  catalogue: Record<string, string> | null = {},
  message: MessageAnswer = { status: 404 },
) {
  const posts: Array<{ url: string; body: unknown }> = [];
  messageReads = 0;
  vi.spyOn(window, "fetch").mockImplementation(async (input, init) => {
    const url = String(input);
    if (url.endsWith("/v1/forms/bc-bus-pass/messages/confirmation")) {
      messageReads += 1;
      if (message === "never") return new Promise<Response>(() => {});
      return new Response(
        message.body === undefined ? "" : JSON.stringify(message.body),
        { status: message.status, headers: { "Content-Type": "application/json" } },
      );
    }
    if (url.endsWith("/v1/forms/bc-bus-pass/spec")) {
      return new Response(JSON.stringify({ payload: spec }), {
        status: 200,
        headers: { "Content-Type": "application/json" },
      });
    }
    if (url.endsWith("/v1/forms/error-messages")) {
      return catalogue === null
        ? new Response("", { status: 500 })
        : new Response(JSON.stringify({ payload: catalogue }), {
            status: 200,
            headers: { "Content-Type": "application/json" },
          });
    }
    if (url.endsWith("/v1/bus-pass/submissions") && init?.method === "POST") {
      posts.push({ url, body: JSON.parse(String(init.body)) });
      await answer.wait;
      return new Response(JSON.stringify(answer.body), {
        status: answer.status,
        headers: { "Content-Type": "application/json" },
      });
    }
    throw new Error(`Unexpected fetch in test: ${url}`);
  });
  return posts;
}

registerBcgovComponents();

function newQueryClient() {
  return new QueryClient({ defaultOptions: { queries: { retry: false } } });
}

function renderForm(queryClient = newQueryClient()) {
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <BusPassForm />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

async function fillAndSubmit(screen: Awaited<ReturnType<typeof renderForm>>) {
  await expect
    .element(screen.getByRole("textbox", { name: "First name" }))
    .toBeVisible();
  await screen.getByRole("textbox", { name: "First name" }).fill("Ada");
  await screen.getByRole("button", { name: "Submit" }).click();
}

afterEach(() => {
  vi.restoreAllMocks();
});

test("phone errors appear inline on blur and clear after correction", async () => {
  stubApi(accepted, phoneSpecV7);
  const screen = await renderForm();
  const phone = screen.getByRole("textbox", { name: "Phone number" });
  const firstName = screen.getByRole("textbox", { name: "First name" });

  await phone.fill("1234567890");
  await firstName.click();
  await expect
    .element(screen.getByText("Phone number is invalid").first())
    .toBeVisible();

  await phone.fill("2505550199");
  await expect.element(phone).toHaveValue("(250) 555-0199");
  await firstName.click();
  await expect
    .element(screen.getByText("Phone number is invalid"))
    .not.toBeInTheDocument();

  await phone.fill("2501550199");
  await firstName.click();
  await expect
    .element(screen.getByText("Phone number is invalid"))
    .not.toBeInTheDocument();

  await phone.fill("25055501999");
  await expect.element(phone).toHaveValue("(250) 555-0199");
  await phone.fill("250555019");
  await expect.element(phone).toHaveValue("(250) 555-019");
  await firstName.click();
  await expect
    .element(screen.getByText("Phone number is invalid").first())
    .toBeVisible();

  await phone.fill("");
  await firstName.click();
  await expect
    .element(screen.getByText("Phone number is invalid").first())
    .toBeVisible();
});

test("an older masked phone reports a short number, then submits formatted digits", async () => {
  const posts = stubApi(accepted, legacyPhoneSpec);
  const screen = await renderForm();
  const phone = screen.getByRole("textbox", { name: "Phone number" });

  await phone.fill("250555019");
  await expect.element(phone).toHaveValue("(250) 555-019");
  await screen.getByRole("textbox", { name: "First name" }).fill("Ada");
  await screen.getByRole("button", { name: "Submit" }).click();
  await expect.element(phone).toHaveAttribute("aria-invalid", "true");
  expect(posts).toHaveLength(0);

  await phone.fill(" 250 555 - 0199 ");
  await expect.element(phone).toHaveValue("(250) 555-0199");
  await screen.getByRole("button", { name: "Submit" }).click();
  await vi.waitFor(() => expect(posts).toHaveLength(1));
  expect(posts[0].body).toMatchObject({ answers: { phoneNumber: "(250) 555-0199" } });
});

test("posts to the bus pass endpoint with the rendered version and shows the reference number", async () => {
  const posts = stubApi(accepted);
  const screen = await renderForm();

  await fillAndSubmit(screen);

  await expect
    .element(
      screen.getByRole("heading", {
        level: 1,
        name: BUS_PASS_CONFIRMATION.heading,
      }),
    )
    .toBeVisible();
  await expect.element(screen.getByText("1-TEST-0001")).toBeVisible();
  expect(posts).toHaveLength(1);
  expect(posts[0].url).toContain("/v1/bus-pass/submissions");
  expect(posts[0].body).toMatchObject({
    formSpecVersion: 1,
    answers: { firstName: "Ada" },
  });
});

test("renders the nested service choices in order and submits the address update leaf", async () => {
  const posts = stubApi(accepted, currentSpecV3);
  const screen = await renderForm();

  await expect
    .element(
      screen.getByText(
        "I am an existing client and require an address change or replacement bus pass.",
      ),
    )
    .toBeVisible();
  await expect
    .element(
      screen.getByText(
        "I am a new applicant and would like to request a bus pass.",
      ),
    )
    .toBeVisible();
  await expect
    .element(
      screen.getByText("I have moved and would like to update my address."),
    )
    .not.toBeInTheDocument();

  await screen
    .getByText(
      "I am an existing client and require an address change or replacement bus pass.",
    )
    .click();
  await expect
    .element(
      screen.getByText("I have moved and would like to update my address."),
    )
    .toBeVisible();
  await expect
    .element(
      screen.getByText(
        "My bus pass has been lost or stolen. Please mail me a replacement pass application.",
      ),
    )
    .toBeVisible();

  await screen
    .getByText("I have moved and would like to update my address.")
    .click();
  await screen.getByRole("textbox", { name: "First name" }).fill("Ada");
  await screen.getByRole("button", { name: "Submit" }).click();

  await expect
    .element(
      screen.getByRole("heading", {
        level: 1,
        name: BUS_PASS_CONFIRMATION.heading,
      }),
    )
    .toBeVisible();
  expect(posts[0].body).toMatchObject({
    formSpecVersion: 3,
    answers: {
      serviceRequestType: "addressUpdate",
      firstName: "Ada",
    },
  });
});

test("shows replacement cost information, requires acknowledgement, and submits the replacement request", async () => {
  const posts = stubApi(accepted, currentSpecV4);
  const screen = await renderForm();

  await expect
    .element(screen.getByText(/You will be billed \$10/))
    .not.toBeInTheDocument();

  await screen
    .getByText(
      "I am an existing client and require an address change or replacement bus pass.",
    )
    .click();
  await screen
    .getByText(
      "My bus pass has been lost or stolen. Please mail me a replacement pass application.",
    )
    .click();

  await expect
    .element(screen.getByText(/You will be billed \$10/))
    .toBeVisible();
  const acknowledgement = screen.getByRole("checkbox", {
    name: /I acknowledge that my lost\/stolen bus pass will be cancelled/,
  });
  await expect.element(acknowledgement).toBeVisible();

  await screen.getByRole("textbox", { name: "First name" }).fill("Ada");
  await screen.getByRole("button", { name: "Submit" }).click();

  await expect
    .element(screen.getByText("Acknowledgement is required").first())
    .toBeVisible();
  expect(posts).toHaveLength(0);

  // The design system checkbox keeps its input visually hidden under the
  // label, which is the surface a citizen clicks.
  await screen
    .getByText(/I acknowledge that my lost\/stolen bus pass will be cancelled/)
    .click();
  await expect.element(acknowledgement).toBeChecked();
  await screen.getByRole("button", { name: "Submit" }).click();

  await expect
    .element(
      screen.getByRole("heading", {
        level: 1,
        name: BUS_PASS_CONFIRMATION.heading,
      }),
    )
    .toBeVisible();
  expect(posts[0].body).toMatchObject({
    formSpecVersion: 4,
    answers: {
      serviceRequestType: "replacement",
      acknowledgedPassCancellation: true,
      firstName: "Ada",
    },
  });
});

test("switches from an expanded existing-client choice to a new application", async () => {
  stubApi(accepted, currentSpecV3);
  const screen = await renderForm();

  await screen
    .getByText(
      "I am an existing client and require an address change or replacement bus pass.",
    )
    .click();
  await expect
    .element(
      screen.getByText("I have moved and would like to update my address."),
    )
    .toBeVisible();

  await screen
    .getByText("I am a new applicant and would like to request a bus pass.")
    .click();

  await expect
    .element(
      screen.getByRole("radio", {
        name: "I am a new applicant and would like to request a bus pass.",
      }),
    )
    .toBeChecked();
  await expect
    .element(
      screen.getByText("I have moved and would like to update my address."),
    )
    .not.toBeInTheDocument();
});

test("shows one error when an existing client leaves the nested choice unanswered", async () => {
  stubApi(accepted, currentSpecV3);
  const screen = await renderForm();

  await screen
    .getByText(
      "I am an existing client and require an address change or replacement bus pass.",
    )
    .click();
  await screen.getByRole("textbox", { name: "First name" }).fill("Ada");
  await screen.getByRole("button", { name: "Submit" }).click();

  await expect
    .poll(() => {
      const errors = document.querySelectorAll(
        ".bcds-react-aria-RadioGroup--error",
      );
      return {
        count: errors.length,
        message: errors[0]?.textContent?.trim(),
      };
    })
    .toEqual({
      count: 1,
      message: "Select the type of service you would like to request",
    });
});

test("a rejection from the ministry shows the keyword text and the error code", async () => {
  stubApi(rejected);
  const screen = await renderForm();

  await fillAndSubmit(screen);

  await expect
    .element(screen.getByRole("heading", { name: "Request not accepted" }))
    .toBeVisible();
  await expect
    .element(screen.getByText(/could not accept this request/))
    .toBeVisible();
  await expect.element(screen.getByText("NO_MATCH")).toBeVisible();
  await expect
    .element(screen.getByRole("button", { name: "Start a new request" }))
    .toBeVisible();
});

test("a rejection is worded from the catalogue when it has the keyword", async () => {
  stubApi(rejected, currentSpecV1, {
    "BUSPASS.SUBMIT.REJECTED": "Catalogue wording for a declined request.",
  });
  const screen = await renderForm();

  await fillAndSubmit(screen);

  await expect
    .element(screen.getByRole("heading", { name: "Request not accepted" }))
    .toBeVisible();
  await expect
    .element(screen.getByText("Catalogue wording for a declined request."))
    .toBeVisible();
  await expect
    .element(screen.getByText(/could not accept this request/))
    .not.toBeInTheDocument();
});

test("a rejection keeps the compiled wording when the catalogue cannot be read", async () => {
  stubApi(rejected, currentSpecV1, null);
  const screen = await renderForm();

  await fillAndSubmit(screen);

  await expect
    .element(screen.getByRole("heading", { name: "Request not accepted" }))
    .toBeVisible();
  await expect
    .element(screen.getByText(/could not accept this request/))
    .toBeVisible();
});

test("a throttled request is worded from the catalogue when it has the keyword", async () => {
  stubApi(throttled, currentSpecV1, {
    "BUSPASS.SUBMIT.RATE_LIMITED": "Catalogue wording for a throttled request.",
  });
  const screen = await renderForm();

  await fillAndSubmit(screen);

  await expect
    .element(screen.getByRole("heading", { name: "Too many requests" }))
    .toBeVisible();
  await expect
    .element(screen.getByText("Catalogue wording for a throttled request."))
    .toBeVisible();
});

test("a request the ministry may already hold shows the id and removes the form", async () => {
  stubApi(unavailable(true));
  const screen = await renderForm();

  await fillAndSubmit(screen);

  await expect
    .element(screen.getByRole("heading", { name: "Request saved" }))
    .toBeVisible();
  await expect
    .element(screen.getByText(/Do not submit it again/))
    .toBeVisible();
  await expect
    .element(screen.getByText("11111111-2222-3333-4444-555555555555"))
    .toBeVisible();
  // No form, so it cannot be sent a second time.
  await expect
    .element(screen.getByRole("button", { name: "Submit" }))
    .not.toBeInTheDocument();
});

test("a request that provably never arrived keeps the form, and it can be sent again", async () => {
  const posts = stubApi(unavailable(false));
  const screen = await renderForm();

  await fillAndSubmit(screen);

  await expect
    .element(
      screen.getByRole("heading", { name: "Request saved but not sent" }),
    )
    .toBeVisible();
  await expect
    .element(screen.getByText("11111111-2222-3333-4444-555555555555"))
    .toBeVisible();

  // Form.io disables its submit button on click; the component must
  // re-enable it once the attempt fails, or "try again" is impossible.
  await screen.getByRole("button", { name: "Submit" }).click();
  await expect.poll(() => posts.length).toBe(2);
});

test("a throttled request keeps the form and says to wait", async () => {
  stubApi(throttled);
  const screen = await renderForm();

  await fillAndSubmit(screen);

  await expect
    .element(screen.getByRole("heading", { name: "Too many requests" }))
    .toBeVisible();
  await expect
    .element(screen.getByRole("button", { name: "Submit" }))
    .toBeVisible();
});

test("a refused submission lists every reason and focuses the field on request", async () => {
  stubApi(refused);
  const screen = await renderForm();

  await fillAndSubmit(screen);

  await expect.element(screen.getByText("There is a problem")).toBeVisible();
  await screen
    .getByRole("button", {
      name: "A Social Insurance Number or bus pass account number is required",
    })
    .click();
  await expect
    .element(screen.getByRole("textbox", { name: "First name" }))
    .toHaveFocus();
});

test("a refused submission keeps every answer, ready to send again", async () => {
  const posts = stubApi(refused);
  const screen = await renderForm();

  await fillAndSubmit(screen);
  await expect.element(screen.getByText("There is a problem")).toBeVisible();

  // The answer is still in the field after the refusal...
  await expect
    .element(screen.getByRole("textbox", { name: "First name" }))
    .toHaveValue("Ada");

  // ...and is what the next attempt sends, without typing it again.
  await screen.getByRole("button", { name: "Submit" }).click();
  await vi.waitFor(() => expect(posts).toHaveLength(2));
  expect(posts[1].body).toMatchObject({ answers: { firstName: "Ada" } });
});

test("says the request is being submitted while it is on its way", async () => {
  let release: () => void = () => {};
  const wait = new Promise<void>((resolve) => {
    release = resolve;
  });
  const posts = stubApi({ ...accepted, wait });
  const screen = await renderForm();

  await fillAndSubmit(screen);

  // While the API has not answered: a message, announced, and no second post.
  const message = screen.getByText("Your request is being submitted.");
  await expect.element(message).toBeVisible();
  // Inside a live region (<output>, role "status"), so screen readers
  // announce it.
  expect(message.element().closest("output")).not.toBeNull();
  await expect
    .element(
      screen.getByRole("progressbar", { name: "Submitting your request" }),
    )
    .toBeInTheDocument();
  await screen.getByRole("button", { name: "Submit" }).click({ force: true });
  expect(posts).toHaveLength(1);

  release();

  await expect.element(screen.getByText("1-TEST-0001")).toBeVisible();
  expect(document.body.textContent).not.toContain(
    "Your request is being submitted.",
  );
});

type Screen = Awaited<ReturnType<typeof renderForm>>;

const BUILT_IN_FIRST_LINE =
  "The BC Bus Pass Program has received your request. Your reference number is 1-TEST-0001. Keep it for your records.";
const BUILT_IN_SECOND_LINE =
  "We may contact you if we need further information for your request.";

/** The page heading the confirmation shows, after a submit. */
async function submittedHeading(screen: Screen, name: string) {
  await fillAndSubmit(screen);
  const heading = screen.getByRole("heading", { level: 1, name });
  await expect.element(heading).toBeVisible();
  return heading;
}

test("shows the configured confirmation with the reference number in place, focused, without the submission ID", async () => {
  stubApi(accepted, currentSpecV1, {}, published(configuredMessage));
  const screen = await renderForm();

  const heading = await submittedHeading(screen, configuredMessage.heading);

  await expect.element(heading).toHaveFocus();
  const alert = screen.getByRole("note", { name: configuredMessage.title });
  await expect
    .element(alert.getByText("Thank you. Your reference number is 1-TEST-0001."))
    .toBeVisible();
  // No extra reference line, since the message shows the placeholder.
  expect(alert.element().textContent?.split("1-TEST-0001")).toHaveLength(2);
  const text = document.body.textContent ?? "";
  expect(text).not.toContain("Submission ID");
  expect(text).not.toContain("11111111-2222-3333-4444-555555555555");
  expect(text).not.toContain(BUS_PASS_CONFIRMATION.heading);
});

test("renders bold text, a link and a bulleted list from the configured message", async () => {
  stubApi(
    accepted,
    currentSpecV1,
    {},
    published({
      ...configuredMessage,
      body:
        "Your reference number is **{referenceNumber}**.\n\n" +
        "Next:\n- We check your details\n" +
        "- [Contact the program](https://www2.gov.bc.ca/buspass) if anything changes",
    }),
  );
  const screen = await renderForm();

  await submittedHeading(screen, configuredMessage.heading);

  const alert = screen.getByRole("note", { name: configuredMessage.title });
  expect(alert.element().querySelector("strong")?.textContent).toBe(
    "1-TEST-0001",
  );
  expect(alert.getByRole("listitem").elements()).toHaveLength(2);
  const link = alert.getByRole("link", {
    name: "Contact the program (opens in a new tab)",
  });
  await expect
    .element(link)
    .toHaveAttribute("href", "https://www2.gov.bc.ca/buspass");
  await expect.element(link).toHaveAttribute("target", "_blank");
});

// A failed read is tried once more, so it has ended after two reads.
test.each([
  ["no message is published", { status: 404 }, 1],
  ["the message cannot be read", { status: 502, body: { status: 502 } }, 2],
  ["the message is malformed", { status: 200, body: { payload: { heading: "Only" } } }, 2],
  ["the message read never finishes", "never", 1],
] as const)(
  "shows the built-in confirmation when %s, and the request is still sent",
  async (_case, message, reads) => {
    const posts = stubApi(accepted, currentSpecV1, {}, message);
    const screen = await renderForm();
    await expect.poll(() => messageReads).toBe(reads);

    const heading = await submittedHeading(screen, BUS_PASS_CONFIRMATION.heading);

    await expect.element(heading).toHaveFocus();
    const alert = screen.getByRole("note", { name: "Request submitted" });
    await expect.element(alert.getByText(BUILT_IN_FIRST_LINE)).toBeVisible();
    await expect.element(alert.getByText(BUILT_IN_SECOND_LINE)).toBeVisible();
    expect(posts).toHaveLength(1);
  },
);

test("adds the reference number on a line of its own when the message does not show it", async () => {
  stubApi(
    accepted,
    currentSpecV1,
    {},
    published({ ...configuredMessage, body: "Thank you for your request." }),
  );
  const screen = await renderForm();

  await submittedHeading(screen, configuredMessage.heading);

  await expect
    .element(screen.getByText("Your reference number is 1-TEST-0001."))
    .toBeVisible();
});

test("shows the submission ID in place of a reference number the ministry did not return", async () => {
  const withoutReference: Answer = {
    status: 200,
    body: {
      payload: {
        ...(accepted.body as { payload: object }).payload,
        referenceNumber: null,
      },
    },
  };
  stubApi(withoutReference, currentSpecV1, {}, published(configuredMessage));
  const screen = await renderForm();

  await submittedHeading(screen, configuredMessage.heading);

  await expect
    .element(
      screen.getByText(
        "Thank you. Your reference number is 11111111-2222-3333-4444-555555555555.",
      ),
    )
    .toBeVisible();
});

test("asks for the message when the form opens, not at submit, and again when the form opens again", async () => {
  stubApi(accepted, currentSpecV1, {}, published(configuredMessage));
  const queryClient = newQueryClient();
  const first = await renderForm(queryClient);
  await expect.poll(() => messageReads).toBe(1);

  await submittedHeading(first, configuredMessage.heading);
  expect(messageReads).toBe(1);

  await first.unmount();
  const second = await renderForm(queryClient);
  await expect
    .element(second.getByRole("textbox", { name: "First name" }))
    .toBeVisible();
  await expect.poll(() => messageReads).toBe(2);
});

describe("confirmation layout", () => {
  let initial = { width: 0, height: 0 };

  beforeAll(loadPageStyles);

  beforeEach(() => {
    initial = { width: window.innerWidth, height: window.innerHeight };
  });

  afterEach(async () => {
    await page.viewport(initial.width, initial.height);
  });

  /** The pixel size a font-size value resolves to on this page. */
  function resolvedFontSize(value: string): string {
    const probe = document.createElement("span");
    probe.style.fontSize = value;
    document.body.append(probe);
    const size = getComputedStyle(probe).fontSize;
    probe.remove();
    return size;
  }

  test("fits a 320-pixel phone, its heading at the phone heading size", async () => {
    await page.viewport(320, 640);
    // An email address typed as plain text has no break opportunity.
    stubApi(
      accepted,
      currentSpecV1,
      {},
      published({
        ...configuredMessage,
        body: "Your reference number is {referenceNumber}. Questions: SDPR.BusPassProgramEnquiries@gov.bc.ca",
      }),
    );
    const screen = await renderForm();

    const heading = await submittedHeading(screen, configuredMessage.heading);

    const root = document.documentElement;
    expect(root.scrollWidth).toBeLessThanOrEqual(root.clientWidth);
    const alert = screen.getByRole("note").element().closest(".bcds-Inline-Alert");
    expect(alert?.getBoundingClientRect().right).toBeLessThanOrEqual(
      root.clientWidth,
    );
    expect(getComputedStyle(heading.element()).fontSize).toBe(
      resolvedFontSize("var(--typography-font-size-h2)"),
    );
  });
});

function contactGroup(screen: Screen) {
  return screen.getByRole("radiogroup", {
    name: "Preferred method of communication (required)",
  });
}

/** Clicks an option's label, the surface a citizen clicks; the design system hides the input. */
async function choose(screen: Screen, name: string) {
  await contactGroup(screen).getByText(name, { exact: true }).click();
}

test("asks for the preferred method of communication with radio buttons, none chosen", async () => {
  stubApi(accepted, contactSpecV9);
  const screen = await renderForm();
  const group = contactGroup(screen);

  await expect.element(group).toBeVisible();
  await expect.element(group).toHaveAttribute("aria-required", "true");
  const options = group.getByRole("radio").elements() as HTMLInputElement[];
  expect(options.map((option) => option.value)).toEqual(["phone", "email"]);
  for (const name of ["Phone", "Email"]) {
    await expect
      .element(group.getByRole("radio", { name, exact: true }))
      .not.toBeChecked();
  }
  await expect
    .element(
      screen.getByRole("button", { name: /Preferred method of communication/ }),
    )
    .not.toBeInTheDocument();
});

test("refuses to send without a contact choice and says why under the question", async () => {
  const posts = stubApi(accepted, contactSpecV9);
  const screen = await renderForm();

  await screen.getByRole("textbox", { name: "Phone number" }).fill("2505550199");
  await screen.getByRole("button", { name: "Submit" }).click();

  const group = contactGroup(screen);
  await expect.element(group).toHaveAttribute("aria-invalid", "true");
  await expect
    .element(group.getByText("Preferred method of communication is required"))
    .toBeVisible();
  expect(posts).toHaveLength(0);
});

test("requires the email address only while Email is the chosen method", async () => {
  const posts = stubApi(accepted, contactSpecV9);
  const screen = await renderForm();

  await screen.getByRole("textbox", { name: "Phone number" }).fill("2505550199");
  await expect
    .element(screen.getByRole("textbox", { name: "Email", exact: true }))
    .toBeVisible();

  await choose(screen, "Email");
  await expect
    .element(
      screen.getByRole("textbox", { name: /^Email ?\(required\)$/ }),
    )
    .toBeVisible();
  await screen.getByRole("button", { name: "Submit" }).click();
  await expect
    .element(
      screen
        .getByText(
          "An email address is required when email is the preferred means of communication",
        )
        .first(),
    )
    .toBeVisible();
  expect(posts).toHaveLength(0);

  await choose(screen, "Phone");
  await expect
    .element(screen.getByRole("textbox", { name: "Email", exact: true }))
    .toBeVisible();
});

test("posts the chosen method under the dropdown's key, with the version rendered", async () => {
  const posts = stubApi(accepted, contactSpecV9);
  const screen = await renderForm();

  await screen.getByRole("textbox", { name: "Phone number" }).fill("2505550199");
  await choose(screen, "Email");
  await screen
    .getByRole("textbox", { name: /^Email ?\(required\)$/ })
    .fill("ada@example.com");
  await screen
    .getByRole("textbox", { name: /^Email verification ?\(required\)$/ })
    .fill("ada@example.com");
  await screen.getByRole("button", { name: "Submit" }).click();

  await vi.waitFor(() => expect(posts).toHaveLength(1));
  expect(posts[0].body).toMatchObject({
    formSpecVersion: 9,
    answers: {
      preferredCommunication: "email",
      email: "ada@example.com",
      phoneNumber: "(250) 555-0199",
    },
  });
});

test("keeps one contact method when the choice changes", async () => {
  stubApi(accepted, contactSpecV9);
  const screen = await renderForm();
  const group = contactGroup(screen);

  await expect.element(group).toBeVisible();
  await choose(screen, "Phone");
  await expect
    .element(group.getByRole("radio", { name: "Phone", exact: true }))
    .toBeChecked();

  await choose(screen, "Email");
  await expect
    .element(group.getByRole("radio", { name: "Email", exact: true }))
    .toBeChecked();
  await expect
    .element(group.getByRole("radio", { name: "Phone", exact: true }))
    .not.toBeChecked();
});
