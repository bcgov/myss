import { TextField } from "@bcgov/design-system-react-components";
import type { ComponentProps } from "react";
import { useLayoutEffect, useRef } from "react";

import { formatPhoneInput, PHONE_MAX_DIGITS } from "./phone";

type PhoneInputProps = ComponentProps<typeof TextField>;

/** Where the caret goes once the formatted value is on screen. */
interface PendingCaret {
  value: string;
  /** Put the caret after this many digits... */
  digitsBefore?: number;
  /** ...or, for text the formatter left alone, at this index. */
  position?: number;
}

const countDigits = (text: string) => text.replace(/\D/g, "").length;
const isPhoneText = (text: string) => /^[\d().\s-]*$/.test(text);

/** Whether `next` is `previous` with text added and nothing removed. */
function isInsertion(previous: string, next: string): boolean {
  if (next.length <= previous.length) return false;
  let prefix = 0;
  while (prefix < previous.length && previous[prefix] === next[prefix]) prefix++;
  return next.endsWith(previous.slice(prefix));
}

function caretAfterDigits(value: string, digits: number): number {
  if (digits === 0) return value.startsWith("(") ? 1 : 0;
  let seen = 0;
  for (let index = 0; index < value.length; index++) {
    if (/\d/.test(value[index]) && ++seen === digits) return index + 1;
  }
  return value.length;
}

/**
 * A telephone TextField that formats as the citizen types: (250) 555-0199.
 * Refuses an eleventh digit, and keeps the caret beside the digit it was
 * after, since the inserted brackets, space and hyphen would otherwise move it.
 */
export default function PhoneInput(props: Readonly<PhoneInputProps>) {
  // The design system TextField passes no ref to its input; its focus event
  // does carry it.
  const input = useRef<HTMLInputElement | null>(null);
  const pending = useRef<PendingCaret | null>(null);

  // Form.io re-renders asynchronously, possibly more than once per change:
  // wait for the render that shows the formatted value.
  useLayoutEffect(() => {
    const caret = pending.current;
    const element = input.current;
    if (!caret || !element || element.value !== caret.value) return;
    pending.current = null;
    if (document.activeElement !== element) return;
    const position =
      caret.digitsBefore === undefined
        ? Math.min(caret.position ?? caret.value.length, caret.value.length)
        : caretAfterDigits(caret.value, caret.digitsBefore);
    element.setSelectionRange(position, position);
  });

  return (
    <TextField
      {...props}
      type="tel"
      autoComplete={props.autoComplete ?? "tel-national"}
      onFocus={(event) => {
        if (event.target instanceof HTMLInputElement) input.current = event.target;
        props.onFocus?.(event);
      }}
      onChange={(raw) => {
        const caret = input.current?.selectionStart ?? raw.length;
        const beforeCaret = raw.slice(0, caret);
        const previous = typeof props.value === "string" ? props.value : "";
        const added = countDigits(raw) - countDigits(previous);

        let next: string;
        let digitsBefore: number;
        if (
          isPhoneText(raw) &&
          added > 0 &&
          countDigits(previous) >= PHONE_MAX_DIGITS &&
          isInsertion(previous, raw)
        ) {
          // Already ten digits: refuse the added digits and leave the caret
          // where it was. A paste over the number replaces it instead.
          next = previous;
          digitsBefore = countDigits(beforeCaret) - added;
        } else {
          next = formatPhoneInput(raw);
          digitsBefore = Math.min(countDigits(beforeCaret), PHONE_MAX_DIGITS);
        }

        pending.current = isPhoneText(next)
          ? { value: next, digitsBefore }
          : { value: next, position: caret - (beforeCaret.match(/\s/g) ?? []).length };
        props.onChange?.(next);
      }}
    />
  );
}
