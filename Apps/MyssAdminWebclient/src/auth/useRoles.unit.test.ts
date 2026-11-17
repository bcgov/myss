import { beforeEach, describe, expect, it, vi } from "vitest";

const state = vi.hoisted(() => ({
    isAuthenticated: true,
    me: { data: undefined as { roles: string[] } | undefined, isPending: true },
}));

vi.mock("./useSession", () => ({
    useSession: () => ({
        isAuthenticated: state.isAuthenticated,
        user: { sub: "idir-user" },
    }),
}));

vi.mock("./useMe", () => ({
    useMe: () => state.me,
}));

import { useRoles } from "./useRoles";

describe("useRoles", () => {
    beforeEach(() => {
        state.isAuthenticated = true;
        state.me = { data: undefined, isPending: true };
    });

    it("is loading while a signed-in caller's roles are on their way, and grants nothing yet", () => {
        expect(useRoles()).toEqual({ roles: [], isLoading: true, canReview: false });
    });

    it("is not loading for a signed-out caller (the query never runs)", () => {
        state.isAuthenticated = false;

        expect(useRoles().isLoading).toBe(false);
    });

    it.each([["WORKER"], ["ADMIN"]])("lets %s review, as the API's WorkerWithIdir policy does", (role) => {
        state.me = { data: { roles: [role] }, isPending: false };

        expect(useRoles().canReview).toBe(true);
    });

    it("does not let CLIENT, or no role, review", () => {
        state.me = { data: { roles: ["CLIENT"] }, isPending: false };
        expect(useRoles().canReview).toBe(false);

        state.me = { data: { roles: [] }, isPending: false };
        expect(useRoles().canReview).toBe(false);
    });
});
