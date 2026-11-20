import { describe, expect, it } from "vitest";

import {
  ATTACHMENT_KEYWORDS as K,
  ATTACHMENT_MAX_BYTES,
  attachmentErrorMessage,
  checkAttachmentFile,
  formatFileSize,
} from "./attachments";

const PDF = [0x25, 0x50, 0x44, 0x46, 0x2d];
const PNG = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];
const JPEG = [0xff, 0xd8, 0xff];

function file(
  head: number[],
  type: string,
  size = head.length + 10,
  name = "doc",
) {
  const bytes = new Uint8Array(size);
  bytes.set(head.slice(0, size));
  return new File([bytes], name, { type });
}

// The same checks as the API's, so a file kept before sign-in is not refused after it.
describe("checkAttachmentFile", () => {
  it("refuses an empty file", async () => {
    expect(
      await checkAttachmentFile(
        new File([], "empty.pdf", { type: "application/pdf" }),
      ),
    ).toBe(K.empty);
  });

  it("accepts 5 MB and refuses one byte more", async () => {
    expect(
      await checkAttachmentFile(
        file(PDF, "application/pdf", ATTACHMENT_MAX_BYTES),
      ),
    ).toBeNull();
    expect(
      await checkAttachmentFile(
        file(PDF, "application/pdf", ATTACHMENT_MAX_BYTES + 1),
      ),
    ).toBe(K.tooLarge);
  });

  it.each([
    ["application/pdf", PDF],
    ["image/png", PNG],
    ["image/jpeg", JPEG],
  ])("accepts %s with its first bytes", async (type, head) => {
    expect(await checkAttachmentFile(file(head, type))).toBeNull();
  });

  it.each(["application/pdf", "image/png", "image/jpeg"])(
    "refuses %s without its first bytes",
    async (type) => {
      const text = new File(["just some text, renamed"], "notes.pdf", { type });
      expect(await checkAttachmentFile(text)).toBe(K.typeNotAllowed);
    },
  );

  it("refuses a type the API does not accept", async () => {
    const docx = file(
      [0x50, 0x4b, 0x03, 0x04],
      "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
    );
    expect(await checkAttachmentFile(docx)).toBe(K.typeNotAllowed);
    expect(await checkAttachmentFile(file(PDF, ""))).toBe(K.typeNotAllowed);
  });

  it("reads the declared type regardless of case, as the API does", async () => {
    expect(await checkAttachmentFile(file(PDF, "Application/PDF"))).toBeNull();
  });
});

describe("formatFileSize", () => {
  it.each([
    [0, "0 bytes"],
    [1023, "1023 bytes"],
    [421_888, "412 KB"],
    [1_887_437, "1.8 MB"],
    [ATTACHMENT_MAX_BYTES, "5.0 MB"],
  ])("%i bytes reads %s", (size, text) => {
    expect(formatFileSize(size)).toBe(text);
  });
});

describe("attachmentErrorMessage", () => {
  it("words each keyword the API returns", () => {
    expect(attachmentErrorMessage(K.infected)).toBe(
      "The file failed the virus scan and was not stored.",
    );
    expect(attachmentErrorMessage(K.tooLarge)).toBe(
      "The file is too large. The limit is 5 MB.",
    );
  });

  it("has no wording for an unknown or missing keyword", () => {
    expect(attachmentErrorMessage("DOC.SOMETHING.ELSE")).toBeUndefined();
    expect(attachmentErrorMessage(undefined)).toBeUndefined();
  });
});
