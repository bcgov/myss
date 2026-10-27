// Central place for app route paths so links and the router stay in sync.
// Add new routes here as pages are built out during the rebuild.
export const paths = {
  home: "/",
  dashboard: "/dashboard",
  // Dashboard sections (MYSS-194). Placeholders until each feature is built;
  // see routes/dashboardSections.ts for the navigation menu.
  notifications: "/notifications",
  messages: "/messages",
  serviceRequests: "/service-requests",
  afaServiceRequest: "/service-requests/:id",
  accountInfo: "/account",
  eligibilityEstimator: "/eligibility-estimator",
  busPass: "/buspass",
  // In-app sign-in chooser (Option 1) and the OIDC redirect target.
  signIn: "/auth/login",
  authCallback: "/auth/callback",
  // Where the emailed PIN reset link lands (MYSS-36). Public: the link's
  // token is the credential. MyssApi builds the link from this path
  // (PinResetConfig.LinkPath), so change both together.
  pinReset: "/pin-reset",
  // Minimal standalone login harness. Deliberately independent of the home
  // page so home can keep changing without disturbing a known-good way to
  // exercise the auth flow end to end.
  simpleLogin: "/simplelogin",
  // Placeholder page until the registration flow is rebuilt.
  register: "/registration",
  // One Income Assistance application: editable while a draft, read-only
  // once submitted. Started from the dashboard's "My applications".
  application: "/applications/:id",
} as const;

export type AppPath = (typeof paths)[keyof typeof paths];

/** Marks Account Info reached from a successful PIN reset (MYSS-324). */
export const PIN_RESET_DONE_PARAM = "pinReset";

/**
 * Account Info after a successful PIN reset: it then says the PIN was reset.
 * A marker in the address rather than navigation state, so it survives the
 * sign-in round trip a new tab needs.
 */
export const accountInfoAfterPinReset = `${paths.accountInfo}?${PIN_RESET_DONE_PARAM}=done`;

/** Concrete URL for one application, kept beside the `application` pattern. */
export function applicationPath(id: string): string {
  return `/applications/${encodeURIComponent(id)}`;
}

/** Citizen URL for a saved AFA service request. */
export function afaServiceRequestPath(id: string): string {
  return `/service-requests/${encodeURIComponent(id)}`;
}
