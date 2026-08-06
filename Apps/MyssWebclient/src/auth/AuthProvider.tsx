// Option-1-ONLY. The single OIDC-library surface in the app. Wraps the tree in
// react-oidc-context so useSession/useApiAuth can read the auth state. Removed
// entirely under Option 2 (the API owns the OIDC flow).

import type { ReactNode } from "react";
import { AuthProvider as OidcAuthProvider } from "react-oidc-context";

import { oidcConfig } from "./oidcConfig";

export function AuthProvider({ children }: { children: ReactNode }) {
    return <OidcAuthProvider {...oidcConfig}>{children}</OidcAuthProvider>;
}
