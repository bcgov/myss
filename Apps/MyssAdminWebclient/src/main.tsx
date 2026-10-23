import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { RouterProvider } from "react-router/dom";
import { registerBcgovComponents } from "@myss/formio";

import { router } from "@/routes/router";
import { AuthProvider } from "@/auth/AuthProvider";
import { CANADA_POST_API_KEY } from "@/constants";
import "@bcgov/design-tokens/css/variables.css";
import "@/index.css";
import "@bcgov/bc-sans/css/BC_Sans.css";

// Register the BC Gov Form.io components before any <Form> renders, so the
// editor's preview draws a spec exactly as the citizen webclient does.
registerBcgovComponents({ canadaPostApiKey: CANADA_POST_API_KEY });

const queryClient = new QueryClient();

createRoot(document.getElementById("root")!).render(
    <StrictMode>
        <AuthProvider>
            <QueryClientProvider client={queryClient}>
                <RouterProvider router={router} />
            </QueryClientProvider>
        </AuthProvider>
    </StrictMode>,
);
