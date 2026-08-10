import { createBrowserRouter } from "react-router";

import App from "@/App";
import HomePage from "@/pages/HomePage";
import EligibilityEstimatorPage from "@/pages/EligibilityEstimatorPage";
import SignInPage from "@/pages/SignInPage";
import AuthCallbackPage from "@/pages/AuthCallbackPage";
import SimpleLoginPage from "@/pages/SimpleLoginPage";
import TechDemos from "@/pages/TechDemos";
import FormsTechDemo from "@/pages/FormsTechDemo";
import SubmissionView from "@/pages/SubmissionView";
import RequireAuth from "@/auth/RequireAuth";
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

            // ---- Protected (Forms / Strapi): only after login/auth ----
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
                path: "techdemos/forms/submissions/:id",
                element: (
                    <RequireAuth>
                        <SubmissionView />
                    </RequireAuth>
                ),
            },
        ],
    },
]);
