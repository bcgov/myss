import {
  fetchMe as fetchMePayload,
  type MePayloadBase,
} from "@myss/web-common/auth";

import { API_URL } from "@/constants";

// GET /v1/auth/me — the server-computed caller identity (see
// @myss/web-common's fetchMe), from MyssAdminApi.

export { HttpError } from "@myss/web-common/auth";

/**
 * Mirrors `CurrentUser` in MyssCommon/Auth/CurrentUser.cs, which MyssAdminApi
 * returns as is. Unlike MyssApi's `MeModel` it has no citizen-profile fields:
 * staff have no citizen profile.
 */
export type MePayload = MePayloadBase;

export function fetchMe(): Promise<MePayload> {
  return fetchMePayload<MePayload>(API_URL);
}
