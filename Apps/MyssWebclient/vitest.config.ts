import path from "path";
import { defineConfig } from "vitest/config";
import { playwright } from "@vitest/browser-playwright";
import react from "@vitejs/plugin-react";

const alias = {
    "@": path.resolve(__dirname, "src"),
};

export default defineConfig({
    plugins: [react()],
    resolve: { alias },
    test: {
        projects: [
            {
                resolve: { alias },
                test: {
                    name: "unit",
                    include: ["**/*.unit.{test,spec}.ts"],
                    environment: "node",
                },
            },
            {
                plugins: [react()],
                resolve: { alias },
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
