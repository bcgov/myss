// Central place for app route paths so links and the router stay in sync.
// Add new routes here as pages are built out during the rebuild.
export const paths = {
    home: "/",
    dashboard: "/dashboard",
    eligibilityEstimator: "/eligibility-estimator",
    busPass: "/buspass",
    // In-app sign-in chooser (Option 1) and the OIDC redirect target.
    signIn: "/auth/login",
    authCallback: "/auth/callback",
    // Direct-entry route for IDIR administrative capabilities.
    admin: "/admin",
    adminFormManagement: "/admin/form-management",
    // Editor for one form, opened from the form-management list.
    adminFormEditor: "/admin/form-management/:formSpecId",
    // Minimal standalone login harness. Deliberately independent of the home
    // page so home can keep changing without disturbing a known-good way to
    // exercise the auth flow end to end.
    simpleLogin: "/simplelogin",
    // Placeholder page until the registration flow is rebuilt.
    register: "/registration",
} as const;

export type AppPath = (typeof paths)[keyof typeof paths];

/** Concrete editor URL for one form, kept beside the `adminFormEditor` pattern. */
export function adminFormEditorPath(formSpecId: string): string {
    return `/admin/form-management/${encodeURIComponent(formSpecId)}`;
}

/**
 * Editor URL for a form that does not exist yet. The editor starts from a
 * local template and nothing is stored until the first Save draft.
 */
export function adminNewFormPath(formSpecId: string, title: string): string {
    const params = new URLSearchParams({ new: "1" });
    if (title.trim()) {
        params.set("title", title.trim());
    }
    return `${adminFormEditorPath(formSpecId)}?${params.toString()}`;
}
