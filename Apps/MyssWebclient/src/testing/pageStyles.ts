// Everything a page is styled with outside its own modules: the global
// layers main.tsx imports, and the stylesheets index.html links ahead of
// them (Bootstrap and the MySS theme). Browser tests that measure layout load
// these so the page measures as it does live.
import "@bcgov/design-tokens/css/variables.css";
import "@/index.css";
import "@bcgov/bc-sans/css/BC_Sans.css";

const LINKED_STYLESHEETS = ["/vendor/bootstrap.css", "/vendor/myss-theme.css"];

function stylesheetLink(href: string): {
  link: HTMLLinkElement;
  loaded: Promise<void>;
} {
  const link = document.createElement("link");
  link.rel = "stylesheet";
  link.href = href;
  const loaded = new Promise<void>((resolve, reject) => {
    link.onload = () => resolve();
    link.onerror = () => reject(new Error(`${href} did not load`));
  });
  return { link, loaded };
}

/** Adds the linked stylesheets at the top of <head>, where index.html has them. */
export async function loadPageStyles(): Promise<void> {
  if (document.querySelector(`link[href="${LINKED_STYLESHEETS[0]}"]`)) return;
  const sheets = LINKED_STYLESHEETS.map(stylesheetLink);
  document.head.prepend(...sheets.map((sheet) => sheet.link));
  await Promise.all(sheets.map((sheet) => sheet.loaded));
  await document.fonts.ready;
}
