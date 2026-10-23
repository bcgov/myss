import { defineConfig } from "vitest/config";
import { playwright } from "@vitest/browser-playwright";
import react from "@vitejs/plugin-react";

// The package is consumed as TypeScript source, so it has no build of its
// own: these suites are how its code is checked on its own, apart
// from either webclient.
export default defineConfig({
    plugins: [react()],
    test: {
        coverage: {
            provider: "v8",
            reporter: ["text", ["lcov", { projectRoot: "../../" }]],
        },
        projects: [
            {
                test: {
                    name: "unit",
                    include: ["**/*.unit.{test,spec}.ts"],
                    environment: "node",
                },
            },
            {
                plugins: [react()],
                test: {
                    name: "browser",
                    include: ["**/*.browser.{test,spec}.tsx"],
                    browser: {
                        enabled: true,
                        provider: playwright(),
                        // https://vitest.dev/config/browser/playwright
                        instances: [{ browser: "chromium" }],
                    },
                },
            },
        ],
    },
});
