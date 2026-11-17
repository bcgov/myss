// What the questions page keeps in this browser tab while the citizen signs
// in. The answers go in session storage, which belongs to the tab and is
// cleared when it closes. The files go in IndexedDB, encrypted under a key
// held only in the tab's session storage, so they cannot be read once the tab
// closes. Nothing here throws to the page: a browser that refuses storage
// keeps nothing, and the page asks again after sign-in.

import type { AccountCreationAnswers } from "@/lib/accountContext";

const ANSWERS_KEY = "myss.accountCreation.answers";
const CRYPTO_KEY = "myss.accountCreation.key";
const TAB_KEY = "myss.accountCreation.tab";
const DATABASE = "myss-account-creation";
const STORE = "files";

/** How long kept answers and files last. */
export const KEEP_FOR_MS = 60 * 60 * 1000;

export interface KeptAnswers {
  answers: AccountCreationAnswers;
  savedAt: Date;
  /** The browser could not keep the files, so they are attached after sign-in. */
  filesNotKept: boolean;
}

interface StoredFile {
  id: string;
  /** The tab that kept it, so that tab can clear its own files. */
  tab: string;
  savedAt: number;
  iv: Uint8Array<ArrayBuffer>;
  /** The file's name, type and bytes, encrypted together. */
  data: ArrayBuffer;
}

function readSession(key: string): string | null {
  try {
    return sessionStorage.getItem(key);
  } catch {
    return null;
  }
}

function writeSession(key: string, value: string): boolean {
  try {
    sessionStorage.setItem(key, value);
    return true;
  } catch {
    return false;
  }
}

function removeSession(key: string) {
  try {
    sessionStorage.removeItem(key);
  } catch {
    // Storage is blocked, so nothing was kept there.
  }
}

/** Keeps the answers in the tab, with the time they were kept. */
export function keepAnswers(
  answers: AccountCreationAnswers,
  {
    now = new Date(),
    filesNotKept = false,
  }: { now?: Date; filesNotKept?: boolean } = {},
): boolean {
  return writeSession(
    ANSWERS_KEY,
    JSON.stringify({ savedAt: now.getTime(), filesNotKept, answers }),
  );
}

/** The tab's kept answers, or null when there are none or they are over an hour old. */
export function readKeptAnswers(now: Date = new Date()): KeptAnswers | null {
  const raw = readSession(ANSWERS_KEY);
  if (raw === null) return null;
  try {
    const kept = JSON.parse(raw) as {
      savedAt: number;
      filesNotKept?: boolean;
      answers: AccountCreationAnswers;
    };
    if (now.getTime() - kept.savedAt > KEEP_FOR_MS) {
      removeSession(ANSWERS_KEY);
      return null;
    }
    return {
      answers: kept.answers,
      savedAt: new Date(kept.savedAt),
      filesNotKept: kept.filesNotKept === true,
    };
  } catch {
    return null;
  }
}

function request<T>(req: IDBRequest<T>): Promise<T> {
  return new Promise((resolve, reject) => {
    req.onsuccess = () => resolve(req.result);
    req.onerror = () => reject(req.error);
  });
}

function openDatabase(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    const req = indexedDB.open(DATABASE, 1);
    req.onupgradeneeded = () =>
      req.result.createObjectStore(STORE, { keyPath: "id" });
    req.onsuccess = () => resolve(req.result);
    req.onerror = () => reject(req.error);
    req.onblocked = () => reject(new Error("The database is blocked."));
  });
}

/** Runs one transaction and closes the database after it. */
async function withStore<T>(
  mode: IDBTransactionMode,
  run: (store: IDBObjectStore) => Promise<T>,
): Promise<T> {
  const db = await openDatabase();
  try {
    const transaction = db.transaction(STORE, mode);
    const done = new Promise<void>((resolve, reject) => {
      transaction.oncomplete = () => resolve();
      transaction.onerror = () => reject(transaction.error);
      transaction.onabort = () => reject(transaction.error);
    });
    const [result] = await Promise.all([
      run(transaction.objectStore(STORE)),
      done,
    ]);
    return result;
  } finally {
    db.close();
  }
}

function deleteWhere(
  store: IDBObjectStore,
  matches: (file: StoredFile) => boolean,
): Promise<void> {
  return new Promise((resolve, reject) => {
    const req = store.openCursor();
    req.onsuccess = () => {
      const cursor = req.result;
      if (!cursor) {
        resolve();
        return;
      }
      if (matches(cursor.value as StoredFile)) cursor.delete();
      cursor.continue();
    };
    req.onerror = () => reject(req.error);
  });
}

// Opening a database creates it, so a page that only tidies up first asks
// whether there is anything to tidy.
async function databaseExists(): Promise<boolean> {
  if (typeof indexedDB.databases !== "function") return true;
  const databases = await indexedDB.databases();
  return databases.some((d) => d.name === DATABASE);
}

function toBase64(bytes: Uint8Array): string {
  return btoa(String.fromCharCode(...bytes));
}

function fromBase64(text: string): Uint8Array<ArrayBuffer> {
  return Uint8Array.from(atob(text), (c) => c.charCodeAt(0));
}

async function tabKey(create: boolean): Promise<CryptoKey | null> {
  const saved = readSession(CRYPTO_KEY);
  if (saved) {
    return crypto.subtle.importKey("raw", fromBase64(saved), "AES-GCM", false, [
      "encrypt",
      "decrypt",
    ]);
  }
  if (!create) return null;
  const key = await crypto.subtle.generateKey(
    { name: "AES-GCM", length: 256 },
    true,
    ["encrypt", "decrypt"],
  );
  const raw = new Uint8Array(await crypto.subtle.exportKey("raw", key));
  return writeSession(CRYPTO_KEY, toBase64(raw)) ? key : null;
}

function tabId(): string | null {
  const saved = readSession(TAB_KEY);
  if (saved) return saved;
  const created = crypto.randomUUID();
  return writeSession(TAB_KEY, created) ? created : null;
}

/** Whether this tab can keep files: session storage, Web Crypto and IndexedDB all work. */
export async function canKeepFiles(): Promise<boolean> {
  try {
    if (typeof indexedDB === "undefined" || !globalThis.crypto?.subtle) {
      return false;
    }
    if (tabId() === null) return false;
    (await openDatabase()).close();
    return true;
  } catch {
    return false;
  }
}

/** Encrypts and keeps a file in the tab. Returns its ID, or null when it could not be kept. */
export async function keepFile(
  file: File,
  now: Date = new Date(),
): Promise<string | null> {
  try {
    const key = await tabKey(true);
    const tab = tabId();
    if (!key || !tab) return null;

    // The name and type travel with the bytes, so nothing is stored in clear.
    const header = new TextEncoder().encode(
      JSON.stringify({ name: file.name, type: file.type }),
    );
    const plain = new Uint8Array(4 + header.length + file.size);
    new DataView(plain.buffer).setUint32(0, header.length);
    plain.set(header, 4);
    plain.set(new Uint8Array(await file.arrayBuffer()), 4 + header.length);

    const iv = crypto.getRandomValues(new Uint8Array(12));
    const data = await crypto.subtle.encrypt(
      { name: "AES-GCM", iv },
      key,
      plain,
    );
    const stored: StoredFile = {
      id: crypto.randomUUID(),
      tab,
      savedAt: now.getTime(),
      iv,
      data,
    };
    await withStore("readwrite", (store) => request(store.put(stored)));
    return stored.id;
  } catch {
    return null;
  }
}

/** Reads a kept file back, or null when it is gone or this tab cannot decrypt it. */
export async function takeKeptFile(id: string): Promise<File | null> {
  try {
    const key = await tabKey(false);
    if (!key) return null;
    const stored = (await withStore("readonly", (store) =>
      request(store.get(id)),
    )) as StoredFile | undefined;
    if (!stored) return null;

    const plain = new Uint8Array(
      await crypto.subtle.decrypt(
        { name: "AES-GCM", iv: stored.iv },
        key,
        stored.data,
      ),
    );
    const headerLength = new DataView(plain.buffer).getUint32(0);
    const header = JSON.parse(
      new TextDecoder().decode(plain.subarray(4, 4 + headerLength)),
    ) as { name: string; type: string };
    return new File([plain.subarray(4 + headerLength)], header.name, {
      type: header.type,
    });
  } catch {
    return null;
  }
}

export async function deleteKeptFile(id: string): Promise<void> {
  try {
    await withStore("readwrite", (store) => request(store.delete(id)));
  } catch {
    // Nothing was kept, or the browser no longer lets the page reach it.
  }
}

/** Deletes everything this tab kept: the answers, the key and its files. */
export async function clearKept(): Promise<void> {
  const tab = readSession(TAB_KEY);
  removeSession(ANSWERS_KEY);
  removeSession(CRYPTO_KEY);
  removeSession(TAB_KEY);
  if (tab === null) return;
  try {
    if (!(await databaseExists())) return;
    await withStore("readwrite", (store) =>
      deleteWhere(store, (file) => file.tab === tab),
    );
  } catch {
    // The key is gone, so any file left behind can no longer be read.
  }
}

/** Deletes files kept more than an hour ago, by any tab. */
export async function deleteExpiredKeptFiles(now: Date): Promise<void> {
  try {
    if (!(await databaseExists())) return;
    await withStore("readwrite", (store) =>
      deleteWhere(store, (file) => now.getTime() - file.savedAt > KEEP_FOR_MS),
    );
  } catch {
    // Tidying up is best effort; the next page tries again.
  }
}
