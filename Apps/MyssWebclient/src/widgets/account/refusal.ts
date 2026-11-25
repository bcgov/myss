import {
  AccountRequestError,
  EMAIL_TOO_SOON_KEYWORD,
} from "@/hooks/useAccount";

/** The message a refused email call shows; a 429 is a wait, not a failure. */
export function refusal(
  error: Error,
  failed: string,
): {
  variant: "warning" | "danger";
  text: string;
} {
  return error instanceof AccountRequestError &&
    error.keyword === EMAIL_TOO_SOON_KEYWORD
    ? { variant: "warning", text: error.message }
    : { variant: "danger", text: `${failed} ${error.message}` };
}
