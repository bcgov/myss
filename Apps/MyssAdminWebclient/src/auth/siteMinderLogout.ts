// Sign-out through SiteMinder, landing on the unguarded signed-out page (see
// paths.signedOut). The logic is @myss/web-common's; this binds the return path.

import type { AuthContextProps } from "react-oidc-context";
import { siteMinderLogout as logoutThroughSiteMinder } from "@myss/web-common/auth";

import { paths } from "@/routes/paths";

export { isLogoutInProgress } from "@myss/web-common/auth";

export function siteMinderLogout(auth: AuthContextProps): Promise<void> {
    return logoutThroughSiteMinder(auth, { returnPath: paths.signedOut });
}
