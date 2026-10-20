// The shared base for every Form.io component that draws itself with a BC Gov
// Design System component. A wrapper subclasses the stock Form.io class (so
// value handling, Form.io's own validation rules, conditionals and clearOnHide
// are inherited) and mixes this in, which replaces only the rendering:
//
//   - the React subtree is mounted into Form.io's wrapper markup and re-rendered
//     whenever Form.io changes the value, the validity or the disabled state;
//   - Form.io's validation message is mirrored into the component's
//     `isInvalid` / `errorMessage` props, gated behind a submit attempt, and
//     Form.io's own message markup is suppressed so nothing paints twice;
//   - a field's `properties.myssValidator` rule and `properties.myssMatches`
//     confirmation run alongside Form.io's rules (see validationRules.ts);
//   - `isRequired`, `isDisabled` and `name="data[<key>]"` come from the spec and
//     Form.io's state, so required markers, read-only rendering and the error
//     summary's focus buttons behave the same on every field.
//
// Inside the form builder's settings dialog the wrappers step aside and render
// Form.io's stock markup: that dialog is the designer's tool, built from
// Form.io's own edit forms, and it keeps Form.io's look. The builder canvas and
// every citizen-facing form render the design system components.

import type { ReactNode } from "react";
import { flushSync } from "react-dom";
import { createRoot, type Root } from "react-dom/client";
import { Components, Utils } from "@formio/js";

import {
  checkMatches,
  checkRule,
  ruleMessage,
  type RuleFailure,
} from "./validationRules";

/** One validation error as Form.io passes it around (a loose shape). */
export interface FormioError {
  level?: string;
  message?: string;
  ruleName?: string;
  path?: string;
  fromServer?: boolean;
  component?: Record<string, unknown>;
  context?: Record<string, unknown>;
  [key: string]: unknown;
}

/** The parts of a Form.io component instance the wrappers talk to. */
export interface FormioFieldInstance {
  component: Record<string, unknown>;
  /** Render options; Utils.sanitize reads its sanitize config from here. */
  options: Record<string, unknown>;
  refs: Record<
    string,
    HTMLElement | NodeListOf<HTMLElement> | null | undefined
  >;
  key: string;
  path?: string;
  id: string;
  /** The component's current answer, already coerced by the base class. */
  dataValue: unknown;
  /** The whole form's answers. */
  rootValue: Record<string, unknown>;
  /**
   * The Webform this component belongs to. `submitting` is true only while a
   * submit's own validation pass is running.
   */
  root?: {
    submitting?: boolean;
    everyComponent?(visit: (component: unknown) => void): void;
  } | null;
  disabled: boolean;
  visible: boolean;
  loadRefs(element: HTMLElement, refs: Record<string, string>): void;
  render(children?: string): string;
  attach(element: HTMLElement): Promise<void>;
  detach(): void;
  setValue(value: unknown, flags?: Record<string, unknown>): boolean;
  updateValue(value: unknown, flags?: Record<string, unknown>): boolean;
  setCustomValidity(
    messages: unknown,
    dirty?: boolean,
    external?: boolean,
  ): unknown;
  /** Renders Form.io's own error DOM into `refs.messageContainer`. */
  addMessages(messages?: unknown): void;
  setDirty(dirty: boolean): void;
  validateComponent(
    data?: unknown,
    row?: unknown,
    flags?: Record<string, unknown>,
  ): FormioError[] | Promise<FormioError[]>;
  checkComponentValidity(
    data?: unknown,
    dirty?: boolean,
    row?: unknown,
    flags?: Record<string, unknown>,
    errors?: FormioError[],
  ): boolean | Promise<boolean>;
  focus(index?: number): void;
  on(
    event: string,
    callback: (...args: unknown[]) => void,
    internal?: boolean,
  ): unknown;
  emit(event: string, ...args: unknown[]): void;
  t(text: string, params?: Record<string, unknown>): string;
}

/**
 * A Form.io component constructor. `@formio/js` types its registry loosely,
 * and a TypeScript mixin needs a rest-`any` constructor, hence the escape.
 */
// eslint-disable-next-line @typescript-eslint/no-explicit-any
export type FormioFieldCtor = new (...args: any[]) => FormioFieldInstance;

/** The registered class for a stock Form.io type, before it is replaced. */
export function stockComponent(type: string): FormioFieldCtor {
  const registry = (
    Components as unknown as { components: Record<string, FormioFieldCtor> }
  ).components;
  const ctor = registry[type];
  if (!ctor) throw new Error(`Form.io has no component type "${type}".`);
  return ctor;
}

/** The base Form.io component class, the root of every component's chain. */
const BaseComponent = stockComponent("component");

/**
 * The plain component wrapper, with no label and no input markup. A wrapper
 * reaches for this directly: its own `super.render()` emits the stock input
 * template, and the Field class above that adds a label the design system
 * component already draws.
 */
export const renderBareWrapper = (
  BaseComponent as unknown as {
    prototype: { render(children?: string): string };
  }
).prototype.render;

/**
 * A schema field read as text. Spec values arrive untyped: a string, number or
 * boolean reads as its text; anything else (missing, an object) as `fallback`.
 */
export function textOf(value: unknown, fallback = ""): string {
  if (typeof value === "string") return value;
  if (typeof value === "number" || typeof value === "boolean") {
    return String(value);
  }
  return fallback;
}

/**
 * The text of a label that may carry markup, sanitised through Form.io's
 * DOMPurify-backed Utils.sanitize first so nothing in it can run.
 */
export function plainText(
  html: string,
  options: Record<string, unknown> | undefined,
): string {
  if (!html.includes("<") && !html.includes("&")) return html;
  const holder = document.createElement("div");
  holder.innerHTML = Utils.sanitize(html, options ?? {});
  return holder.textContent?.trim() ?? "";
}

/** A component key read as words: "phoneType" becomes "Phone type". */
export function humanizeKey(key: string): string {
  const words = key
    .replace(/([a-z0-9])([A-Z])/g, "$1 $2")
    .replace(/[_-]+/g, " ")
    .trim()
    .toLowerCase();
  return words.charAt(0).toUpperCase() + words.slice(1);
}

/** Whether a Form.io component instance is one of these wrappers. */
function isWrapper(
  component: unknown,
): component is {
  bcgovConfirms(key: string): boolean;
  bcgovRecheckIfShowing(): void;
} {
  return (
    typeof component === "object" &&
    component !== null &&
    typeof (component as { bcgovConfirms?: unknown }).bcgovConfirms ===
      "function"
  );
}

/** Whether the spec marks a component required (`validate.required`). */
export function isRequired(component: Record<string, unknown>): boolean {
  const validate = component.validate;
  return (
    typeof validate === "object" &&
    validate !== null &&
    (validate as { required?: unknown }).required === true
  );
}

/** `setCustomValidity`'s argument as a list: an array as is, '' as none. */
function asList(messages: unknown): unknown[] {
  if (Array.isArray(messages)) return messages;
  return messages ? [messages] : [];
}

/**
 * Pull the message to show out of whatever `setCustomValidity` was handed: it
 * takes `''` to clear, a bare string, a single `{ message, level }` object, or
 * an array of them. Returns "" when there is nothing to show.
 */
export function firstErrorMessage(messages: unknown): string {
  for (const entry of asList(messages)) {
    if (typeof entry === "string" && entry) return entry;
    if (entry && typeof entry === "object") {
      const { message, level } = entry as {
        message?: unknown;
        level?: unknown;
      };
      // Form.io also emits 'warning'/'info' levels; only errors get the
      // component's danger treatment.
      if (
        typeof message === "string" &&
        message &&
        level !== "warning" &&
        level !== "info"
      ) {
        return message;
      }
    }
  }
  return "";
}

/** True for the values Form.io uses to clear a field. */
function isEmptyInput(value: unknown): boolean {
  return value === undefined || value === null || value === "";
}

/** Whether any message came back from the API (Form.io flags those). */
function hasServerMessage(messages: unknown): boolean {
  return asList(messages).some(
    (entry) =>
      entry !== null &&
      typeof entry === "object" &&
      (entry as { fromServer?: unknown }).fromServer === true,
  );
}

function isPromise<T>(value: unknown): value is Promise<T> {
  return (
    typeof value === "object" &&
    value !== null &&
    typeof (value as { then?: unknown }).then === "function"
  );
}

/**
 * The React subtree a Form.io component mounts inside its own markup. Owns the
 * root so every wrapper creates, re-renders and releases it the same way.
 */
export class ReactMount {
  private root?: Root;

  constructor(host: HTMLElement, node: ReactNode) {
    const root = createRoot(host);
    this.root = root;
    // Synchronously, so the subtree is in the DOM when Form.io's attach goes
    // on to wire things to it (the address autocomplete finds its input this
    // way). Form.io attaches outside React's own render, where flushSync is
    // allowed.
    flushSync(() => root.render(node));
  }

  /** Re-render into the root; a no-op once released. */
  render(node: ReactNode): void {
    this.root?.render(node);
  }

  /**
   * Unmount on the next microtask: doing it synchronously inside Form.io's
   * detach draws React's warning about unmounting while rendering. Idempotent.
   */
  release(): void {
    const root = this.root;
    this.root = undefined;
    if (root) queueMicrotask(() => root.unmount());
  }
}

/** The props every design system field takes from the spec and Form.io. */
export interface BcgovFieldProps {
  label?: string;
  "aria-label"?: string;
  description?: string;
  isRequired: boolean;
  isDisabled: boolean;
  isInvalid: boolean;
  errorMessage?: string;
  name: string;
}

/**
 * Mixes the design system rendering into a stock Form.io component class.
 * Subclasses implement `renderReact()`; everything else is shared. Every added
 * member is prefixed `bcgov` so no field shadows a Form.io method of the same
 * name (`errorMessage` is one: Form.io's Button calls it).
 */
export function withBcgovField<TBase extends FormioFieldCtor>(Base: TBase) {
  abstract class BcgovField extends Base {
    private bcgovMount?: ReactMount;

    /** Latest Form.io validation message, mirrored into the component. */
    protected bcgovErrorMessage = "";

    /**
     * Whether this field may display a validation message yet. `dirty` alone
     * is not enough: it stays set for the whole session after a submit, so a
     * conditional field revealed later would show "is required" untouched.
     * Unlocked by a submit attempt, re-locked when the field is cleared.
     */
    protected bcgovErrorsUnlocked = false;

    /**
     * Whether to render Form.io's stock markup instead: inside the builder's
     * settings dialog (`editComponent`) and for the throwaway instance the
     * builder makes to read a component's schema (`inFormBuilder`).
     */
    protected get bcgovStockRendering(): boolean {
      return (
        Boolean(this.options.editComponent) ||
        Boolean(this.options.inFormBuilder)
      );
    }

    /** The subtree to mount. Called again on every change. */
    protected abstract renderReact(): ReactNode;

    /** The markup inside Form.io's wrapper; a subclass may add siblings. */
    protected bcgovMarkup(): string {
      return '<div ref="reactRoot"></div>';
    }

    override render(): string {
      if (this.bcgovStockRendering) return super.render();
      return renderBareWrapper.call(this, this.bcgovMarkup());
    }

    override attach(element: HTMLElement): Promise<void> {
      if (this.bcgovStockRendering) return super.attach(element);
      this.loadRefs(element, { reactRoot: "single" });
      const host = this.refs.reactRoot;
      if (host instanceof HTMLElement) {
        this.bcgovMount = new ReactMount(host, this.renderReact());
      }
      // Safe to chain even though we render no native inputs: the stock
      // classes look them up with querySelectorAll and iterate an empty list.
      return super.attach(element);
    }

    override detach(): void {
      this.bcgovMount?.release();
      this.bcgovMount = undefined;
      super.detach();
    }

    /** Re-render the subtree; the components are controlled. */
    protected bcgovRerender(): void {
      this.bcgovMount?.render(this.renderReact());
    }

    override setValue(
      value: unknown,
      flags?: Record<string, unknown>,
    ): boolean {
      // A cleared field counts as untouched again, so drop any error state it
      // is still carrying. Test the INCOMING value: a base may normalise a
      // clearing null into something that never looks empty afterwards.
      const changed = super.setValue(value, flags);
      if (isEmptyInput(value)) this.bcgovResetErrors();
      this.bcgovRerender();
      return changed;
    }

    protected bcgovResetErrors(): void {
      this.bcgovErrorMessage = "";
      this.setDirty(false);
      this.bcgovErrorsUnlocked = false;
    }

    override setCustomValidity(
      messages: unknown,
      dirty?: boolean,
      external?: boolean,
    ): unknown {
      const result = super.setCustomValidity(messages, dirty, external);
      // A submit attempt unlocks the message. `root.submitting` is set for the
      // duration of the submit's own validation pass, so a change-driven
      // validation on an untouched field stays silent. A message the API sent
      // back unlocks it too: Form.io's own onSubmit resets empty fields (which
      // re-locks them) before the server's errors are applied, and a server
      // error on an empty field must still show.
      if (this.root?.submitting || external || hasServerMessage(messages)) {
        this.bcgovErrorsUnlocked = true;
      }
      this.bcgovErrorMessage =
        dirty && this.bcgovErrorsUnlocked ? firstErrorMessage(messages) : "";
      // This does not redraw, so re-render or the message never shows.
      this.bcgovRerender();
      return result;
    }

    /**
     * Commits a change the citizen made: through Form.io's `updateValue`, then
     * a re-render. A field already showing a message is re-checked at once, so
     * a correction clears it without waiting for a blur Form.io would have
     * listened for on its own input (`validateOn: "blur"`), or for the next
     * submit. Silent before any submit attempt.
     */
    protected bcgovCommit(value: unknown): void {
      this.updateValue(value, { modified: true });
      this.bcgovRecheckIfShowing();
      this.bcgovRerender();
      // A confirmation field's message depends on this one too: fixing the
      // email rather than its verification must clear the verification's
      // mismatch as well.
      this.root?.everyComponent?.((other) => {
        if (
          other !== this &&
          isWrapper(other) &&
          other.bcgovConfirms(this.key)
        ) {
          other.bcgovRecheckIfShowing();
        }
      });
    }

    /** Re-runs this field's rules when it is already showing a message. */
    bcgovRecheckIfShowing(): void {
      if (this.bcgovErrorsUnlocked && this.bcgovErrorMessage !== "") {
        void this.checkComponentValidity(this.rootValue, true);
      }
    }

    /** Whether this field is a confirmation of the field with that key. */
    bcgovConfirms(key: string): boolean {
      const properties = this.component.properties as
        Record<string, unknown> | undefined;
      return properties?.myssMatches === key;
    }

    override addMessages(): void {
      // Intentionally empty: suppress Form.io's own error DOM. The wrapper
      // still contains a message container, which would paint the message a
      // second time below the field. The design system component links its
      // own message to the input for screen readers.
    }

    override validateComponent(
      data?: unknown,
      row?: unknown,
      flags: Record<string, unknown> = {},
    ): FormioError[] | Promise<FormioError[]> {
      const result = super.validateComponent(data, row, flags);
      const failed = this.bcgovRuleFailure();
      if (!failed) return result;
      return isPromise<FormioError[]>(result)
        ? result.then((errors) => this.bcgovAppendRuleError(errors, failed))
        : this.bcgovAppendRuleError(result, failed);
    }

    /** The registry rule or confirmation this field fails, if any. */
    private bcgovRuleFailure(): RuleFailure | null {
      if (!this.visible || this.options.readOnly === true) return null;
      const properties = this.component.properties as
        Record<string, unknown> | undefined;
      const matches = properties?.myssMatches;
      const partner =
        typeof matches === "string" && matches !== ""
          ? this.rootValue[matches]
          : undefined;
      const value = this.dataValue;

      if (isEmptyInput(value) || String(value).trim() === "") {
        // An empty confirmation of a filled partner is a mismatch, not a
        // blank: the typo guard must not be satisfiable by leaving it out.
        return typeof partner === "string" && partner.trim() !== ""
          ? checkMatches("", partner)
          : null;
      }

      const text = String(value);
      const rule = properties?.myssValidator;
      if (typeof rule === "string" && rule !== "") {
        const failed = checkRule(rule, text, {
          data: this.rootValue,
          component: this.component,
        });
        if (failed) return failed;
      }

      return partner !== undefined ? checkMatches(text, partner) : null;
    }

    /**
     * Adds the rule's failure after Form.io's own, in the shape Form.io's
     * error list, alert and `setCustomValidity` read. A field already failing
     * one of Form.io's rules keeps that message; one message per field.
     */
    private bcgovAppendRuleError(
      errors: FormioError[],
      failed: RuleFailure,
    ): FormioError[] {
      if (errors.length > 0) return errors;
      const message = ruleMessage(this.component, failed);
      const path = this.path ?? this.key;
      errors.push({
        level: "error",
        message,
        errorKeyOrMessage: message,
        ruleName: failed.rule,
        keyword: failed.keyword,
        path,
        component: this.component,
        context: {
          component: this.component,
          path,
          value: this.dataValue,
          hasLabel: false,
          field: textOf(this.component.label),
        },
      });
      return errors;
    }

    override focus(): void {
      const host = this.refs.reactRoot;
      if (!(host instanceof HTMLElement)) {
        super.focus();
        return;
      }
      const target = Array.from(
        host.querySelectorAll<HTMLElement>(
          "input, select, textarea, button, [tabindex]",
        ),
      ).find(
        (element) =>
          element.tabIndex >= 0 &&
          !element.hidden &&
          element.getAttribute("type") !== "hidden" &&
          element.getAttribute("aria-hidden") !== "true",
      );
      target?.focus();
    }

    /** The props every design system field takes from the spec and Form.io. */
    protected bcgovFieldProps(): BcgovFieldProps {
      const component = this.component;
      // Labels may carry markup from the form; the design system components
      // take text, so the markup is sanitised away rather than shown raw.
      const label = plainText(textOf(component.label), this.options);
      const hideLabel = component.hideLabel === true || label === "";
      return {
        label: hideLabel ? undefined : label,
        // A field with its label hidden still needs an accessible name: its
        // label if it has one, else its key read as words ("phoneType" as
        // "Phone type"). Never the placeholder, which is a format hint.
        "aria-label": hideLabel ? label || humanizeKey(this.key) : undefined,
        description: textOf(component.description) || undefined,
        isRequired: isRequired(component),
        isDisabled:
          this.disabled ||
          component.disabled === true ||
          this.options.readOnly === true,
        isInvalid: this.bcgovErrorMessage !== "",
        errorMessage: this.bcgovErrorMessage || undefined,
        name: `data[${this.key}]`,
      };
    }
  }

  return BcgovField;
}
