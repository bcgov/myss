// Custom Form.io components that mount real BC Gov Design System components, so
// a Strapi-seeded form can use them by `type` and keep Form.io's own placement
// and conditional logic.
//
// Two bases, because one carries an answer and one does not:
//   bcgovAccordion — display only, extends the base component.
//   bcgovRadio     — a data field, extends Form.io's built-in radio to inherit
//                    value handling, validation and clearOnHide.
//
// A referenced type only works if registered: call `registerBcgovComponents()`
// once at app start, before any <Form> mounts. An unregistered type renders a
// blank slot.

import type { ReactNode } from "react";
import { createRoot, type Root } from "react-dom/client";
import { Components, Utils } from "@formio/js";
import {
  Accordion,
  Radio,
  RadioGroup,
} from "@bcgov/design-system-react-components";

// The base Form.io component class. `@formio/js` types the registry loosely, so
// we take the constructor as `any` and keep our subclasses thin.
type FormioComponentCtor = {
  new (...args: unknown[]): FormioComponentInstance;
  schema(...extend: unknown[]): Record<string, unknown>;
};

interface FormioComponentInstance {
  component: Record<string, unknown>;
  /** Render options; Utils.sanitize reads its sanitize config from here. */
  options: Record<string, unknown>;
  refs: Record<string, HTMLElement | undefined>;
  loadRefs(element: HTMLElement, refs: Record<string, string>): void;
  render(children?: string): string;
  attach(element: HTMLElement): Promise<void>;
  detach(): void;
}

const BaseComponent = (
  Components as unknown as { components: { component: FormioComponentCtor } }
).components.component;

/**
 * The plain component wrapper, with no label and no input markup. A subclass of
 * the radio has to reach for this directly: its own `super.render()` emits
 * Form.io's native radio template, and the one above that adds a label the BCDS
 * component already draws.
 */
const renderBareWrapper = (
  BaseComponent as unknown as {
    prototype: { render(children?: string): string };
  }
).prototype.render;

/**
 * A Form.io display component whose body is a React subtree. Subclasses return
 * the node from `renderReact()`; this base handles the mount/unmount lifecycle.
 */
abstract class ReactFormioComponent extends BaseComponent {
  private reactRoot?: Root;

  /** A single container Form.io hands back to us in `attach`. */
  override render(): string {
    return super.render('<div ref="reactRoot"></div>');
  }

  override attach(element: HTMLElement): Promise<void> {
    this.loadRefs(element, { reactRoot: "single" });
    const host = this.refs.reactRoot;
    if (host) {
      this.reactRoot = createRoot(host);
      this.reactRoot.render(this.renderReact());
    }
    return super.attach(element);
  }

  override detach(): void {
    // Defer unmount to avoid React warning about unmounting while rendering.
    const root = this.reactRoot;
    this.reactRoot = undefined;
    if (root) queueMicrotask(() => root.unmount());
    super.detach();
  }

  protected abstract renderReact(): ReactNode;
}

/** The collapsible help. Reads `accordionLabel` + `accordionBody` (HTML). */
class BcgovAccordionComponent extends ReactFormioComponent {
  static schema(...extend: unknown[]): Record<string, unknown> {
    return BaseComponent.schema(
      {
        type: "bcgovAccordion",
        key: "bcgovAccordion",
        input: false,
        accordionLabel: "",
        accordionBody: "",
      },
      ...extend,
    );
  }

  static get builderInfo() {
    return {
      title: "BC Gov Accordion",
      group: "basic",
      icon: "list",
      schema: BcgovAccordionComponent.schema(),
    };
  }

  protected renderReact(): ReactNode {
    const label = String(this.component.accordionLabel ?? "");
    // accordionBody is HTML sourced from the form spec (CMS/Strapi). Sanitize
    // via Form.io's DOMPurify-backed Utils.sanitize before rendering, so spec
    // content can never inject scripts/handlers (defence-in-depth XSS guard).
    const body = Utils.sanitize(
      String(this.component.accordionBody ?? ""),
      this.options ?? {},
    );
    return (
      <Accordion label={label}>
        <div dangerouslySetInnerHTML={{ __html: body }} />
      </Accordion>
    );
  }
}

// bcgovRadio — the BCDS RadioGroup as a Form.io data component. Extends the
// built-in radio so value handling, validation, conditionals and clearOnHide are
// inherited; only the rendering is replaced.

interface FormioRadioInstance extends FormioComponentInstance {
  /** The component's current answer, already coerced by `dataType`. */
  dataValue: unknown;
  updateValue(value: unknown, flags?: Record<string, unknown>): boolean;
  setValue(value: unknown, flags?: Record<string, unknown>): boolean;
  setCustomValidity(
    messages: unknown,
    dirty?: boolean,
    external?: boolean,
  ): unknown;
  /** Renders Form.io's own error DOM into `refs.messageContainer`. */
  addMessages(messages?: unknown): void;
  /** Form.io's "the citizen has interacted with this field" flag. */
  dirty: boolean;
  setDirty(dirty: boolean): void;
  /**
   * The Webform this component belongs to. `submitting` is true only while a
   * submit's own validation pass is running.
   */
  root?: { submitting?: boolean };
}

type FormioRadioCtor = {
  new (...args: unknown[]): FormioRadioInstance;
  schema(...extend: unknown[]): Record<string, unknown>;
};

const RadioBase = (
  Components as unknown as { components: { radio: FormioRadioCtor } }
).components.radio;

/** One `{ label, value }` entry of the component's `values` array. */
interface RadioOption {
  label?: unknown;
  value?: unknown;
  childrenLabel?: unknown;
  children?: RadioOption[];
}

/**
 * Pull the message to show out of whatever `setCustomValidity` was handed: it
 * takes `''` to clear, a bare string, a single `{ message, level }` object, or
 * an array of them. Returns "" when there is nothing to show.
 */
function firstErrorMessage(messages: unknown): string {
  const list = Array.isArray(messages) ? messages : messages ? [messages] : [];
  for (const entry of list) {
    if (typeof entry === "string" && entry) return entry;
    if (entry && typeof entry === "object") {
      const { message, level } = entry as {
        message?: unknown;
        level?: unknown;
      };
      // Form.io also emits 'warning'/'info' levels; only errors get the
      // RadioGroup's danger treatment.
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

class BcgovRadioComponent extends RadioBase {
  private reactRoot?: Root;

  /** Latest Form.io validation message, mirrored into RadioGroup. */
  private errorMessage = "";

  private selectedParent?: string;

  /**
   * Whether this field may display a validation message yet. `dirty` alone is
   * not enough: it stays set for the whole session after a submit, so a
   * conditional field revealed later would show "is required" untouched.
   * Unlocked by a submit attempt, re-locked when the field is cleared.
   */
  private errorsUnlocked = false;

  static schema(...extend: unknown[]): Record<string, unknown> {
    // Keeps the built-in radio's defaults, notably `inputType: "radio"`.
    return RadioBase.schema({ type: "bcgovRadio" }, ...extend);
  }

  static get builderInfo() {
    return {
      title: "BC Gov Radio",
      group: "basic",
      icon: "dot-circle-o",
      schema: BcgovRadioComponent.schema(),
    };
  }

  override render(): string {
    return renderBareWrapper.call(this, '<div ref="reactRoot"></div>');
  }

  override attach(element: HTMLElement): Promise<void> {
    this.loadRefs(element, { reactRoot: "single" });
    const host = this.refs.reactRoot;
    if (host) {
      this.reactRoot = createRoot(host);
      this.renderGroup();
    }
    // Safe to chain even though we render no native inputs: the base looks them
    // up with querySelectorAll, so it iterates an empty list.
    return super.attach(element);
  }

  override detach(): void {
    // Defer unmount to avoid React warning about unmounting while rendering.
    const root = this.reactRoot;
    this.reactRoot = undefined;
    if (root) queueMicrotask(() => root.unmount());
    super.detach();
  }

  override setValue(value: unknown, flags?: Record<string, unknown>): boolean {
    // A cleared field counts as untouched again, so drop any error state it is
    // still carrying. Test the INCOMING value: Form.io clears with
    // `setValue(null)`, and under `dataType: "string"` that normalises to the
    // literal string "null", so `dataValue` never looks empty here.
    const emptying = value === undefined || value === null || value === "";
    const changed = super.setValue(value, flags);
    if (emptying) {
      this.errorMessage = "";
      this.setDirty(false);
      this.errorsUnlocked = false;
    }
    const selectedValue = this.selectedValue();
    this.selectedParent =
      selectedValue === null
        ? undefined
        : (this.parentValue(selectedValue) ?? undefined);
    // The group is controlled, so a programmatic change needs a re-render.
    this.renderGroup();
    return changed;
  }

  override setCustomValidity(
    messages: unknown,
    dirty?: boolean,
    external?: boolean,
  ): unknown {
    const result = super.setCustomValidity(messages, dirty, external);
    // A submit attempt unlocks the message. `root.submitting` is set for the
    // duration of the submit's own validation pass, so a change-driven
    // validation on an untouched field stays silent. Read here rather than
    // from a `submitButton` listener, which would depend on Form.io deferring
    // executeSubmit by a microtask.
    if (this.root?.submitting) this.errorsUnlocked = true;
    this.errorMessage =
      dirty && this.errorsUnlocked ? firstErrorMessage(messages) : "";
    // This does not redraw, so re-render or the message never reaches the group.
    this.renderGroup();
    return result;
  }

  /**
   * Suppress Form.io's own error DOM: the base wrapper still contains a message
   * container, which would paint the message a second time below the group.
   * react-aria links the group to its own message for screen readers.
   */
  override addMessages(): void {}

  private renderGroup(): void {
    this.reactRoot?.render(this.buildGroup());
  }

  private componentOptions(): RadioOption[] {
    return Array.isArray(this.component.values)
      ? (this.component.values as RadioOption[])
      : [];
  }

  private selectedValue(): string | null {
    // null, not "": react-aria reads null as "nothing selected". "null" is here
    // because that is what a cleared field holds — see setValue.
    const raw = this.dataValue;
    if (raw === undefined || raw === null || raw === "" || raw === "null") {
      return null;
    }
    if (
      typeof raw === "string" ||
      typeof raw === "number" ||
      typeof raw === "boolean"
    ) {
      return String(raw);
    }
    return null;
  }

  private buildGroup(): ReactNode {
    const options = this.componentOptions();
    const value = this.selectedValue();
    const hasNestedOptions = options.some(
      (option) => (option.children?.length ?? 0) > 0,
    );
    const selectedParent = hasNestedOptions
      ? (this.selectedParent ?? this.parentValue(value))
      : value;
    const validate = this.component.validate;
    const required =
      validate && typeof validate === "object" && "required" in validate
        ? (validate as { required?: unknown }).required === true
        : false;

    return (
      <RadioGroup
        label={String(this.component.label ?? "")}
        orientation="vertical"
        value={selectedParent}
        isRequired={hasNestedOptions ? required : undefined}
        isInvalid={this.errorMessage !== ""}
        errorMessage={this.errorMessage}
        onChange={(next: string) => {
          const option = options.find(
            (candidate) => String(candidate.value) === next,
          );
          if (!hasNestedOptions) {
            this.updateValue(next, { modified: true });
          } else if (option?.children?.length) {
            this.setValue("", { modified: true });
            this.selectedParent = next;
          } else {
            this.setValue(next, { modified: true });
          }
          this.renderGroup();
        }}
      >
        {options.map((option) => {
          const optionValue = String(option.value ?? "");
          const children = option.children ?? [];
          if (!hasNestedOptions) {
            return (
              <Radio key={optionValue} value={optionValue}>
                {String(option.label ?? "")}
              </Radio>
            );
          }
          const isExpanded =
            selectedParent === optionValue && children.length > 0;

          return (
            <div key={optionValue} data-myss-nested-option={optionValue}>
              <Radio value={optionValue}>{String(option.label ?? "")}</Radio>
              {isExpanded && (
                <RadioGroup
                  aria-label={String(
                    option.childrenLabel ??
                      option.label ??
                      "Additional options",
                  )}
                  orientation="vertical"
                  value={value}
                  isRequired={required}
                  isInvalid={this.errorMessage !== ""}
                  errorMessage={this.errorMessage}
                  onChange={(next: string) => {
                    this.setValue(next, { modified: true });
                    this.renderGroup();
                  }}
                >
                  {children.map((child) => (
                    <div
                      key={String(child.value)}
                      data-myss-nested-option-child={String(child.value)}
                    >
                      <Radio value={String(child.value)}>
                        {String(child.label ?? "")}
                      </Radio>
                    </div>
                  ))}
                </RadioGroup>
              )}
            </div>
          );
        })}
      </RadioGroup>
    );
  }

  private parentValue(value: string | null): string | null {
    if (value === null) return null;
    const parent = this.componentOptions().find((option) =>
      (option.children ?? []).some((child) => String(child.value) === value),
    );
    return parent ? String(parent.value) : value;
  }
}

let registered = false;

/**
 * Register the custom BC Gov Form.io components. Idempotent; call once before
 * any <Form> that uses these types renders (e.g. from `main.tsx`).
 */
export function registerBcgovComponents(): void {
  if (registered) return;
  const setComponent = (
    Components as unknown as {
      setComponent(name: string, comp: unknown): void;
    }
  ).setComponent;
  setComponent("bcgovAccordion", BcgovAccordionComponent);
  setComponent("bcgovRadio", BcgovRadioComponent);
  registered = true;
}
