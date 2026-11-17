// Sign-out through SiteMinder, returning to the landing page with a clean
// session. The logic is @myss/web-common's; this binds the return path.

import type { AuthContextProps } from "react-oidc-context";
import { siteMinderLogout as logoutThroughSiteMinder } from "@myss/web-common/auth";

import { paths } from "@/routes/paths";

export { isLogoutInProgress } from "@myss/web-common/auth";

export function siteMinderLogout(auth: AuthContextProps): Promise<void> {
    return logoutThroughSiteMinder(auth, { returnPath: paths.home });
}
