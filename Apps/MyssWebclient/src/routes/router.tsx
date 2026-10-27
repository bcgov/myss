import { createBrowserRouter } from "react-router";
import { AuthCallbackPage } from "@myss/web-common/components";

import App from "@/App";
import HomePage from "@/pages/HomePage";
import DashboardPage from "@/pages/DashboardPage";
import DashboardLayout from "@/pages/DashboardLayout";
import AccountInfoPage from "@/pages/AccountInfoPage";
import UnderDevelopmentPage from "@/pages/UnderDevelopmentPage";
import RegistrationPage from "@/pages/RegistrationPage";
import EligibilityEstimatorPage from "@/pages/EligibilityEstimatorPage";
import SignInPage from "@/pages/SignInPage";
import PinResetPage from "@/pages/PinResetPage";
import VerifyEmailPage from "@/pages/VerifyEmailPage";
import SimpleLoginPage from "@/pages/SimpleLoginPage";
import TechDemos from "@/pages/TechDemos";
import FormsTechDemo from "@/pages/FormsTechDemo";
import BusPassPage from "@/pages/BusPassPage";
import AttachmentsTechDemo from "@/pages/AttachmentsTechDemo";
import ApplicationPage from "@/pages/ApplicationPage";
import AfaServiceRequestPage from "@/pages/AfaServiceRequestPage";
import ServiceRequestsPage from "@/pages/ServiceRequestsPage";
import RequireAuth from "@/auth/RequireAuth";
import { paths } from "@/routes/paths";
import FeatureRoute from "@/routes/FeatureRoute";

// App is the shared layout (header/footer + app-wide auth concerns). Child
// routes render into its <Outlet />.
//
// Route composition (see doc/myss-vs-rebuild-merge-analysis.md §6):
//   PUBLIC    - landing, eligibility estimator, the sign-in / callback
//               pages, and the emailed PIN reset link. Any anonymous user
//               can reach these.
//   PROTECTED - the Forms / Strapi tech-demo pages, wrapped in <RequireAuth>
//               so they only render once the user has authenticated.
export const router = createBrowserRouter([
  {
    path: paths.home,
    element: <App />,
    children: [
      // ---- Public ----
      { index: true, element: <HomePage /> },
      { path: paths.register, element: <RegistrationPage /> },
      {
        path: paths.eligibilityEstimator,
        element: (
          <FeatureRoute feature="EligibilityEstimator">
            <EligibilityEstimatorPage />
          </FeatureRoute>
        ),
      },
      {
        path: paths.busPass,
        element: (
          <FeatureRoute feature="BusPass">
            <BusPassPage />
          </FeatureRoute>
        ),
      },
      { path: paths.signIn, element: <SignInPage /> },
      {
        path: paths.authCallback,
        element: <AuthCallbackPage fallbackPath={paths.dashboard} />,
      },
      {
        path: paths.pinReset,
        element: (
          <FeatureRoute feature="PinReset">
            <PinResetPage />
          </FeatureRoute>
        ),
      },
      {
        path: paths.verifyEmail,
        element: (
          <FeatureRoute feature="EmailVerification">
            <VerifyEmailPage />
          </FeatureRoute>
        ),
      },
      { path: paths.simpleLogin, element: <SimpleLoginPage /> },

      // ---- Dashboard (MYSS-194): the signed-in citizen's sections, framed
      // by the left-hand menu. A section still under development renders the
      // placeholder; connecting its feature means swapping that element only.
      {
        element: (
          <RequireAuth>
            <DashboardLayout />
          </RequireAuth>
        ),
        children: [
          { path: paths.dashboard, element: <DashboardPage /> },
          {
            path: paths.notifications,
            element: (
              <FeatureRoute feature="Notifications">
                <UnderDevelopmentPage title="Notifications" />
              </FeatureRoute>
            ),
          },
          {
            path: paths.messages,
            element: (
              <FeatureRoute feature="Messages">
                <UnderDevelopmentPage title="Messages" />
              </FeatureRoute>
            ),
          },
          {
            path: paths.serviceRequests,
            element: (
              <FeatureRoute feature="ServiceRequests">
                <ServiceRequestsPage />
              </FeatureRoute>
            ),
          },
          {
            path: paths.afaServiceRequest,
            element: (
              <FeatureRoute feature="ServiceRequests">
                <AfaServiceRequestPage />
              </FeatureRoute>
            ),
          },
          {
            path: paths.accountInfo,
            element: (
              <FeatureRoute feature="AccountInfo">
                <AccountInfoPage />
              </FeatureRoute>
            ),
          },
        ],
      },

      // ---- Protected (Forms / Strapi): only after login/auth ----
      {
        path: paths.application,
        element: (
          <RequireAuth>
            <FeatureRoute feature="Intake">
              <ApplicationPage />
            </FeatureRoute>
          </RequireAuth>
        ),
      },
      {
        path: "techdemos",
        element: (
          <RequireAuth>
            <FeatureRoute feature="TechDemos">
              <TechDemos />
            </FeatureRoute>
          </RequireAuth>
        ),
      },
      {
        path: "techdemos/forms",
        element: (
          <RequireAuth>
            <FeatureRoute feature="TechDemos">
              <FormsTechDemo />
            </FeatureRoute>
          </RequireAuth>
        ),
      },
      {
        path: "techdemos/attachments",
        element: (
          <RequireAuth>
            <FeatureRoute feature="TechDemos">
              <AttachmentsTechDemo />
            </FeatureRoute>
          </RequireAuth>
        ),
      },
    ],
  },
]);
