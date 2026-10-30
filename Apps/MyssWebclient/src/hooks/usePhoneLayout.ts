import { useSyncExternalStore } from "react";

/** Widths that get the phone layout; the stylesheets use the same 767px. */
export const PHONE_LAYOUT_QUERY = "(max-width: 767px)";

function subscribe(onChange: () => void): () => void {
  const query = window.matchMedia(PHONE_LAYOUT_QUERY);
  query.addEventListener("change", onChange);
  return () => query.removeEventListener("change", onChange);
}

function isPhoneWidth(): boolean {
  return window.matchMedia(PHONE_LAYOUT_QUERY).matches;
}

/** Whether the window is phone width; follows resizing and zoom. */
export function usePhoneLayout(): boolean {
  return useSyncExternalStore(subscribe, isPhoneWidth);
}
