import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { renderHook } from "vitest-browser-react";
import { IDLE_MS } from "@myss/web-common/auth";

import type { Session } from "./useSession";

const session = vi.hoisted(
    () =>
        ({
            user: { sub: "client", roles: [] },
            isAuthenticated: true,
            isLoading: false,
            isMeLoading: false,
            login: vi.fn(),
            loginSilently: vi.fn(),
            logout: vi.fn(),
        }) as Session & { logout: ReturnType<typeof vi.fn> },
);

vi.mock("./useSession", () => ({
    useSession: () => session,
}));


import { useIdleLogout } from "./useIdleLogout";

// The timer's behaviour is tested in @myss/web-common; this pins the wiring to
// the session.
describe("useIdleLogout", () => {
    beforeEach(() => {
        vi.useFakeTimers();
        session.isAuthenticated = true;
        session.logout.mockClear();
    });

    afterEach(() => {
        vi.clearAllTimers();
        vi.useRealTimers();
    });

    it("signs the session out when the inactivity deadline expires", async () => {
        const hook = await renderHook(() => useIdleLogout());

        await hook.act(() => vi.advanceTimersByTime(IDLE_MS));

        expect(session.logout).toHaveBeenCalledOnce();
        await hook.unmount();
    });

    it("times nothing while signed out", async () => {
        session.isAuthenticated = false;
        const hook = await renderHook(() => useIdleLogout());

        await hook.act(() => vi.advanceTimersByTime(IDLE_MS));

        expect(hook.result.current.warning).toBe(false);
        expect(session.logout).not.toHaveBeenCalled();
        await hook.unmount();
    });
});
