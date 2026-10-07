// The BC Gov Design System as Form.io's components. Each stock Form.io input
// type is re-registered under its own name with a wrapper that keeps the stock
// class's value handling, validation and conditionals and replaces only the
// rendering (see bcgovField.tsx for the shared base). Every published form
// spec, every version and every archived submission therefore renders with
// the design system without a spec, builder or server change.
//
// Display and layout types stay Form.io's own: `content`, `columns` and
// `panel` have no design system equivalent. `bcgovAccordion` is the one
// display component added here; `bcgovRadio` stays registered as a second name
// for the radio so specs published with it keep rendering.
//
// A referenced type only works if registered: call `registerBcgovComponents()`
// once at app start, before any <Form> mounts.

import type { ReactNode } from "react";
import { Components, Utils } from "@formio/js";
import { parseDate, type CalendarDate } from "@internationalized/date";
import {
  Accordion,
  Button,
  DatePicker,
  NumberField,
  Radio,
  RadioGroup,
  Select,
} from "@bcgov/design-system-react-components";

import { BcgovAddressAutocompleteComponent } from "./addressAutocomplete";
import "./bcgovComponents.css";
import CheckboxField from "./CheckboxField";
import {
  ReactMount,
  isRequired,
  stockComponent,
  textOf,
  withBcgovField,
  type FormioFieldCtor,
} from "./bcgovField";
import {
  BcgovEmailComponent,
  BcgovTextAreaComponent,
  BcgovTextFieldComponent,
} from "./bcgovTextField";
import styles from "./bcgovFields.module.css";

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

// ---------------------------------------------------------------------------
// bcgovAccordion: display only, so it extends the base component rather than
// a field and carries no value.

const BaseComponent = stockComponent("component");

/** The collapsible help. Reads `accordionLabel` + `accordionBody` (HTML). */
class BcgovAccordionComponent extends BaseComponent {
  private mount?: ReactMount;

  static schema(...extend: unknown[]): Record<string, unknown> {
    return (
      BaseComponent as unknown as {
        schema(...extend: unknown[]): Record<string, unknown>;
      }
    ).schema(
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

  override render(): string {
    return super.render('<div ref="reactRoot"></div>');
  }

  override attach(element: HTMLElement): Promise<void> {
    this.loadRefs(element, { reactRoot: "single" });
    const host = this.refs.reactRoot;
    if (host instanceof HTMLElement) {
      this.mount = new ReactMount(host, this.renderReact());
    }
    return super.attach(element);
  }

  override detach(): void {
    this.mount?.release();
    super.detach();
  }

  private renderReact(): ReactNode {
    const label = textOf(this.component.accordionLabel);
    // accordionBody is HTML sourced from the form spec (CMS/Strapi). Sanitize
    // via Form.io's DOMPurify-backed Utils.sanitize before rendering, so spec
    // content can never inject scripts/handlers (defence-in-depth XSS guard).
    const body = Utils.sanitize(
      textOf(this.component.accordionBody),
      this.options ?? {},
    );
    return (
      <Accordion label={label}>
        <div dangerouslySetInnerHTML={{ __html: body }} />
      </Accordion>
    );
  }
}

// ---------------------------------------------------------------------------
// number

/** Form.io's `number`, drawn with the design system NumberField. */
class BcgovNumberComponent extends withBcgovField(stockComponent("number")) {
  protected renderReact(): ReactNode {
    const component = this.component;
    const props = this.bcgovFieldProps();
    // A format hint the form put in the placeholder, as the description.
    props.description ??= textOf(component.placeholder) || undefined;
    const raw = this.dataValue;
    const validate = isRecord(component.validate) ? component.validate : {};
    // The spec's own limit if it sets one; otherwise none for a whole number
    // and two (currency) for anything else.
    let decimalLimit = validate.integer === true ? 0 : 2;
    if (typeof component.decimalLimit === "number") {
      decimalLimit = component.decimalLimit;
    }

    return (
      <NumberField
        {...props}
        value={typeof raw === "number" ? raw : Number.NaN}
        formatOptions={{
          maximumFractionDigits: decimalLimit,
          useGrouping: component.delimiter === true,
        }}
        onChange={(next: number) => {
          // Form.io reads null as "take the value from the inputs", and there
          // are none, so a cleared field is sent as '' and normalised to empty.
          this.bcgovCommit(Number.isNaN(next) ? "" : next);
        }}
      />
    );
  }
}

// ---------------------------------------------------------------------------
// select

interface SelectValue {
  label?: unknown;
  value?: unknown;
}

/**
 * Form.io's `select` with a fixed list of values, drawn with the design
 * system Select. A select fed from a URL, a resource, JSON or custom code, or
 * one that takes several values, keeps Form.io's own rendering: the builder
 * only offers the fixed-list kind, and the rest appear in its settings dialog.
 */
class BcgovSelectComponent extends withBcgovField(stockComponent("select")) {
  protected override get bcgovStockRendering(): boolean {
    return super.bcgovStockRendering || !this.bcgovHasFixedValues();
  }

  private bcgovHasFixedValues(): boolean {
    const component = this.component;
    const dataSrc = component.dataSrc ?? "values";
    return (
      dataSrc === "values" &&
      component.multiple !== true &&
      isRecord(component.data) &&
      Array.isArray(component.data.values)
    );
  }

  private bcgovValues(): SelectValue[] {
    const data = this.component.data;
    return isRecord(data) && Array.isArray(data.values)
      ? data.values.filter(isRecord)
      : [];
  }

  override setValue(value: unknown, flags?: Record<string, unknown>): boolean {
    if (this.bcgovStockRendering) return super.setValue(value, flags);
    // The stock setValue drives the Choices.js widget, which is not rendered;
    // only the data path is needed.
    const changed = this.updateValue(value, flags);
    if (value === undefined || value === null || value === "") {
      this.bcgovResetErrors();
    }
    this.bcgovRerender();
    return changed;
  }

  protected renderReact(): ReactNode {
    const component = this.component;
    const props = this.bcgovFieldProps();
    const items = this.bcgovValues().map((option) => ({
      id: textOf(option.value),
      label: textOf(option.label),
    }));
    const selected = textOf(this.dataValue);

    return (
      <Select
        {...props}
        placeholder={textOf(component.placeholder) || undefined}
        items={items}
        selectedKey={selected === "" ? null : selected}
        onSelectionChange={(key) => {
          this.bcgovCommit(key === null ? "" : String(key));
        }}
      />
    );
  }
}

// ---------------------------------------------------------------------------
// checkbox

class BcgovCheckboxComponent extends withBcgovField(
  stockComponent("checkbox"),
) {
  protected renderReact(): ReactNode {
    const props = this.bcgovFieldProps();
    return (
      <CheckboxField
        // The label is HTML sourced from the form spec (a consent checkbox
        // links to the terms it names). Sanitised through Form.io's
        // DOMPurify-backed Utils.sanitize, so spec content can never inject
        // scripts or handlers, the way the accordion body is.
        labelHtml={Utils.sanitize(
          textOf(this.component.label),
          this.options ?? {},
        )}
        checked={this.dataValue === true}
        isRequired={props.isRequired}
        isDisabled={props.isDisabled}
        errorMessage={props.errorMessage}
        name={props.name}
        onChange={(next) => {
          this.bcgovCommit(next);
        }}
      />
    );
  }
}

// ---------------------------------------------------------------------------
// radio (also registered as bcgovRadio for specs published with that name)

/** One `{ label, value }` entry of the component's `values` array. */
interface RadioOption {
  label?: unknown;
  value?: unknown;
  childrenLabel?: unknown;
  children?: RadioOption[];
}

/**
 * Form.io's `radio`, drawn with the design system RadioGroup. An option may
 * carry `children`: choosing it reveals a nested group whose choice is the
 * answer, so a two-level question stays one field.
 */
class BcgovRadioComponent extends withBcgovField(stockComponent("radio")) {
  private selectedParent?: string;

  override setValue(value: unknown, flags?: Record<string, unknown>): boolean {
    const changed = super.setValue(value, flags);
    const selectedValue = this.selectedValue();
    this.selectedParent =
      selectedValue === null
        ? undefined
        : (this.parentValue(selectedValue) ?? undefined);
    this.bcgovRerender();
    return changed;
  }

  private componentOptions(): RadioOption[] {
    return Array.isArray(this.component.values)
      ? (this.component.values as RadioOption[])
      : [];
  }

  private selectedValue(): string | null {
    // null, not "": react-aria reads null as "nothing selected". "null" is here
    // because that is what a cleared field holds under dataType "string".
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

  protected renderReact(): ReactNode {
    const props = this.bcgovFieldProps();
    const options = this.componentOptions();
    const value = this.selectedValue();
    const hasNestedOptions = options.some(
      (option) => (option.children?.length ?? 0) > 0,
    );
    const selectedParent = hasNestedOptions
      ? (this.selectedParent ?? this.parentValue(value))
      : value;
    const required = isRequired(this.component);
    const labelId = `${this.id}-label`;

    // The label is drawn here rather than through RadioGroup's `label` prop.
    // BCDS appends "(required)" to that label as bare text whenever
    // `isRequired` is set, which a page cannot style or hide. Owning it lets
    // every group report `isRequired` (so assistive technology announces the
    // field as required) while a page that marks required fields another way
    // hides the `[data-myss-required]` text visually. Same BCDS class, so it
    // looks the same. Every other prop comes from the shared field props
    // (required, disabled, invalid, error, name), as for every other field.
    const { label, ...groupProps } = props;

    return (
      <div className="myss-radio-field">
        {label && (
          <span id={labelId} className="bcds-react-aria-RadioGroup--label">
            {label}
            {required && <span data-myss-required=""> (required)</span>}
          </span>
        )}
        <RadioGroup
          {...groupProps}
          aria-labelledby={label ? labelId : undefined}
          orientation="vertical"
          value={selectedParent}
          onChange={(next: string) => {
            const option = options.find(
              (candidate) => String(candidate.value) === next,
            );
            if (!hasNestedOptions) {
              this.updateValue(next, { modified: true });
            } else if (option?.children?.length) {
              this.updateValue("", { modified: true });
              this.selectedParent = next;
            } else {
              this.selectedParent = next;
              this.updateValue(next, { modified: true });
            }
            this.bcgovRerender();
          }}
        >
          {options.map((option) => {
            const optionValue = textOf(option.value);
            const children = option.children ?? [];
            if (!hasNestedOptions) {
              return (
                <Radio key={optionValue} value={optionValue}>
                  {textOf(option.label)}
                </Radio>
              );
            }
            const isExpanded =
              selectedParent === optionValue && children.length > 0;

            return (
              <div key={optionValue} data-myss-nested-option={optionValue}>
                <Radio value={optionValue}>{textOf(option.label)}</Radio>
                {isExpanded && (
                  <RadioGroup
                    aria-label={textOf(
                      option.childrenLabel,
                      textOf(option.label, "Additional options"),
                    )}
                    orientation="vertical"
                    value={value}
                    isRequired={required}
                    isDisabled={props.isDisabled}
                    onChange={(next: string) => {
                      this.bcgovCommit(next);
                    }}
                  >
                    {children.map((child) => (
                      <div
                        key={String(child.value)}
                        data-myss-nested-option-child={String(child.value)}
                      >
                        <Radio value={String(child.value)}>
                          {textOf(child.label)}
                        </Radio>
                      </div>
                    ))}
                  </RadioGroup>
                )}
              </div>
            );
          })}
        </RadioGroup>
      </div>
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

// ---------------------------------------------------------------------------
// datetime

/** The date portion of a stored value as a calendar date, or null. */
function calendarDateOf(value: unknown): CalendarDate | null {
  const match = /^(\d{4}-\d{2}-\d{2})/.exec(textOf(value));
  if (!match) return null;
  try {
    return parseDate(match[1]);
  } catch {
    return null;
  }
}

/**
 * Form.io's `datetime`, drawn with the design system DatePicker. The answer is
 * stored as the ISO date (`yyyy-MM-dd`), which the API's date parsing accepts
 * alongside the datetime strings Form.io's own picker used to send.
 */
class BcgovDateTimeComponent extends withBcgovField(
  stockComponent("datetime"),
) {
  protected renderReact(): ReactNode {
    const props = this.bcgovFieldProps();
    return (
      <DatePicker
        {...props}
        value={calendarDateOf(this.dataValue)}
        onChange={(next) => {
          this.bcgovCommit(next ? next.toString() : "");
        }}
      />
    );
  }
}

// ---------------------------------------------------------------------------
// button

const BUTTON_STATE_EVENTS = [
  "submitButton",
  "cancelSubmit",
  "submitDone",
  "submitError",
  "change",
  "error",
] as const;

/**
 * Form.io's `button`, drawn with the design system Button. The stock class
 * still owns the submit flow: the click calls its `onClick`, and the events it
 * handles to disable and re-enable itself re-render the Button afterwards.
 */
class BcgovButtonComponent extends withBcgovField(stockComponent("button")) {
  override attach(element: HTMLElement): Promise<void> {
    const attached = super.attach(element);
    if (!this.bcgovStockRendering) {
      // Registered after the stock handlers (which super.attach adds), so each
      // re-render sees the disabled state they just set.
      for (const event of BUTTON_STATE_EVENTS) {
        this.on(event, () => this.bcgovRerender(), true);
      }
    }
    return attached;
  }

  protected renderReact(): ReactNode {
    const component = this.component;
    const action = textOf(component.action) || "submit";
    return (
      <Button
        variant={action === "submit" ? "primary" : "secondary"}
        type="button"
        isDisabled={this.disabled || this.options.readOnly === true}
        className={styles.submitButton}
        onPress={() => this.bcgovClick()}
      >
        {textOf(component.label) || this.t("submit")}
      </Button>
    );
  }

  private bcgovClick(): void {
    // The stock handler expects a DOM event only to stop its propagation.
    (this as unknown as { onClick(event: unknown): void }).onClick({
      preventDefault() {},
      stopPropagation() {},
    });
  }
}

// ---------------------------------------------------------------------------

let registered = false;

/**
 * Register the design system renderers for Form.io's stock types and the BC
 * Gov custom types. Idempotent; call once before any <Form> renders (from
 * `main.tsx`).
 */
export function registerBcgovComponents(): void {
  if (registered) return;
  const setComponent = (
    Components as unknown as {
      setComponent(name: string, comp: FormioFieldCtor | unknown): void;
    }
  ).setComponent;

  setComponent("textfield", BcgovTextFieldComponent);
  setComponent("email", BcgovEmailComponent);
  setComponent("textarea", BcgovTextAreaComponent);
  setComponent("number", BcgovNumberComponent);
  setComponent("select", BcgovSelectComponent);
  setComponent("checkbox", BcgovCheckboxComponent);
  setComponent("radio", BcgovRadioComponent);
  setComponent("bcgovRadio", BcgovRadioComponent);
  setComponent("datetime", BcgovDateTimeComponent);
  setComponent("button", BcgovButtonComponent);
  setComponent("bcgovAccordion", BcgovAccordionComponent);
  setComponent("bcgovAddressAutocomplete", BcgovAddressAutocompleteComponent);
  registered = true;
}
