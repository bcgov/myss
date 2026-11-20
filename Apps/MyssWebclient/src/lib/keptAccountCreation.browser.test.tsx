import { afterEach, describe, expect, it, vi } from "vitest";

import { openingAnswers } from "@/lib/accountContext";
import {
  canKeepFiles,
  clearKept,
  deleteExpiredKeptFiles,
  deleteKeptFile,
  keepAnswers,
  keepFile,
  readKeptAnswers,
  takeKeptFile,
} from "@/lib/keptAccountCreation";

// In the browser project, where session storage, IndexedDB and Web Crypto are real.

const DATABASE = "myss-account-creation";
const PDF_TEXT = "%PDF-1.7 a signed consent form";

function pdf(name = "consent.pdf") {
  return new File([PDF_TEXT], name, { type: "application/pdf" });
}

function minutesBefore(now: Date, minutes: number) {
  return new Date(now.getTime() - minutes * 60 * 1000);
}

// The rows as IndexedDB holds them, read without the module.
async function storedRows(): Promise<Record<string, unknown>[]> {
  const db = await new Promise<IDBDatabase>((resolve, reject) => {
    const req = indexedDB.open(DATABASE, 1);
    req.onupgradeneeded = () =>
      req.result.createObjectStore("files", { keyPath: "id" });
    req.onsuccess = () => resolve(req.result);
    req.onerror = () => reject(req.error);
  });
  try {
    return await new Promise((resolve, reject) => {
      const req = db.transaction("files").objectStore("files").getAll();
      req.onsuccess = () => resolve(req.result as Record<string, unknown>[]);
      req.onerror = () => reject(req.error);
    });
  } finally {
    db.close();
  }
}

afterEach(async () => {
  vi.restoreAllMocks();
  sessionStorage.clear();
  await new Promise<void>((resolve) => {
    const req = indexedDB.deleteDatabase(DATABASE);
    req.onsuccess = () => resolve();
    req.onerror = () => resolve();
    req.onblocked = () => resolve();
  });
});

describe("the answers", () => {
  it("come back as they were kept", () => {
    const answers = {
      ...openingAnswers(),
      context: "legalRepresentative" as const,
      representativeFirstName: "Pat",
    };
    const now = new Date();

    expect(keepAnswers(answers, { now })).toBe(true);

    expect(readKeptAnswers(now)).toEqual({
      answers,
      savedAt: now,
      filesNotKept: false,
    });
  });

  it("say when the files could not be kept", () => {
    keepAnswers(openingAnswers(), { filesNotKept: true });

    expect(readKeptAnswers()?.filesNotKept).toBe(true);
  });

  it("are gone after an hour", () => {
    const now = new Date();
    keepAnswers(openingAnswers(), { now: minutesBefore(now, 61) });

    expect(readKeptAnswers(now)).toBeNull();
    expect(sessionStorage.length).toBe(0);
  });

  it("are not kept when session storage refuses them", () => {
    vi.spyOn(Storage.prototype, "setItem").mockImplementation(() => {
      throw new DOMException("Storage is blocked", "SecurityError");
    });

    expect(keepAnswers(openingAnswers())).toBe(false);
    expect(readKeptAnswers()).toBeNull();
  });
});

describe("the files", () => {
  it("come back byte for byte, with their name and type", async () => {
    const id = await keepFile(pdf());

    const file = await takeKeptFile(id!);

    expect(file?.name).toBe("consent.pdf");
    expect(file?.type).toBe("application/pdf");
    expect(await file?.text()).toBe(PDF_TEXT);
  });

  it("are stored encrypted, the name included", async () => {
    await keepFile(pdf("Pat Lee power of attorney.pdf"));

    const [row] = await storedRows();
    const stored = new TextDecoder().decode(row.data as ArrayBuffer);
    expect(stored).not.toContain("%PDF");
    expect(stored).not.toContain("Pat Lee");
    expect(JSON.stringify(row)).not.toContain("Pat Lee");
  });

  it("cannot be read once the tab's key is gone", async () => {
    const id = await keepFile(pdf());
    sessionStorage.removeItem("myss.accountCreation.key");

    expect(await takeKeptFile(id!)).toBeNull();
  });

  it("can be deleted one at a time", async () => {
    const first = await keepFile(pdf("first.pdf"));
    const second = await keepFile(pdf("second.pdf"));

    await deleteKeptFile(first!);

    expect(await takeKeptFile(first!)).toBeNull();
    expect((await takeKeptFile(second!))?.name).toBe("second.pdf");
  });

  it("over an hour old are deleted, and younger ones kept", async () => {
    const now = new Date();
    const old = await keepFile(pdf("old.pdf"), minutesBefore(now, 61));
    const recent = await keepFile(pdf("recent.pdf"), minutesBefore(now, 59));

    await deleteExpiredKeptFiles(now);

    expect(await takeKeptFile(old!)).toBeNull();
    expect((await takeKeptFile(recent!))?.name).toBe("recent.pdf");
  });
});

describe("clearKept", () => {
  it("leaves nothing of the tab's answers, key or files", async () => {
    keepAnswers(openingAnswers());
    await keepFile(pdf());

    await clearKept();

    expect(sessionStorage.length).toBe(0);
    expect(readKeptAnswers()).toBeNull();
    expect(await storedRows()).toEqual([]);
  });

  it("does nothing when the tab kept nothing", async () => {
    await expect(clearKept()).resolves.toBeUndefined();
  });
});

describe("a browser that refuses IndexedDB", () => {
  it("cannot keep files, and nothing throws", async () => {
    vi.spyOn(indexedDB, "open").mockImplementation(() => {
      throw new DOMException("IndexedDB is unavailable", "InvalidStateError");
    });

    expect(await canKeepFiles()).toBe(false);
    expect(await keepFile(pdf())).toBeNull();
    expect(await takeKeptFile("anything")).toBeNull();
    await expect(deleteKeptFile("anything")).resolves.toBeUndefined();
    await expect(deleteExpiredKeptFiles(new Date())).resolves.toBeUndefined();
  });

  it("can keep files when everything works", async () => {
    expect(await canKeepFiles()).toBe(true);
  });
});
