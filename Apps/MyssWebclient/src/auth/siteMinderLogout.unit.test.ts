import { describe, it, expect } from "vitest";

import {
    buildKeycloakLogoutUrl,
    buildSiteMinderLogoutUrl,
} from "./siteMinderLogout";

describe("buildKeycloakLogoutUrl", () => {
    it("includes id_token_hint and post_logout_redirect_uri", () => {
        const url = new URL(
            buildKeycloakLogoutUrl({
                authority:
                    "https://dev.loginproxy.gov.bc.ca/auth/realms/standard",
                idTokenHint: "id.token.jwt",
                postLogoutRedirectUri: "http://localhost:5173",
            }),
        );
        expect(url.pathname).toBe(
            "/auth/realms/standard/protocol/openid-connect/logout",
        );
        expect(url.searchParams.get("id_token_hint")).toBe("id.token.jwt");
        expect(url.searchParams.get("post_logout_redirect_uri")).toBe(
            "http://localhost:5173",
        );
    });
});

describe("buildSiteMinderLogoutUrl", () => {
    it("wraps the Keycloak logout url in a SiteMinder logoff, url-encoded", () => {
        const result = buildSiteMinderLogoutUrl({
            authority: "https://dev.loginproxy.gov.bc.ca/auth/realms/standard",
            idTokenHint: "id.token.jwt",
            postLogoutRedirectUri: "http://localhost:5173",
        });
        const url = new URL(result);
        expect(url.origin).toBe("https://logon7.gov.bc.ca");
        expect(url.pathname).toBe("/clp-cgi/logoff.cgi");
        expect(url.searchParams.get("retnow")).toBe("1");

        // returl must be the fully-encoded Keycloak end-session URL
        const returl = url.searchParams.get("returl")!;
        expect(returl).toContain(
            "dev.loginproxy.gov.bc.ca/auth/realms/standard/protocol/openid-connect/logout",
        );
        expect(returl).toContain("id_token_hint=id.token.jwt");
    });

    it("allows overriding the SiteMinder logoff base", () => {
        const result = buildSiteMinderLogoutUrl({
            authority: "https://dev.loginproxy.gov.bc.ca/auth/realms/standard",
            postLogoutRedirectUri: "http://localhost:5173",
            siteMinderLogoffUrl: "https://logontest7.gov.bc.ca/clp-cgi/logoff.cgi",
        });
        expect(result.startsWith("https://logontest7.gov.bc.ca/clp-cgi/logoff.cgi")).toBe(
            true,
        );
    });
});
