import {
  fetchMe as fetchMePayload,
  type MePayloadBase,
} from "@myss/web-common/auth";

import { API_URL } from "@/constants";

// GET /v1/auth/me — the server-computed caller identity (see
// @myss/web-common's fetchMe), from MyssApi.

export { HttpError } from "@myss/web-common/auth";

/** Mirrors `MeModel` in MyssApi/Models/MeModel.cs (`CurrentUser` plus the profile fields). */
export interface MePayload extends MePayloadBase {
  hasProfile: boolean;
  profileFirstName?: string | null;
}

export function fetchMe(): Promise<MePayload> {
  return fetchMePayload<MePayload>(API_URL);
}
