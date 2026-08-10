// PERMANENT (identical in both options). RULE-IDA-07: 15-minute idle timeout
// with a 14-minute warning. The IdleTimer class holds the (framework-free)
// scheduling logic so it is unit-testable with fake timers; the hook wires it
// to DOM activity and the session's logout().

import { useEffect, useRef, useState } from "react";

import { useSession } from "./useSession";

export const IDLE_MS = 15 * 60 * 1000; // hard logout
export const WARNING_MS = 14 * 60 * 1000; // warn one minute before

export interface IdleTimerOptions {
    onWarn: () => void;
    onLogout: () => void;
    idleMs?: number;
    warningMs?: number;
}

export class IdleTimer {
    private readonly onWarn: () => void;
    private readonly onLogout: () => void;
    private readonly idleMs: number;
    private readonly warningMs: number;
    private warnHandle: ReturnType<typeof setTimeout> | undefined;
    private logoutHandle: ReturnType<typeof setTimeout> | undefined;

    constructor(options: IdleTimerOptions) {
        this.onWarn = options.onWarn;
        this.onLogout = options.onLogout;
        this.idleMs = options.idleMs ?? IDLE_MS;
        this.warningMs = options.warningMs ?? WARNING_MS;
    }

    start(): void {
        this.warnHandle = setTimeout(this.onWarn, this.warningMs);
        this.logoutHandle = setTimeout(this.onLogout, this.idleMs);
    }

    reset(): void {
        this.stop();
        this.start();
    }

    stop(): void {
        if (this.warnHandle !== undefined) clearTimeout(this.warnHandle);
        if (this.logoutHandle !== undefined) clearTimeout(this.logoutHandle);
        this.warnHandle = undefined;
        this.logoutHandle = undefined;
    }
}

const ACTIVITY_EVENTS = [
    "mousemove",
    "mousedown",
    "keydown",
    "scroll",
    "touchstart",
] as const;

// Mount once (in App). Returns `warning`, true during the final minute so the
// app can show the 14-minute warning; any user activity resets the timer and
// clears the warning. Inactive while unauthenticated.
export function useIdleLogout(): { warning: boolean } {
    const { isAuthenticated, logout } = useSession();
    const [warning, setWarning] = useState(false);

    // Keep the latest logout without restarting the effect each render.
    const logoutRef = useRef(logout);
    logoutRef.current = logout;
    const warningRef = useRef(false);

    useEffect(() => {
        if (!isAuthenticated) {
            warningRef.current = false;
            setWarning(false);
            return;
        }

        const showWarning = () => {
            warningRef.current = true;
            setWarning(true);
        };
        const clearWarning = () => {
            if (warningRef.current) {
                warningRef.current = false;
                setWarning(false);
            }
        };

        const timer = new IdleTimer({
            onWarn: showWarning,
            onLogout: () => logoutRef.current(),
        });
        timer.start();

        const onActivity = () => {
            timer.reset();
            clearWarning();
        };
        ACTIVITY_EVENTS.forEach((e) =>
            window.addEventListener(e, onActivity, { passive: true }),
        );

        return () => {
            timer.stop();
            ACTIVITY_EVENTS.forEach((e) =>
                window.removeEventListener(e, onActivity),
            );
        };
    }, [isAuthenticated]);

    return { warning };
}
