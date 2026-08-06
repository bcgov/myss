import { describe, it, expect } from "vitest";

import { bearerInterceptor } from "./useApiAuth";

describe("bearerInterceptor", () => {
    it("adds an Authorization: Bearer header when a token is present", async () => {
        const interceptor = bearerInterceptor(() => "access.token.jwt");
        const req = new Request("http://api.local/v1/thing");
        const out = await interceptor(req, {} as never);
        expect(out.headers.get("Authorization")).toBe("Bearer access.token.jwt");
    });

    it("leaves the request untouched when no token is available", async () => {
        const interceptor = bearerInterceptor(() => undefined);
        const req = new Request("http://api.local/v1/thing");
        const out = await interceptor(req, {} as never);
        expect(out.headers.get("Authorization")).toBeNull();
    });

    it("reads the token lazily on each request", async () => {
        let token: string | undefined = undefined;
        const interceptor = bearerInterceptor(() => token);

        const first = await interceptor(
            new Request("http://api.local/a"),
            {} as never,
        );
        expect(first.headers.get("Authorization")).toBeNull();

        token = "later.token";
        const second = await interceptor(
            new Request("http://api.local/b"),
            {} as never,
        );
        expect(second.headers.get("Authorization")).toBe("Bearer later.token");
    });
});
