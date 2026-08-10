import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";

import { IdleTimer, IDLE_MS, WARNING_MS } from "./useIdleLogout";

describe("IdleTimer (RULE-IDA-07: 15-min idle, 14-min warning)", () => {
    beforeEach(() => vi.useFakeTimers());
    afterEach(() => vi.useRealTimers());

    it("exposes the mandated timings", () => {
        expect(IDLE_MS).toBe(15 * 60 * 1000);
        expect(WARNING_MS).toBe(14 * 60 * 1000);
    });

    it("warns at 14 minutes and logs out at 15", () => {
        const onWarn = vi.fn();
        const onLogout = vi.fn();
        const timer = new IdleTimer({ onWarn, onLogout });
        timer.start();

        vi.advanceTimersByTime(WARNING_MS - 1);
        expect(onWarn).not.toHaveBeenCalled();

        vi.advanceTimersByTime(1);
        expect(onWarn).toHaveBeenCalledOnce();
        expect(onLogout).not.toHaveBeenCalled();

        vi.advanceTimersByTime(IDLE_MS - WARNING_MS);
        expect(onLogout).toHaveBeenCalledOnce();

        timer.stop();
    });

    it("reset() reschedules so activity defers the warning", () => {
        const onWarn = vi.fn();
        const onLogout = vi.fn();
        const timer = new IdleTimer({ onWarn, onLogout });
        timer.start();

        vi.advanceTimersByTime(WARNING_MS - 1000);
        timer.reset();
        vi.advanceTimersByTime(1000);
        expect(onWarn).not.toHaveBeenCalled();

        vi.advanceTimersByTime(WARNING_MS - 1000);
        expect(onWarn).toHaveBeenCalledOnce();

        timer.stop();
    });

    it("stop() cancels all pending callbacks", () => {
        const onWarn = vi.fn();
        const onLogout = vi.fn();
        const timer = new IdleTimer({ onWarn, onLogout });
        timer.start();
        timer.stop();
        vi.advanceTimersByTime(IDLE_MS * 2);
        expect(onWarn).not.toHaveBeenCalled();
        expect(onLogout).not.toHaveBeenCalled();
    });
});
