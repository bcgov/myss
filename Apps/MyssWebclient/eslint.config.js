import js from "@eslint/js";
import globals from "globals";
import reactHooks from "eslint-plugin-react-hooks";
import reactRefresh from "eslint-plugin-react-refresh";
import tseslint from "typescript-eslint";
import tanstackQuery from "@tanstack/eslint-plugin-query";
import { defineConfig, globalIgnores } from "eslint/config";

export default defineConfig([
    globalIgnores(["dist", "src/api/generated"]),
    {
        files: ["**/*.{ts,tsx}"],
        extends: [
            js.configs.recommended,
            tseslint.configs.recommended,
            reactHooks.configs.flat.recommended,
            reactRefresh.configs.vite,
            tanstackQuery.configs["flat/recommended"],
        ],
        languageOptions: {
            ecmaVersion: 2020,
            globals: globals.browser,
        },
        // Declared after `extends` so it overrides the rule as configured by
        // tseslint.configs.recommended rather than being overridden by it.
        rules: {
            // A leading underscore is this repo's marker for a parameter that
            // exists to document a callback's signature but is deliberately not
            // used — `bearerInterceptor(request, _options)` matching hey-api's
            // interceptor shape, for instance. The recommended preset ships no
            // ignore pattern, so the convention has to be stated to be honoured.
            "@typescript-eslint/no-unused-vars": [
                "error",
                { argsIgnorePattern: "^_" },
            ],
        },
    },
]);
