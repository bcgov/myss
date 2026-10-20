import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { RouterProvider } from "react-router/dom";
import { registerBcgovComponents } from "@myss/formio";

import { client } from "@/api/generated/client.gen";
import { router } from "@/routes/router";
import { AuthProvider } from "@/auth/AuthProvider";
import { API_URL, CANADA_POST_API_KEY } from "@/constants.ts";
import "@bcgov/design-tokens/css/variables.css";
import "@/index.css";
import "@bcgov/bc-sans/css/BC_Sans.css";

client.setConfig({ baseUrl: API_URL });

// Register the custom BC Gov Form.io components (bcgovAccordion, bcgovRadio)
// before any <Form> renders — the estimator v4 spec references both types.
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
