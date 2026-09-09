// SiteMinder-aware logout. BC Gov citizen IdPs sit behind SiteMinder, so
// oidc-client-ts' signoutRedirect alone leaves the SSO session alive. We
// redirect through the SiteMinder logoff, wrapping the Keycloak end-session URL.
// Pattern lifted from the keycloak-example-apps React example. This concern is
// shared with Option 2, so the URL builders are written to be reusable.

import type { AuthContextProps } from "react-oidc-context";

import { paths } from "@/routes/paths";

// Default SiteMinder logoff endpoint (prod). Dev/test uses logontest7.
const DEFAULT_SITEMINDER_LOGOFF = "https://logon7.gov.bc.ca/clp-cgi/logoff.cgi";

let logoutInProgress = false;

export function isLogoutInProgress(): boolean {
    return logoutInProgress;
}

export function buildKeycloakLogoutUrl(params: {
    authority: string;
    idTokenHint?: string;
    postLogoutRedirectUri: string;
}): string {
    const url = new URL(`${params.authority}/protocol/openid-connect/logout`);
    url.searchParams.set(
        "post_logout_redirect_uri",
        params.postLogoutRedirectUri,
    );
    if (params.idTokenHint) {
        url.searchParams.set("id_token_hint", params.idTokenHint);
    }
    return url.toString();
}

export function buildSiteMinderLogoutUrl(params: {
    authority: string;
    idTokenHint?: string;
    postLogoutRedirectUri: string;
    siteMinderLogoffUrl?: string;
}): string {
    const keycloakLogout = buildKeycloakLogoutUrl(params);
    const base = params.siteMinderLogoffUrl ?? DEFAULT_SITEMINDER_LOGOFF;
    const url = new URL(base);
    url.searchParams.set("retnow", "1");
    url.searchParams.set("returl", keycloakLogout);
    return url.toString();
}

// Runtime logout used by the useSession seam. Clearing the local user and
// leaving the SPA discards its in-memory state; the external logout chain then
// returns the client to the landing page with a clean session.
export async function siteMinderLogout(
    auth: AuthContextProps,
    opts: { siteMinderLogoffUrl?: string } = {},
): Promise<void> {
    const idTokenHint = auth.user?.id_token;
    const url = buildSiteMinderLogoutUrl({
        authority: auth.settings.authority,
        idTokenHint,
        postLogoutRedirectUri: new URL(
            paths.home, window.location.origin).toString(),
        siteMinderLogoffUrl: opts.siteMinderLogoffUrl,
    });
    logoutInProgress = true;
    try {
        await auth.removeUser();
        window.location.assign(url);
    } finally {
        logoutInProgress = false;
    }
}
