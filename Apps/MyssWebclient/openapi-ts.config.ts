import { defineConfig } from "@hey-api/openapi-ts";

const myssApiUrl = process.env.VITE_MYSS_API_URL;

if (!myssApiUrl) {
    throw new Error(
        "VITE_MYSS_API_URL is not set. Copy .env.sample to .env and set the value.",
    );
}

export default defineConfig({
    input: `${myssApiUrl}/swagger/v1/swagger.json`,
    output: "src/api/generated",
    plugins: ["@tanstack/react-query"],
});
