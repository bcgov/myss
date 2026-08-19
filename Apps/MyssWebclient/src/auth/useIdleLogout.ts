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

    // Keep the latest logout without restarting the timer effect each render.
    //
    // The write lives in an effect rather than in the render body: React may
    // discard or re-run a render, so mutating a ref there is not guaranteed to
    // correspond to committed UI (react-hooks/refs). Deferring it to commit is
    // unobservable here because `logoutRef.current` is only ever read from the
    // timer callback, never during render, and useRef already seeds the initial
    // value so there is no gap on first mount.
    const logoutRef = useRef(logout);
    useEffect(() => {
        logoutRef.current = logout;
    }, [logout]);

    const warningRef = useRef(false);

    useEffect(() => {
        // Nothing to time while signed out. The warning is cleared by masking
        // it on the way out (see the return below) rather than by calling
        // setWarning here: a setState in an effect body triggers a cascading
        // render, and there is no external state to synchronise with.
        if (!isAuthenticated) return;

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
            warningRef.current = false;
        };
    }, [isAuthenticated]);

    // Derived rather than stored. Every remaining setWarning call now sits in a
    // callback — the timer firing or a DOM activity event — which is the shape
    // the effect rules ask for: subscribe to an external system, set state from
    // its callbacks.
    //
    // The `warning` state can therefore linger as true after sign-out. That is
    // unobservable in practice: siteMinderLogout ends in
    // window.location.assign, a full navigation that discards React state
    // entirely, and signing back in is likewise a redirect. Masking here covers
    // the gap between removeUser() flipping isAuthenticated and the browser
    // actually leaving the page.
    return { warning: isAuthenticated && warning };
}
