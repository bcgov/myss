import { describe, expect, it } from "vitest";

import { parseMessageText, type InlinePart, type MessageBlock } from "./messageTextParser";

// The parts of each line or bullet point, without the offsets the renderer
// uses only as keys.
type Shape =
  | { paragraph: unknown[][] }
  | { list: unknown[][] };

function shapeOf(part: InlinePart): unknown {
  switch (part.kind) {
    case "text":
      return part.text;
    case "bold":
      return { bold: part.parts.map(shapeOf) };
    case "link":
      return { link: part.text, href: part.href };
    case "placeholder":
      return { placeholder: part.name };
  }
}

function shape(message: string): Shape[] {
  return parseMessageText(message).map((block: MessageBlock) =>
    block.kind === "list"
      ? { list: block.items.map((item) => item.parts.map(shapeOf)) }
      : { paragraph: block.lines.map((line) => line.parts.map(shapeOf)) },
  );
}

describe("parseMessageText", () => {
  it.each<[string, string, Shape[]]>([
    ["blank lines start paragraphs", "One.\n\nTwo.", [{ paragraph: [["One."]] }, { paragraph: [["Two."]] }]],
    ["a single line break stays in the paragraph", "One.\nTwo.", [{ paragraph: [["One."], ["Two."]] }]],
    ["a Windows line break counts as one", "One.\r\nTwo.\r\n\r\nThree.", [{ paragraph: [["One."], ["Two."]] }, { paragraph: [["Three."]] }]],
    ["lines starting with a dash and a space are a list", "Bring:\n- your card\n- your number", [{ paragraph: [["Bring:"]] }, { list: [["your card"], ["your number"]] }]],
    ["a dash without a space is text", "-5 degrees", [{ paragraph: [["-5 degrees"]] }]],
    ["bold", "Keep **this** safe.", [{ paragraph: [["Keep ", { bold: ["this"] }, " safe."]] }]],
    ["an unclosed bold mark stays text", "Keep **this safe.", [{ paragraph: [["Keep **this safe."]] }]],
    ["a placeholder inside bold", "**Number {referenceNumber}**", [{ paragraph: [[{ bold: ["Number ", { placeholder: "referenceNumber" }] }]] }]],
    ["an https link", "See [the program](https://www2.gov.bc.ca).", [{ paragraph: [["See ", { link: "the program", href: "https://www2.gov.bc.ca" }, "."]] }]],
    ["an email link", "[Email us](mailto:buspass@example.com)", [{ paragraph: [[{ link: "Email us", href: "mailto:buspass@example.com" }]] }]],
    ["a phone link", "[Call us](tel:18005551234)", [{ paragraph: [[{ link: "Call us", href: "tel:18005551234" }]] }]],
    ["a javascript link stays text", "[x](javascript:alert(1))", [{ paragraph: [["[x](javascript:alert(1))"]] }]],
    ["an http link stays text", "[x](http://example.com)", [{ paragraph: [["[x](http://example.com)"]] }]],
    ["HTML stays text", "<b>Bold</b> <img src=x onerror=alert(1)>", [{ paragraph: [["<b>Bold</b> <img src=x onerror=alert(1)>"]] }]],
    ["the reference number placeholder", "Your number is {referenceNumber}.", [{ paragraph: [["Your number is ", { placeholder: "referenceNumber" }, "."]] }]],
    ["nothing but blank lines", "\n  \n", []],
  ])("%s", (_name, message, expected) => {
    expect(shape(message)).toEqual(expected);
  });

  // The links formMessages.unit.test.ts accepts all render as links.
  it.each([
    ["the program", "https://www2.gov.bc.ca/gov/content/bus-pass"],
    ["email us", "mailto:buspass@example.com"],
    ["call us", "tel:18005551234"],
    ["shouting", "HTTPS://www2.gov.bc.ca"],
  ])("parses an accepted link to %s as a link", (text, href) => {
    expect(shape(`See [${text}](${href}).`)).toEqual([
      { paragraph: [["See ", { link: text, href }, "."]] },
    ]);
  });

  it("gives each block, line and part a distinct offset among its siblings", () => {
    const [paragraph, list] = parseMessageText("A **b** c\nD\n- e\n- f");

    expect(paragraph.at).toBe(0);
    expect(paragraph.kind === "paragraph" && paragraph.lines.map((line) => line.at)).toEqual([0, 10]);
    expect(paragraph.kind === "paragraph" && paragraph.lines[0].parts.map((part) => part.at)).toEqual([0, 2, 7]);
    expect(list.kind === "list" && list.items.map((item) => item.at)).toEqual([14, 18]);
  });
});
