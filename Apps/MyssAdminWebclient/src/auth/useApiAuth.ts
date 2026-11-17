// Feeds the OIDC access token to authHeaders(), which every admin API call
// sends as its Bearer header. The citizen webclient also wires the token into
// its generated API client; the admin app has no generated client, so
// publishing the token is all there is.

import { useAuth } from "react-oidc-context";
import { setAccessToken } from "@myss/web-common/auth";


export function useApiAuth(): void {
    const auth = useAuth();

    // Publish the freshest token on every render so authHeaders() always sees
    // the current value. Signing out sets this back to undefined, so the token
    // cannot outlive the session.
    setAccessToken(auth.user?.access_token);
}
