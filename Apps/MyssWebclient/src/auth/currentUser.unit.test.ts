import { describe, it, expect } from "vitest";

import { normalizeUser } from "./currentUser";

describe("normalizeUser", () => {
    it("maps core identity claims", () => {
        const user = normalizeUser({
            sub: "abc-123",
            name: "Alice Applicant",
            email: "alice@example.com",
        });
        expect(user.sub).toBe("abc-123");
        expect(user.name).toBe("Alice Applicant");
        expect(user.email).toBe("alice@example.com");
        expect(user.roles).toEqual([]);
    });

    it("prefers display_name and preferred_username when name is absent", () => {
        expect(normalizeUser({ sub: "x", display_name: "Bob B" }).name).toBe(
            "Bob B",
        );
        expect(
            normalizeUser({ sub: "x", preferred_username: "bob@bceid" }).name,
        ).toBe("bob@bceid");
    });

    it("collects roles from client_roles, realm_access and resource_access, de-duplicated", () => {
        const user = normalizeUser({
            sub: "x",
            client_roles: ["CLIENT"],
            realm_access: { roles: ["WORKER", "CLIENT"] },
            resource_access: {
                "sdpr-my-ss-6498": { roles: ["ADMIN", "WORKER"] },
            },
        });
        expect(user.roles.sort()).toEqual(["ADMIN", "CLIENT", "WORKER"]);
    });

    it("surfaces bceid guid and idir username under either claim spelling", () => {
        expect(
            normalizeUser({ sub: "x", bceid_user_guid: "guid-1" }).bceidGuid,
        ).toBe("guid-1");
        expect(normalizeUser({ sub: "x", bceid_guid: "guid-2" }).bceidGuid).toBe(
            "guid-2",
        );
        expect(
            normalizeUser({ sub: "x", idir_username: "AJONES" }).idirUsername,
        ).toBe("AJONES");
    });

    it("never returns undefined roles", () => {
        expect(normalizeUser({ sub: "x" }).roles).toEqual([]);
    });
});
