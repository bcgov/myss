import { describe, expect, it } from "vitest";

import { formatBcDateTime } from "@/lib/bcTime";

// Summer timestamps: BC is UTC−7 then under every time zone database.
describe("formatBcDateTime", () => {
  it("shows the day, the short month, the year and the 24-hour time in BC", () => {
    expect(formatBcDateTime("2026-07-03T17:42:00Z")).toBe("3 Jul 2026 at 10:42");
  });

  it("writes September as Sep, and a time after midnight with two-digit hours", () => {
    expect(formatBcDateTime("2026-09-15T07:05:00Z")).toBe("15 Sep 2026 at 00:05");
  });

  it("takes the BC date, not the UTC date", () => {
    expect(formatBcDateTime("2026-08-01T03:30:00Z")).toBe("31 Jul 2026 at 20:30");
  });
});
