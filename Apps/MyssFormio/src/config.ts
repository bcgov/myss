// Settings the host app hands the Form.io components at registration. The
// package never reads an app's runtime config itself: each app resolves its
// own values (window.APP_CONFIG, Vite env) and passes them in through
// registerBcgovComponents.

let canadaPostApiKey = "";

/** Sets the AddressComplete key the address autocomplete calls Canada Post with. */
export function setCanadaPostApiKey(key: string | undefined): void {
  canadaPostApiKey = key?.trim() ?? "";
}

/** The AddressComplete key, or "" when the host app configured none. */
export function getCanadaPostApiKey(): string {
  return canadaPostApiKey;
}
