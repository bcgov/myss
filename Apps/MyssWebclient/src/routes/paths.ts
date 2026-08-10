// Central place for app route paths so links and the router stay in sync.
// Add new routes here as pages are built out during the rebuild.
export const paths = {
    home: "/",
    eligibilityEstimator: "/eligibility-estimator",
    // In-app sign-in chooser (Option 1) and the OIDC redirect target.
    signIn: "/auth/login",
    authCallback: "/auth/callback",
    // Minimal standalone login harness. Deliberately independent of the home
    // page so home can keep changing without disturbing a known-good way to
    // exercise the auth flow end to end.
    simpleLogin: "/simplelogin",
    // Placeholder wired to the real prod destination for now; swap to an
    // internal route as the registration flow is rebuilt.
    register: "/registration/step1",
} as const;

export type AppPath = (typeof paths)[keyof typeof paths];
