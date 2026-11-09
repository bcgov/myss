import { isSafeLinkTarget } from "../api/formMessages";

// A form message's formatting set, parsed into blocks of inline parts; anything
// else, HTML included, stays text. Each part's offset in the message is its render key.

export type InlinePart =
  | { kind: "text"; at: number; text: string }
  | { kind: "bold"; at: number; parts: readonly InlinePart[] }
  | { kind: "link"; at: number; text: string; href: string }
  | { kind: "placeholder"; at: number; name: string };

/** One line of a paragraph, or one bullet point. */
export interface MessageLine {
  at: number;
  parts: readonly InlinePart[];
}

export type MessageBlock =
  | { kind: "paragraph"; at: number; lines: MessageLine[] }
  | { kind: "list"; at: number; items: MessageLine[] };

const LIST_ITEM = "- ";
const INLINE =
  /\*\*([^*\n]+?)\*\*|\[([^\]\n]+)\]\(([^)\s]+)\)|\{([A-Za-z][A-Za-z0-9]*)\}/g;

function pushText(parts: InlinePart[], at: number, text: string): void {
  const last = parts.at(-1);
  if (last?.kind === "text") {
    parts[parts.length - 1] = { ...last, text: last.text + text };
  } else {
    parts.push({ kind: "text", at, text });
  }
}

/** The inline parts of one line: bold, links to https, email or phone, and placeholders. */
function parseInline(text: string, offset: number): InlinePart[] {
  const parts: InlinePart[] = [];
  let last = 0;
  for (const match of text.matchAll(INLINE)) {
    const [whole, bold, linkText, href, name] = match;
    const at = offset + match.index;
    if (match.index > last) {
      pushText(parts, offset + last, text.slice(last, match.index));
    }
    if (bold !== undefined) {
      parts.push({ kind: "bold", at, parts: parseInline(bold, at + 2) });
    } else if (linkText !== undefined && isSafeLinkTarget(href)) {
      parts.push({ kind: "link", at, text: linkText, href });
    } else if (name !== undefined) {
      parts.push({ kind: "placeholder", at, name });
    } else {
      pushText(parts, at, whole);
    }
    last = match.index + whole.length;
  }
  if (last < text.length) {
    pushText(parts, offset + last, text.slice(last));
  }
  return parts;
}

/**
 * Splits a message into paragraphs and bulleted lists. A blank line ends a
 * block; a line that starts with "- " is a bullet point; other lines in a
 * row form one paragraph, a line break between each.
 */
export function parseMessageText(message: string): MessageBlock[] {
  const blocks: MessageBlock[] = [];
  let current: MessageBlock | undefined;
  let offset = 0;
  for (const line of message.replace(/\r\n?/g, "\n").split("\n")) {
    const at = offset;
    offset += line.length + 1;
    if (line.trim() === "") {
      current = undefined;
    } else if (line.startsWith(LIST_ITEM)) {
      if (current?.kind !== "list") {
        current = { kind: "list", at, items: [] };
        blocks.push(current);
      }
      const itemAt = at + LIST_ITEM.length;
      current.items.push({ at: itemAt, parts: parseInline(line.slice(LIST_ITEM.length), itemAt) });
    } else {
      if (current?.kind !== "paragraph") {
        current = { kind: "paragraph", at, lines: [] };
        blocks.push(current);
      }
      current.lines.push({ at, parts: parseInline(line, at) });
    }
  }
  return blocks;
}

function partsShow(parts: readonly InlinePart[], name: string): boolean {
  return parts.some((part) =>
    part.kind === "bold" ? partsShow(part.parts, name) : part.kind === "placeholder" && part.name === name,
  );
}

/** Whether a message shows the value of placeholder `name`; one inside a link is never replaced, so it does not count. */
export function messageShowsPlaceholder(message: string, name: string): boolean {
  return parseMessageText(message).some((block) =>
    (block.kind === "list" ? block.items : block.lines).some((line) => partsShow(line.parts, name)),
  );
}
