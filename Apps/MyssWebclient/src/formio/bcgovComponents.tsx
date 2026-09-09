// Custom Form.io component that renders a real BC Gov Design System React
// component inside a Form.io-rendered form (MYSS-206, Option 3 — see
// doc/MYSS-206-Estimator-Conditional-Display-Plan.md §4.3 / §6 Step 1.5).
//
// Why this exists: the estimator form is rendered by Form.io from a Strapi
// spec, but the designer wants a genuine BC Gov component (not a CSS look-alike)
// for the "What does status mean?" accordion. A custom Form.io component lets
// the seed reference a `type` ("bcgovAccordion") that Form.io renders by
// mounting a React subtree — so the component stays in the form tree (native
// placement, the same `conditional` mechanism as every other field) while being
// the real design-system component.
//
// (The Q2 info tooltip is NOT a custom component: it stays the native Form.io
// `tooltip` property so the icon renders inline inside Q2's label, which a
// separate component cannot do. See the v3 seed note.)
//
// CONTRACT: the seed's custom `bcgovAccordion` type is only meaningful if this
// component is registered (call `registerBcgovComponents()` once at app start,
// before any <Form> mounts). An unregistered type renders a blank slot. MyssApi's
// FormSpecValidator lists it in NonDataTypes so it is treated as non-data
// (never validated, never rejected as an unknown key).

import type { ReactNode } from "react";
import { createRoot, type Root } from "react-dom/client";
import { Components, Utils } from "@formio/js";
import { Accordion } from "@bcgov/design-system-react-components";

// The base Form.io component class. `@formio/js` types the registry loosely, so
// we take the constructor as `any` and keep our subclasses thin.
type FormioComponentCtor = {
  new (...args: unknown[]): FormioComponentInstance;
  schema(...extend: unknown[]): Record<string, unknown>;
};

interface FormioComponentInstance {
  component: Record<string, unknown>;
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

let registered = false;

/**
 * Register the custom BC Gov Form.io components. Idempotent; call once before
 * any <Form> that uses these types renders (e.g. from `main.tsx`).
 */
export function registerBcgovComponents(): void {
  if (registered) return;
  const setComponent = (Components as unknown as {
    setComponent(name: string, comp: unknown): void;
  }).setComponent;
  setComponent("bcgovAccordion", BcgovAccordionComponent);
  registered = true;
}
