import { createBrowserRouter } from "react-router";

import App from "@/App";
import AdminPage from "@/pages/AdminPage";
import FormManagementPage from "@/pages/FormManagementPage";
import FormEditorPage from "@/pages/FormEditorPage";
import AuthCallbackPage from "@/pages/AuthCallbackPage";
import SignedOutPage from "@/pages/SignedOutPage";
import RequireIdir from "@/auth/RequireIdir";
import { paths } from "@/routes/paths";

// App is the shared layout (header + app-wide auth concerns). Every page but
// the OIDC callback and the signed-out page needs an IDIR sign-in; the API
// enforces the same rule.
export const router = createBrowserRouter([
    {
        path: paths.home,
        element: <App />,
        children: [
            { path: paths.authCallback, element: <AuthCallbackPage /> },
            { path: paths.signedOut, element: <SignedOutPage /> },
            {
                index: true,
                element: (
                    <RequireIdir>
                        <AdminPage />
                    </RequireIdir>
                ),
            },
            {
                path: paths.formManagement,
                element: (
                    <RequireIdir>
                        <FormManagementPage />
                    </RequireIdir>
                ),
            },
            {
                path: paths.formEditor,
                element: (
                    <RequireIdir>
                        <FormEditorPage />
                    </RequireIdir>
                ),
            },
        ],
    },
]);
