import { afterEach, describe, expect, it, vi } from "vitest";

import { paths } from "@/routes/paths";
import { siteMinderLogout } from "./siteMinderLogout";

// The logout chain itself is tested in @myss/web-common; this pins where this
// app returns to.
afterEach(() => vi.unstubAllGlobals());

describe("siteMinderLogout", () => {
    it("returns to paths.signedOut", async () => {
        const assign = vi.fn();
        vi.stubGlobal("window", {
            location: { origin: "http://localhost:5173", assign },
        });

        await siteMinderLogout({
            user: { id_token: "id.token.jwt" },
            settings: {
                authority:
                    "https://dev.loginproxy.gov.bc.ca/auth/realms/standard",
            },
            removeUser: vi.fn().mockResolvedValue(undefined),
        } as never);

        const siteMinderUrl = new URL(assign.mock.calls[0][0]);
        const keycloakUrl = new URL(siteMinderUrl.searchParams.get("returl")!);
        expect(keycloakUrl.searchParams.get("post_logout_redirect_uri")).toBe(
            new URL(paths.signedOut, "http://localhost:5173").toString(),
        );
    });
});
