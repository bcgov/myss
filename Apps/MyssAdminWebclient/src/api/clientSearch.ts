import { throwApplicationRequestError } from "@myss/web-common/api";
import { authHeaders } from "@myss/web-common/auth";

import { API_URL } from "@/constants";

// Support View client search (GET /v1/support/clients, MYSS-275): a worker
// finds MySS clients to validate their personal information. Responses come
// wrapped in the API's payload envelope. Raw fetches with authHeaders(), as
// api/review.ts.

/** What a worker searches by. Blank fields are left out of the request. */
export interface ClientSearchCriteria {
  firstName?: string;
  lastName?: string;
  /** The full nine-digit SIN; spaces and dashes are ignored. */
  sin?: string;
  bceidUsername?: string;
}

/** How a client signs in. Mirrors ClientSignInTypes. */
export type ClientSignInType = "BCEID" | "BC_SERVICES_CARD" | "WORKER_OVERRIDE";

/** One client a search found. Mirrors ClientSummaryModel. */
export interface ClientSummaryPayload {
  id: string;
  firstName: string;
  lastName: string;
  bceidUsername?: string | null;
  /** The SIN's last three digits, the only part the API returns. */
  sinLastThree?: string | null;
  /** yyyy-mm-dd. */
  dateOfBirth: string;
  signInType?: ClientSignInType | null;
}

/** A search's answer. Mirrors ClientSearchResultModel. */
export interface ClientSearchResultPayload {
  clients: ClientSummaryPayload[];
  /** More clients matched than the API returns. */
  hasMore: boolean;
}

/** The keywords a refused search (400) carries. */
export const CLIENT_SEARCH_KEYWORDS = {
  criteriaRequired: "SUPPORT.CLIENT_SEARCH.CRITERIA_REQUIRED",
  sinInvalid: "SUPPORT.CLIENT_SEARCH.SIN_INVALID",
} as const;

const BASE_URL = `${API_URL}/v1/support/clients`;

/** The criteria as query parameters, blank fields left out. */
export function searchQuery(criteria: ClientSearchCriteria): string {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(criteria)) {
    const trimmed = typeof value === "string" ? value.trim() : "";
    if (trimmed) params.set(key, trimmed);
  }
  return params.toString();
}

/** Searches the clients. */
export async function searchClients(
  criteria: ClientSearchCriteria,
): Promise<ClientSearchResultPayload> {
  const res = await fetch(`${BASE_URL}?${searchQuery(criteria)}`, {
    headers: authHeaders(),
  });
  if (!res.ok) await throwApplicationRequestError(res);
  return (await res.json()).payload;
}
