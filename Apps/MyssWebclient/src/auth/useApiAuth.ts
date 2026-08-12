// THE swappable line (plan §3.8). Feeds the OIDC access token into the
// generated @hey-api client as a Bearer header on every request.
//
// We use a request *interceptor* rather than client.setConfig({ auth }) on
// purpose: the built-in auth callback is a no-op until the API emits a Swagger
// Bearer security scheme AND `generate:schema` is re-run so the generated ops
// invoke it (plan §3.8 / §11.4). The interceptor works regardless, so the
// frontend is not coupled to backend Swagger regeneration.
//
// Option 2 replaces this whole file with one line in main.tsx:
//   client.setConfig({ credentials: "include" })

import { useEffect } from "react";
import { useAuth } from "react-oidc-context";

import { client } from "@/api/generated/client.gen";
import { getAccessToken, setAccessToken } from "@/auth/accessToken";

// Exported for unit testing. Returns a hey-api request interceptor that adds
// `Authorization: Bearer <token>` when a token is available, reading it lazily
// so the latest (possibly renewed) token is always used.
export function bearerInterceptor(getToken: () => string | undefined) {
    return (request: Request, _options?: unknown): Request => {
        const token = getToken();
        if (token) {
            request.headers.set("Authorization", `Bearer ${token}`);
        }
        return request;
    };
}

export function useApiAuth(): void {
    const auth = useAuth();

    // Publish the freshest token on every render so both readers below always
    // see the current value without re-registering on every renew:
    //   - the interceptor (registered once) for generated-client calls
    //   - authHeaders() for the raw-fetch call sites in hooks/usePocForm.ts
    // Signing out sets this back to undefined, so the token cannot outlive the
    // session.
    setAccessToken(auth.user?.access_token);

    useEffect(() => {
        const id = client.interceptors.request.use(
            bearerInterceptor(getAccessToken),
        );
        return () => {
            client.interceptors.request.eject(id);
        };
    }, []);
}
