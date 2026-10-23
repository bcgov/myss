import { createBrowserRouter } from "react-router";
import { AuthCallbackPage } from "@myss/web-common/components";

import App from "@/App";
import AdminPage from "@/pages/AdminPage";
import FormManagementPage from "@/pages/FormManagementPage";
import FormEditorPage from "@/pages/FormEditorPage";
import FormFromJsonPage from "@/pages/FormFromJsonPage";
import SignedOutPage from "@/pages/SignedOutPage";
import ApplicationsPage from "@/pages/ApplicationsPage";
import ApplicationReviewPage from "@/pages/ApplicationReviewPage";
import RequireWorker from "@/auth/RequireWorker";
import RequireIdir from "@/auth/RequireIdir";
import { paths } from "@/routes/paths";

// App is the shared layout (header + app-wide auth concerns). Every page but
// the OIDC callback and the signed-out page needs an IDIR sign-in, and the
// application review pages also need the worker role; the API enforces the
// same rules.
export const router = createBrowserRouter([
    {
        path: paths.home,
        element: <App />,
        children: [
            {
                path: paths.authCallback,
                element: <AuthCallbackPage fallbackPath={paths.home} />,
            },
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
            {
                path: paths.formFromJson,
                element: (
                    <RequireIdir>
                        <FormFromJsonPage />
                    </RequireIdir>
                ),
            },
            {
                path: paths.applications,
                element: (
                    <RequireIdir>
                        <RequireWorker>
                            <ApplicationsPage />
                        </RequireWorker>
                    </RequireIdir>
                ),
            },
            {
                path: paths.application,
                element: (
                    <RequireIdir>
                        <RequireWorker>
                            <ApplicationReviewPage />
                        </RequireWorker>
                    </RequireIdir>
                ),
            },
        ],
    },
]);
