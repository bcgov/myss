// RULE-IDA-07: 15-minute idle timeout with a 14-minute warning. The timer and
// the hook are @myss/web-common's; this wires them to the session.

import { useIdleTimeout } from "@myss/web-common/auth";

import { useSession } from "./useSession";

// Mount once (in App). Inactive while unauthenticated.
export function useIdleLogout(): {
    warning: boolean;
    extendSession: () => void;
} {
    const { isAuthenticated, logout } = useSession();
    return useIdleTimeout(isAuthenticated, logout);
}
