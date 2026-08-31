import { API_URL } from "@/constants";
import { authHeaders } from "@/auth/accessToken";

// GET /v1/auth/me — the server-computed caller identity. Roles here are the
// API's EFFECTIVE roles (RoleCalculator, ADR-0007); the SPA never derives
// roles from token claims, because the browser cannot see the server's derive
// switch today, nor MySS account state once the APPLICANT/CLIENT split lands.
//
// Not in the generated client yet, hence the raw fetch with authHeaders() —
// same as src/api/forms.ts. Swap for the SDK after regenerating the schema.

/** Mirrors `CurrentUser` in MyssApi/Models/CurrentUser.cs. */
export interface MePayload {
  isAuthenticated: boolean;
  subject: string;
  roles: string[];
  bceidGuid?: string | null;
  idirUsername?: string | null;
}

export async function fetchMe(): Promise<MePayload> {
  const res = await fetch(`${API_URL}/v1/auth/me`, {
    headers: { ...authHeaders() },
  });
  if (!res.ok) {
    throw new Error(`GET /v1/auth/me failed: ${res.status}`);
  }
  const body = (await res.json()) as { payload: MePayload };
  return body.payload;
}
