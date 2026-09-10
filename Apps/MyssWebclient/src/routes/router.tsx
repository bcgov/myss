import { createBrowserRouter } from "react-router";

import App from "@/App";
import HomePage from "@/pages/HomePage";
import DashboardPage from "@/pages/DashboardPage";
import EligibilityEstimatorPage from "@/pages/EligibilityEstimatorPage";
import SignInPage from "@/pages/SignInPage";
import AuthCallbackPage from "@/pages/AuthCallbackPage";
import SimpleLoginPage from "@/pages/SimpleLoginPage";
import TechDemos from "@/pages/TechDemos";
import FormsTechDemo from "@/pages/FormsTechDemo";
import BusPassPage from "@/pages/BusPassPage";
import SubmissionView from "@/pages/SubmissionView";
import AttachmentsTechDemo from "@/pages/AttachmentsTechDemo";
import AdminPage from "@/pages/AdminPage";
import FormManagementPage from "@/pages/FormManagementPage";
import RequireAuth from "@/auth/RequireAuth";
import RequireIdir from "@/auth/RequireIdir";
import { paths } from "@/routes/paths";

// App is the shared layout (header/footer + app-wide auth concerns). Child
// routes render into its <Outlet />.
//
// Route composition (see doc/myss-vs-rebuild-merge-analysis.md §6):
//   PUBLIC    - landing, eligibility estimator, and the sign-in / callback
//               pages. Any anonymous user can reach these.
//   PROTECTED - the Forms / Strapi tech-demo pages, wrapped in <RequireAuth>
//               so they only render once the user has authenticated.
export const router = createBrowserRouter([
    {
        path: paths.home,
        element: <App />,
        children: [
            // ---- Public ----
            { index: true, element: <HomePage /> },
            {
                path: paths.eligibilityEstimator,
                element: <EligibilityEstimatorPage />,
            },
            { path: paths.signIn, element: <SignInPage /> },
            { path: paths.authCallback, element: <AuthCallbackPage /> },
            { path: paths.simpleLogin, element: <SimpleLoginPage /> },

            // ---- Administration: direct URL entry for authenticated IDIR users ----
            {
                path: paths.admin,
                element: (
                    <RequireIdir>
                        <AdminPage />
                    </RequireIdir>
                ),
            },
            {
                path: paths.adminFormManagement,
                element: (
                    <RequireIdir>
                        <FormManagementPage />
                    </RequireIdir>
                ),
            },

            // ---- Protected (Forms / Strapi): only after login/auth ----
            {
                path: paths.dashboard,
                element: (
                    <RequireAuth>
                        <DashboardPage />
                    </RequireAuth>
                ),
            },
            {
                path: "techdemos",
                element: (
                    <RequireAuth>
                        <TechDemos />
                    </RequireAuth>
                ),
            },
            {
                path: "techdemos/forms",
                element: (
                    <RequireAuth>
                        <FormsTechDemo />
                    </RequireAuth>
                ),
            },
            {
                path: "techdemos/bc-bus-pass",
                element: (
                    <RequireAuth>
                        <BusPassPage />
                    </RequireAuth>
                ),
            },
            {
                path: "techdemos/forms/submissions/:id",
                element: (
                    <RequireAuth>
                        <SubmissionView />
                    </RequireAuth>
                ),
            },
            {
                path: "techdemos/attachments",
                element: (
                    <RequireAuth>
                        <AttachmentsTechDemo />
                    </RequireAuth>
                ),
            },
        ],
    },
]);
