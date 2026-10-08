import { createBrowserRouter } from "react-router";

import App from "@/App";
import HomePage from "@/pages/HomePage";
import DashboardPage from "@/pages/DashboardPage";
import DashboardLayout from "@/pages/DashboardLayout";
import AccountInfoPage from "@/pages/AccountInfoPage";
import UnderDevelopmentPage from "@/pages/UnderDevelopmentPage";
import RegistrationPage from "@/pages/RegistrationPage";
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
import FormEditorPage from "@/pages/FormEditorPage";
import ApplicationPage from "@/pages/ApplicationPage";
import WorkerApplicationsPage from "@/pages/WorkerApplicationsPage";
import WorkerApplicationPage from "@/pages/WorkerApplicationPage";
import RequireAuth from "@/auth/RequireAuth";
import RequireIdir from "@/auth/RequireIdir";
import RequireWorker from "@/auth/RequireWorker";
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
      { path: paths.register, element: <RegistrationPage /> },
      {
        path: paths.eligibilityEstimator,
        element: <EligibilityEstimatorPage />,
      },
      {
        path: paths.busPass,
        element: <BusPassPage />,
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
      {
        path: paths.adminFormEditor,
        element: (
          <RequireIdir>
            <FormEditorPage />
          </RequireIdir>
        ),
      },

      // ---- Worker operations: IDIR sign-in with the WORKER role ----
      {
        path: paths.workerApplications,
        element: (
          <RequireWorker>
            <WorkerApplicationsPage />
          </RequireWorker>
        ),
      },
      {
        path: paths.workerApplication,
        element: (
          <RequireWorker>
            <WorkerApplicationPage />
          </RequireWorker>
        ),
      },

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
            element: <UnderDevelopmentPage title="Notifications" />,
          },
          {
            path: paths.messages,
            element: <UnderDevelopmentPage title="Messages" />,
          },
          {
            path: paths.serviceRequests,
            element: <UnderDevelopmentPage title="Service Requests" />,
          },
          { path: paths.accountInfo, element: <AccountInfoPage /> },
        ],
      },

      // ---- Protected (Forms / Strapi): only after login/auth ----
      {
        path: paths.application,
        element: (
          <RequireAuth>
            <ApplicationPage />
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
