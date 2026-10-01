import { Components } from "@formio/js";

import {
  findCanadaPostAddresses,
  retrieveCanadaPostAddress,
  type CanadaPostAddress,
  type CanadaPostAddressSuggestion,
} from "@/api/canadaPostAddress";
import { CANADA_POST_API_KEY } from "@/constants";

import { AddressListbox } from "./addressListbox";

const SEARCH_DELAY_MS = 250;

/** The sibling fields a retrieved address fills; line 1 is this component. */
const ADDRESS_PARTS = ["line2", "city", "province", "postalCode"] as const;
type AddressPart = (typeof ADDRESS_PARTS)[number];

interface FormioTargetComponent {
  readonly dataValue: unknown;
  setValue(value: unknown, flags?: Record<string, unknown>): boolean;
}

type AddressTargets = Record<AddressPart, FormioTargetComponent>;

interface FormioTextFieldInstance {
  component: Record<string, unknown>;
  refs: Record<string, unknown>;
  root?: { getComponent(key: string): FormioTargetComponent | undefined };
  attach(element: HTMLElement): Promise<void>;
  detach(): void;
  setValue(value: unknown, flags?: Record<string, unknown>): boolean;
  addEventListener(
    element: EventTarget,
    type: string,
    listener: EventListener,
  ): void;
}

type FormioTextFieldCtor = {
  new (...args: unknown[]): FormioTextFieldInstance;
  schema(...extend: unknown[]): Record<string, unknown>;
};

const TextFieldBase = (
  Components as unknown as {
    components: { textfield: FormioTextFieldCtor };
  }
).components.textfield;

function addressParts(address: CanadaPostAddress): Record<AddressPart, string> {
  return {
    line2: address.line2,
    city: address.city,
    province: address.provinceCode,
    postalCode: address.postalCode,
  };
}

function firstInput(ref: unknown): HTMLInputElement | undefined {
  if (ref instanceof HTMLInputElement) return ref;
  if (typeof ref !== "object" || ref === null) return undefined;

  const first = (ref as { readonly 0?: unknown })[0];
  return first instanceof HTMLInputElement ? first : undefined;
}

/**
 * A Form.io text field enhanced with Canada Post AddressComplete suggestions.
 * The native text input remains the source of truth, preserving Form.io's
 * required/pattern validation and allowing ordinary manual entry.
 */
export class BcgovAddressAutocompleteComponent extends TextFieldBase {
  private input?: HTMLInputElement;
  private listbox?: AddressListbox;
  private searchTimer?: number;
  private request?: AbortController;

  static schema(...extend: unknown[]): Record<string, unknown> {
    return TextFieldBase.schema(
      {
        type: "bcgovAddressAutocomplete",
        addressFields: {
          line2: "",
          city: "",
          province: "",
          postalCode: "",
        },
      },
      ...extend,
    );
  }

  static get builderInfo() {
    return {
      title: "BC Gov Address Autocomplete",
      group: "basic",
      icon: "home",
      schema: BcgovAddressAutocompleteComponent.schema(),
    };
  }

  override async attach(element: HTMLElement): Promise<void> {
    await super.attach(element);

    const input = firstInput(this.refs.input);
    const host = input?.parentElement;
    if (!input || !host || input.disabled || input.readOnly) return;
    if (!this.addressTargets()) {
      // A spec error: stay a plain text field rather than half-fill an address.
      console.error(
        `bcgovAddressAutocomplete "${String(this.component.key)}" has an invalid addressFields mapping.`,
      );
      return;
    }

    this.input = input;
    this.listbox = new AddressListbox({
      input,
      host,
      listId: `${input.id || String(this.component.key)}-address-suggestions`,
      onChoose: (suggestion) => void this.choose(suggestion),
    });
    this.addEventListener(input, "input", this.handleInput);
    this.addEventListener(input, "blur", this.handleBlur);
  }

  override detach(): void {
    this.cancelPending();
    this.listbox?.destroy();
    this.listbox = undefined;
    this.input = undefined;
    super.detach();
  }

  private readonly handleInput: EventListener = () => {
    this.cancelPending();
    this.listbox?.close();

    const term = this.input?.value.trim() ?? "";
    if (!term) {
      this.listbox?.announce("");
      return;
    }
    if (!CANADA_POST_API_KEY) {
      this.listbox?.announce(
        "Address suggestions are not configured. Enter the address manually.",
        true,
      );
      return;
    }

    this.listbox?.announce("Searching for addresses.");
    this.searchTimer = window.setTimeout(
      () => void this.search(term),
      SEARCH_DELAY_MS,
    );
  };

  // An in-flight search is left to `search`, which ignores it once unfocused.
  private readonly handleBlur: EventListener = () => {
    window.clearTimeout(this.searchTimer);
    this.searchTimer = undefined;
  };

  private async search(term: string, container?: string): Promise<void> {
    const signal = this.beginRequest();
    let suggestions: CanadaPostAddressSuggestion[];
    try {
      suggestions = await findCanadaPostAddresses({
        searchTerm: term,
        lastId: container,
        signal,
      });
    } catch {
      if (signal.aborted) return;
      this.listbox?.announce(
        "Address suggestions are unavailable. Enter the address manually.",
        true,
      );
      return;
    }

    if (signal.aborted) return;
    // Results landing after the user left the field must not reopen the list.
    if (document.activeElement !== this.input) {
      this.listbox?.announce("");
    } else if (suggestions.length === 0) {
      this.listbox?.announce(
        "No address suggestions found. Continue entering the address manually.",
      );
    } else {
      this.listbox?.show(suggestions);
    }
  }

  private async choose(suggestion: CanadaPostAddressSuggestion): Promise<void> {
    this.listbox?.close();
    if (suggestion.next === "Find") {
      this.listbox?.announce("Loading more address options.");
      await this.search(this.input?.value ?? "", suggestion.id);
      return;
    }

    const targets = this.addressTargets();
    if (!targets) return;
    this.listbox?.announce("Loading the selected address.");
    const before = ADDRESS_PARTS.map((part) => targets[part].dataValue);
    const signal = this.beginRequest();
    let address: CanadaPostAddress;
    try {
      address = await retrieveCanadaPostAddress({ id: suggestion.id, signal });
    } catch {
      if (signal.aborted) return;
      this.listbox?.announce(
        "The selected address could not be loaded. Enter the address manually.",
        true,
      );
      return;
    }

    if (signal.aborted) return;
    // Never overwrite what the user typed into the address while this loaded.
    if (
      ADDRESS_PARTS.some((part, i) => targets[part].dataValue !== before[i])
    ) {
      this.listbox?.announce(
        "Your address changes were kept. Choose a suggestion again to replace them.",
        true,
      );
      return;
    }

    const parts = addressParts(address);
    for (const part of ADDRESS_PARTS) {
      targets[part].setValue(parts[part], { noUpdateEvent: true });
    }
    this.setValue(address.line1, { modified: true });
    this.listbox?.announce("Address selected.");
    this.input?.focus();
  }

  /** The sibling components named by `addressFields`; null if any is missing. */
  private addressTargets(): AddressTargets | null {
    const keys = this.component.addressFields as
      | Partial<Record<AddressPart, unknown>>
      | undefined;
    const targets: Partial<AddressTargets> = {};
    for (const part of ADDRESS_PARTS) {
      const key = keys?.[part];
      const target =
        typeof key === "string" ? this.root?.getComponent(key) : undefined;
      if (!target) return null;
      targets[part] = target;
    }
    return targets as AddressTargets;
  }

  private beginRequest(): AbortSignal {
    this.cancelPending();
    this.request = new AbortController();
    return this.request.signal;
  }

  private cancelPending(): void {
    window.clearTimeout(this.searchTimer);
    this.searchTimer = undefined;
    this.request?.abort();
    this.request = undefined;
  }
}
