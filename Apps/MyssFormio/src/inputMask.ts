// A Form.io input mask applied to a controlled value. Form.io masks a native
// input with the Inputmask library; the BC Gov text field is a React-controlled
// input, so the mask is applied to the typed value instead and the result is
// what the field shows and stores (the same masked text Form.io stores).
//
// Mask characters, as the Inputmask library reads them:
//   9  a digit            a  a letter            *  a letter or digit
//   [ ]  an optional group (`9[9]` is one or two digits)
//   anything else is a literal that is inserted as the user types past it.

interface MaskToken {
  readonly kind: "digit" | "letter" | "alphanumeric" | "literal";
  readonly char: string;
}

function tokenize(mask: string): MaskToken[] {
  const tokens: MaskToken[] = [];
  for (const char of mask) {
    switch (char) {
      case "9":
        tokens.push({ kind: "digit", char });
        break;
      case "a":
      case "A":
        tokens.push({ kind: "letter", char });
        break;
      case "*":
        tokens.push({ kind: "alphanumeric", char });
        break;
      case "[":
      case "]":
        // Optional groups only matter for completeness checks, which are
        // Form.io's job; for formatting, an optional slot is a slot.
        break;
      default:
        tokens.push({ kind: "literal", char });
    }
  }
  return tokens;
}

function accepts(token: MaskToken, char: string): boolean {
  switch (token.kind) {
    case "digit":
      return /\d/.test(char);
    case "letter":
      return /[a-zA-Z]/.test(char);
    case "alphanumeric":
      return /[a-zA-Z0-9]/.test(char);
    default:
      return false;
  }
}

/**
 * Formats `raw` with `mask`: placeholder slots take the next typed character
 * that fits them, characters that fit nothing are dropped, and a literal is
 * written as soon as something is typed past it. "2505550199" under
 * "(999) 999-9999" becomes "(250) 555-0199"; a pasted "(250) 555-0199" is
 * unchanged. An empty mask returns `raw` untouched.
 */
export function applyInputMask(raw: string, mask: string | undefined): string {
  if (!mask) return raw;

  const tokens = tokenize(mask);
  let out = "";
  let index = 0;

  for (const token of tokens) {
    if (token.kind === "literal") {
      if (index >= raw.length) break;
      if (raw[index] === token.char) index++;
      out += token.char;
      continue;
    }

    while (index < raw.length && !accepts(token, raw[index])) index++;
    if (index >= raw.length) break;
    out += raw[index];
    index++;
  }

  return out;
}

/** True when every slot in the mask is a digit, so a numeric keyboard fits. */
export function isNumericMask(mask: string | undefined): boolean {
  if (!mask) return false;
  const tokens = tokenize(mask);
  return (
    tokens.some((token) => token.kind === "digit") &&
    tokens.every((token) => token.kind === "digit" || token.kind === "literal")
  );
}
