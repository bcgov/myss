import {
  findCanadaPostAddresses,
  retrieveCanadaPostAddress,
  type CanadaPostAddress,
  type CanadaPostAddressSuggestion,
} from "@/api/canadaPostAddress";
import { CANADA_POST_API_KEY } from "@/constants";

import { AddressListbox } from "./addressListbox";
import { stockComponent } from "./bcgovField";
import { BcgovTextFieldComponent } from "./bcgovTextField";

const SEARCH_DELAY_MS = 250;

/** The sibling fields a retrieved address fills; line 1 is this component. */
const ADDRESS_PARTS = ["line2", "city", "province", "postalCode"] as const;
type AddressPart = (typeof ADDRESS_PARTS)[number];

interface FormioTargetComponent {
  readonly dataValue: unknown;
  setValue(value: unknown, flags?: Record<string, unknown>): boolean;
}

type AddressTargets = Record<AddressPart, FormioTargetComponent>;

function addressParts(address: CanadaPostAddress): Record<AddressPart, string> {
  return {
    line2: address.line2,
    city: address.city,
    province: address.provinceCode,
    postalCode: address.postalCode,
  };
}

/**
 * The design system text field enhanced with Canada Post AddressComplete
 * suggestions. The field's input remains the source of truth, preserving
 * Form.io's required/pattern validation and allowing ordinary manual entry.
 * The suggestion list lives in a sibling of the React root, so React never
 * sees DOM it did not render.
 */
export class BcgovAddressAutocompleteComponent extends BcgovTextFieldComponent {
  private input?: HTMLInputElement;
  private listbox?: AddressListbox;
  private searchTimer?: number;
  private request?: AbortController;

  static schema(...extend: unknown[]): Record<string, unknown> {
    const TextFieldBase = stockComponent("textfield") as unknown as {
      schema(...extend: unknown[]): Record<string, unknown>;
    };
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

  protected override bcgovMarkup(): string {
    return '<div ref="reactRoot"></div><div ref="addressList"></div>';
  }

  override async attach(element: HTMLElement): Promise<void> {
    await super.attach(element);
    if (this.bcgovStockRendering) return;

    this.loadRefs(element, { addressList: "single" });
    const host = this.refs.reactRoot;
    const listHost = this.refs.addressList;
    const input =
      host instanceof HTMLElement ? host.querySelector("input") : null;
    if (
      !(input instanceof HTMLInputElement) ||
      !(listHost instanceof HTMLElement) ||
      input.disabled ||
      input.readOnly
    ) {
      return;
    }
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
      host: listHost,
      listId: `${input.id || String(this.component.key)}-address-suggestions`,
      onChoose: (suggestion) => void this.choose(suggestion),
      onDismiss: () => {
        this.cancelPending();
        this.listbox?.announce("");
      },
    });
    input.addEventListener("input", this.handleInput);
    input.addEventListener("blur", this.handleBlur);
  }

  override detach(): void {
    this.cancelPending();
    this.input?.removeEventListener("input", this.handleInput);
    this.input?.removeEventListener("blur", this.handleBlur);
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
      Partial<Record<AddressPart, unknown>> | undefined;
    const root = this.root as
      | { getComponent?(key: string): FormioTargetComponent | undefined }
      | null
      | undefined;
    const targets: Partial<AddressTargets> = {};
    for (const part of ADDRESS_PARTS) {
      const key = keys?.[part];
      const target =
        typeof key === "string" ? root?.getComponent?.(key) : undefined;
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
