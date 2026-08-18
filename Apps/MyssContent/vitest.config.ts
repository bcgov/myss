import { defineConfig } from "vitest/config";

/**
 * Tests here run over pure, exported modules under `src/lib/` — no Strapi
 * boot, no database, no HTTP. Anything that needs Strapi belongs in a thin
 * wrapper (a lifecycle hook, the bootstrap function) that delegates to a
 * module tested here.
 */
export default defineConfig({
  test: {
    include: ["src/**/*.test.ts"],
    environment: "node",
  },
});
