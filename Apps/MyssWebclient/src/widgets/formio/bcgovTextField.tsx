// The text-entry wrappers: Form.io's textfield, email and textarea drawn with
// the design system TextField and TextArea. In their own module because the
// address autocomplete subclasses the text field, and bcgovComponents imports
// both for registration; a cycle between those two would leave the base class
// undefined at module evaluation time.

import type { ReactNode } from "react";
import { TextArea, TextField } from "@bcgov/design-system-react-components";

import {
  stockComponent,
  textOf,
  withBcgovField,
  type FormioFieldCtor,
} from "./bcgovField";
import { applyInputMask, isNumericMask } from "./inputMask";

type TextVariant = "text" | "email" | "textarea";

/**
 * Mixes a design system text input into a stock Form.io text class. The
 * variant decides the element and input type; the value flow is the same: a
 * controlled input whose changes go through Form.io's `updateValue`, with the
 * spec's `inputMask` applied to the typed text the way Form.io's native input
 * would have.
 */
export function withBcgovTextInput<TBase extends FormioFieldCtor>(
  Base: TBase,
  variant: TextVariant,
) {
  return class BcgovTextInput extends withBcgovField(Base) {
    protected renderReact(): ReactNode {
      const component = this.component;
      const props = this.bcgovFieldProps();
      // The design system fields take no placeholder, so a format hint the
      // form put there ("dd", "(999) 999-9999") is shown as the description
      // when the field has none of its own.
      props.description ??= textOf(component.placeholder) || undefined;
      const mask = textOf(component.inputMask) || undefined;
      const value = textOf(this.dataValue);
      const autoComplete = textOf(component.autocomplete) || undefined;
      const onChange = (next: string) => {
        this.bcgovCommit(applyInputMask(next, mask));
      };

      // The design system fields take no placeholder: a format hint belongs
      // in the description, where it stays visible once the field is filled.
      if (variant === "textarea") {
        return (
          <TextArea
            {...props}
            value={value}
            autoComplete={autoComplete}
            onChange={onChange}
          />
        );
      }

      return (
        <TextField
          {...props}
          type={variant === "email" ? "email" : "text"}
          inputMode={isNumericMask(mask) ? "numeric" : undefined}
          value={value}
          autoComplete={autoComplete}
          onChange={onChange}
        />
      );
    }
  };
}

/** Form.io's `textfield`, drawn with the design system TextField. */
export const BcgovTextFieldComponent = withBcgovTextInput(
  stockComponent("textfield"),
  "text",
);

/** Form.io's `email`: the TextField with an email input, keeping Form.io's email rule. */
export const BcgovEmailComponent = withBcgovTextInput(
  stockComponent("email"),
  "email",
);

/** Form.io's `textarea`, drawn with the design system TextArea. */
export const BcgovTextAreaComponent = withBcgovTextInput(
  stockComponent("textarea"),
  "textarea",
);
