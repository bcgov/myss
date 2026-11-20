import { defineConfig, devices } from "@playwright/test";
import { cucumberReporter, defineBddConfig } from "playwright-bdd";

// End-to-end scenarios written in Gherkin (e2e/features) and run by Playwright
// Test through playwright-bdd: `bddgen` turns each feature into a spec under
// .features-gen, then `playwright test` runs them like any other spec. The
// browser drives the real app served by Vite; only the API is stubbed, from
// the Strapi seed (see e2e/support). `npm run test:e2e`.

const testDir = defineBddConfig({
  features: "e2e/features/**/*.feature",
  steps: ["e2e/fixtures.ts", "e2e/steps/**/*.ts"],
  outputDir: ".features-gen",
});

// Its own port so a developer's `npm run dev` on 5173 is left alone.
const PORT = 5174;

// E2E_BASE_URL points the scenarios at a deployed webclient instead (the
// nightly integration run, .github/workflows/e2e-integration.yml): no local
// server is started, and with MYSS_E2E_LIVE_API=1 nothing is stubbed, so the
// deployed config.js, API and content are what the scenarios exercise.
const liveBaseUrl = process.env.E2E_BASE_URL;

export default defineConfig({
  testDir,
  fullyParallel: true,
  forbidOnly: Boolean(process.env.CI),
  retries: process.env.CI ? 2 : 0,
  reporter: [
    ["list"],
    ["html", { open: "never", outputFolder: "playwright-report" }],
    // Cucumber-style report grouped by feature and scenario, for people who
    // read the features rather than the specs.
    cucumberReporter("html", { outputFile: "e2e-report/index.html" }),
  ],
  use: {
    baseURL: liveBaseUrl ?? `http://localhost:${PORT}`,
    trace: "on-first-retry",
  },
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
  webServer: liveBaseUrl
    ? undefined
    : {
        command: `npx vite --port ${PORT} --strictPort`,
        url: `http://localhost:${PORT}`,
        reuseExistingServer: !process.env.CI,
        timeout: 60_000,
      },
});
