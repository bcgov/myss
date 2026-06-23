import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

import { client } from "@/api/generated/client.gen";
import App from "@/App.tsx";
import { API_URL } from "@/constants.ts";
import "@bcgov/design-tokens/css/variables.css";
import "@/index.css";
import "@bcgov/bc-sans/css/BC_Sans.css";

client.setConfig({ baseUrl: API_URL });

const queryClient = new QueryClient();

createRoot(document.getElementById("root")!).render(
    <StrictMode>
        <QueryClientProvider client={queryClient}>
            <App />
        </QueryClientProvider>
    </StrictMode>,
);
