import path from "path";
import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// https://vite.dev/config/
export default defineConfig({
    plugins: [react()],
    resolve: {
        alias: {
            "@": path.resolve(__dirname, "src"),
        },
    },
    // 5173 is the citizen webclient's; both can run side by side locally.
    server: { port: 5174, strictPort: true },
    preview: { port: 5174, strictPort: true },
});
