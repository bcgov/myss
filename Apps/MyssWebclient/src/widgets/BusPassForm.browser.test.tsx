import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router";
import { render } from "vitest-browser-react";
import { afterEach, expect, test, vi } from "vitest";

import BusPassForm from "@/widgets/BusPassForm";

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

type Answer = { status: number; body: unknown };

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

function stubApi(answer: Answer) {
    const posts: Array<{ url: string; body: unknown }> = [];
    vi.spyOn(window, "fetch").mockImplementation(async (input, init) => {
        const url = String(input);
        if (url.endsWith("/v1/forms/bc-bus-pass/spec")) {
            return new Response(JSON.stringify({ payload: currentSpecV1 }), {
                status: 200,
                headers: { "Content-Type": "application/json" },
            });
        }
        if (
            url.endsWith("/v1/bus-pass/submissions") &&
            init?.method === "POST"
        ) {
            posts.push({ url, body: JSON.parse(String(init.body)) });
            return new Response(JSON.stringify(answer.body), {
                status: answer.status,
                headers: { "Content-Type": "application/json" },
            });
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

test("posts to the bus pass endpoint with the rendered version and shows the reference number", async () => {
    const posts = stubApi(accepted);
    const screen = await renderForm();

    await fillAndSubmit(screen);

    await expect
        .element(screen.getByRole("heading", { name: "Request submitted" }))
        .toBeVisible();
    await expect.element(screen.getByText("1-TEST-0001")).toBeVisible();
    expect(posts).toHaveLength(1);
    expect(posts[0].url).toContain("/v1/bus-pass/submissions");
    expect(posts[0].body).toMatchObject({
        formSpecVersion: 1,
        answers: { firstName: "Ada" },
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
