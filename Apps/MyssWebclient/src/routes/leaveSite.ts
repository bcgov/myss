/**
 * Leaves the app for another site, replacing the current history entry so
 * Back does not return to a page that only redirects again. Its own module so
 * a test can stand in for it: a browser test cannot spy on window.location.
 */
export function leaveSite(url: string): void {
  window.location.replace(url);
}
