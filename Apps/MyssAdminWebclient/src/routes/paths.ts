// Central place for the admin app's route paths so links and the router stay
// in sync. The app has its own origin, so these carry no /admin prefix.
export const paths = {
    // The admin landing page: the list of admin capabilities.
    home: "/",
    formManagement: "/form-management",
    // Editor for one form, opened from the form-management list.
    formEditor: "/form-management/:formSpecId",
    // The OIDC redirect target.
    authCallback: "/auth/callback",
    // Where sign-out and idle logout land. Deliberately unguarded: a guarded
    // page would start IDIR sign-in at once and, with the IDIR session still
    // alive upstream, sign the user straight back in.
    signedOut: "/signed-out",
} as const;

export type AppPath = (typeof paths)[keyof typeof paths];

/** Concrete editor URL for one form, kept beside the `formEditor` pattern. */
export function formEditorPath(formSpecId: string): string {
    return `/form-management/${encodeURIComponent(formSpecId)}`;
}

/**
 * Editor URL for a form that does not exist yet. The editor starts from a
 * local template and nothing is stored until the first Save draft.
 */
export function newFormPath(formSpecId: string, title: string): string {
    const params = new URLSearchParams({ new: "1" });
    if (title.trim()) {
        params.set("title", title.trim());
    }
    return `${formEditorPath(formSpecId)}?${params.toString()}`;
}
