// Decoded-token inspector for the SimpleLogin harness, ported from the BC Gov
// keycloak playground's TokenDetails (bcgov/keycloak-example-apps). The original
// uses keycloak-js, which hands you pre-parsed tokens; this app uses
// react-oidc-context, so the mapping is:
//
//   keycloak.token            -> auth.user.access_token   (raw string)
//   keycloak.idToken          -> auth.user.id_token       (raw string)
//   keycloak.refreshToken     -> auth.user.refresh_token  (raw string)
//   keycloak.idTokenParsed    -> auth.user.profile        (already decoded)
//   keycloak.tokenParsed      -> decodeJwt(access_token)  (we decode it)
//   keycloak.refreshTokenParsed -> decodeJwt(refresh_token) (often opaque -> null)
//
// The design system has no Tabs/Table, so the tab strip is a ToggleButtonGroup
// and the parsed view is a plain semantic <table> styled via the CSS module.

import { useMemo, useState } from "react";
import {
  Button,
  ToggleButton,
  ToggleButtonGroup,
} from "@bcgov/design-system-react-components";
import { useAuth } from "react-oidc-context";

import { decodeJwt, type JwtClaims } from "@/auth/decodeJwt";
import styles from "./TokenDetails.module.css";

type TabKey =
  | "payload"
  | "accessToken"
  | "accessTokenParsed"
  | "idToken"
  | "idTokenParsed"
  | "refreshToken"
  | "refreshTokenParsed";

const TABS: { key: TabKey; title: string }[] = [
  { key: "payload", title: "Payload" },
  { key: "accessToken", title: "Access Token" },
  { key: "accessTokenParsed", title: "Access Token Parsed" },
  { key: "idToken", title: "ID Token" },
  { key: "idTokenParsed", title: "ID Token Parsed" },
  { key: "refreshToken", title: "Refresh Token" },
  { key: "refreshTokenParsed", title: "Refresh Token Parsed" },
];

// JWT time claims are Unix seconds; render them as something human-readable
// alongside the raw number so the table stays faithful to the token.
const TIME_CLAIMS = new Set(["exp", "iat", "auth_time", "nbf", "updated_at"]);

function formatValue(key: string, value: unknown): string {
  if (TIME_CLAIMS.has(key) && typeof value === "number") {
    return `${value} (${new Date(value * 1000).toLocaleString()})`;
  }
  if (value !== null && typeof value === "object") {
    return JSON.stringify(value);
  }
  return String(value);
}

async function copyToClipboard(text: string) {
  try {
    await navigator.clipboard.writeText(text);
  } catch {
    // Fallback for non-secure contexts where the Clipboard API is blocked.
    const ta = document.createElement("textarea");
    ta.value = text;
    ta.style.position = "fixed";
    ta.style.opacity = "0";
    document.body.appendChild(ta);
    ta.focus();
    ta.select();
    try {
      document.execCommand("copy");
    } finally {
      document.body.removeChild(ta);
    }
  }
}

function RawView({ value }: { value: string }) {
  if (!value) return <p className={styles.empty}>Not present on this token.</p>;
  return (
    <>
      <Button
        variant="secondary"
        size="small"
        onPress={() => void copyToClipboard(value)}
      >
        Copy
      </Button>
      <div className={styles.rawValue}>{value}</div>
    </>
  );
}

function ParsedView({ claims }: { claims: JwtClaims | null }) {
  if (!claims) {
    return (
      <p className={styles.empty}>
        Nothing to decode &mdash; this token is absent or opaque (not a JWT).
      </p>
    );
  }
  const entries = Object.entries(claims);
  if (entries.length === 0)
    return <p className={styles.empty}>No claims present.</p>;

  return (
    <table className={styles.table}>
      <tbody>
        {entries.map(([key, val]) => (
          <tr key={key}>
            <td className={styles.claimKey}>{key}</td>
            <td className={styles.claimVal}>{formatValue(key, val)}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

export default function TokenDetails() {
  const auth = useAuth();
  const [active, setActive] = useState<TabKey>("payload");

  const accessToken = auth.user?.access_token ?? "";
  const idToken = auth.user?.id_token ?? "";
  const refreshToken = auth.user?.refresh_token ?? "";

  // ID token is already decoded by oidc-client-ts (auth.user.profile). Access
  // and refresh tokens we decode ourselves; memoised so we don't re-parse on
  // every render / tab switch.
  const accessParsed = useMemo(() => decodeJwt(accessToken), [accessToken]);
  const refreshParsed = useMemo(() => decodeJwt(refreshToken), [refreshToken]);
  const idParsed = (auth.user?.profile as JwtClaims | undefined) ?? null;

  const payload = useMemo(
    () =>
      JSON.stringify(
        {
          access_token: accessToken || undefined,
          id_token: idToken || undefined,
          refresh_token: refreshToken || undefined,
        },
        null,
        2,
      ),
    [accessToken, idToken, refreshToken],
  );

  if (!auth.isAuthenticated) return null;

  return (
    <section className={styles.details} aria-label="Token details">
      <ToggleButtonGroup
        label="Token view"
        size="small"
        selectionMode="single"
        disallowEmptySelection
        selectedKeys={[active]}
        onSelectionChange={(keys) => {
          const next = [...keys][0] as TabKey | undefined;
          if (next) setActive(next);
        }}
      >
        {TABS.map((t) => (
          <ToggleButton key={t.key} id={t.key}>
            {t.title}
          </ToggleButton>
        ))}
      </ToggleButtonGroup>

      <div className={styles.panel}>
        {active === "payload" && <RawView value={payload} />}
        {active === "accessToken" && <RawView value={accessToken} />}
        {active === "accessTokenParsed" && <ParsedView claims={accessParsed} />}
        {active === "idToken" && <RawView value={idToken} />}
        {active === "idTokenParsed" && <ParsedView claims={idParsed} />}
        {active === "refreshToken" && <RawView value={refreshToken} />}
        {active === "refreshTokenParsed" && (
          <ParsedView claims={refreshParsed} />
        )}
      </div>
    </section>
  );
}
