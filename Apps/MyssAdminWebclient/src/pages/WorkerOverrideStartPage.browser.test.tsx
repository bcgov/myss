import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter, Route, Routes, useParams } from "react-router";
import { render } from "vitest-browser-react";
import { afterEach, describe, expect, it, vi } from "vitest";

import WorkerOverrideStartPage from "./WorkerOverrideStartPage";

// Where a worker starts a worker override application (MYSS-321): starting
// makes a draft and opens it by its link.

const ID = "11111111-2222-3333-4444-555555555555";

function json(status: number, body: unknown) {
    return new Response(JSON.stringify(body), {
        status,
        headers: { "Content-Type": "application/json" },
    });
}

function Opened() {
    const { id } = useParams<{ id: string }>();
    return <p>Opened draft {id}</p>;
}

function renderPage() {
    const queryClient = new QueryClient({
        defaultOptions: { queries: { retry: false } },
    });
    return render(
        <QueryClientProvider client={queryClient}>
            <MemoryRouter initialEntries={["/worker-override"]}>
                <Routes>
                    <Route
                        path="/worker-override"
                        element={<WorkerOverrideStartPage />}
                    />
                    <Route path="/worker-override/:id" element={<Opened />} />
                </Routes>
            </MemoryRouter>
        </QueryClientProvider>,
    );
}

afterEach(() => {
    vi.restoreAllMocks();
});

describe("WorkerOverrideStartPage", () => {
    it("starts a draft and opens it", async () => {
        const fetchMock = vi
            .spyOn(window, "fetch")
            .mockResolvedValue(
                json(201, { payload: { id: ID, status: "DRAFT", version: 1 } }),
            );
        const screen = await renderPage();

        await screen
            .getByRole("button", { name: "Start an application" })
            .click();

        await expect
            .element(screen.getByText(`Opened draft ${ID}`))
            .toBeVisible();
        const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
        expect(url).toMatch(/\/v1\/intake\/worker-override\/applications$/);
        expect(init.method).toBe("POST");
    });

    it("says when the form has not been published", async () => {
        vi.spyOn(window, "fetch").mockResolvedValue(
            json(404, { keyword: "INTAKE.APPLICATION.SPEC_NOT_PUBLISHED" }),
        );
        const screen = await renderPage();

        await screen
            .getByRole("button", { name: "Start an application" })
            .click();

        await expect
            .element(screen.getByRole("alert"))
            .toHaveTextContent(
                "The worker override form has not been published yet.",
            );
    });
});
