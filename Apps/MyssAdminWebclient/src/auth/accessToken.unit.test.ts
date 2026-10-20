import { describe, it, expect, afterEach } from "vitest";

import { authHeaders, getAccessToken, setAccessToken } from "./accessToken";

afterEach(() => {
    setAccessToken(undefined);
});

describe("accessToken", () => {
    it("returns no header when signed out", () => {
        expect(getAccessToken()).toBeUndefined();
        expect(authHeaders()).toEqual({});
    });

    it("builds a Bearer header from the current token", () => {
        setAccessToken("access.token.jwt");
        expect(authHeaders()).toEqual({
            Authorization: "Bearer access.token.jwt",
        });
    });

    it("reflects a renewed token on the next read", () => {
        setAccessToken("first.token");
        expect(authHeaders()).toEqual({ Authorization: "Bearer first.token" });

        setAccessToken("renewed.token");
        expect(authHeaders()).toEqual({ Authorization: "Bearer renewed.token" });
    });

    it("drops the header once the session ends", () => {
        setAccessToken("access.token.jwt");
        setAccessToken(undefined);
        expect(authHeaders()).toEqual({});
    });
});
