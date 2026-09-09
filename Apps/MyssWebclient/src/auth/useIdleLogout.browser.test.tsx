import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { renderHook } from "vitest-browser-react";

import type { Session } from "./useSession";

const session = vi.hoisted(
    () =>
        ({
            user: { sub: "client", roles: [] },
            isAuthenticated: true,
            isLoading: false,
            isMeLoading: false,
            login: vi.fn(),
            logout: vi.fn(),
        }) as Session & { logout: ReturnType<typeof vi.fn> },
);

vi.mock("./useSession", () => ({
    useSession: () => session,
}));

import { IDLE_MS, useIdleLogout, WARNING_MS } from "./useIdleLogout";

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

    it("resets inactivity when activity occurs before the warning", async () => {
        const hook = await renderHook(() => useIdleLogout());

        await hook.act(() => vi.advanceTimersByTime(WARNING_MS - 1_000));
        await hook.act(() => window.dispatchEvent(new Event("mousedown")));
        await hook.act(() => vi.advanceTimersByTime(1_000));

        expect(hook.result.current.warning).toBe(false);

        await hook.act(() => vi.advanceTimersByTime(WARNING_MS - 1_000));

        expect(hook.result.current.warning).toBe(true);
        await hook.unmount();
    });

    it("requires explicit extension after the warning appears", async () => {
        const hook = await renderHook(() => useIdleLogout());

        await hook.act(() => vi.advanceTimersByTime(WARNING_MS));
        expect(hook.result.current.warning).toBe(true);

        await hook.act(() => window.dispatchEvent(new Event("mousemove")));
        expect(hook.result.current.warning).toBe(true);

        await hook.unmount();
    });

    it("starts a fresh timeout cycle when the session is extended", async () => {
        const hook = await renderHook(() => useIdleLogout());

        await hook.act(() => vi.advanceTimersByTime(WARNING_MS));
        await hook.act(() => hook.result.current.extendSession());

        expect(hook.result.current.warning).toBe(false);

        await hook.act(() => vi.advanceTimersByTime(WARNING_MS));

        expect(hook.result.current.warning).toBe(true);
        expect(session.logout).not.toHaveBeenCalled();
        await hook.unmount();
    });

    it("logs out when the inactivity deadline expires", async () => {
        const hook = await renderHook(() => useIdleLogout());

        await hook.act(() => vi.advanceTimersByTime(IDLE_MS));

        expect(session.logout).toHaveBeenCalledOnce();
        await hook.unmount();
    });

    it("does not retain a warning across re-authentication", async () => {
        const hook = await renderHook(() => useIdleLogout());

        await hook.act(() => vi.advanceTimersByTime(WARNING_MS));
        expect(hook.result.current.warning).toBe(true);

        session.isAuthenticated = false;
        await hook.rerender();
        expect(hook.result.current.warning).toBe(false);

        session.isAuthenticated = true;
        await hook.rerender();
        expect(hook.result.current.warning).toBe(false);

        await hook.unmount();
    });
});
