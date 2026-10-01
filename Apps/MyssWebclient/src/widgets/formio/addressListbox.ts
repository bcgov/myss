import type { CanadaPostAddressSuggestion } from "@/api/canadaPostAddress";

import "./addressAutocomplete.css";

const HOST_CLASS = "myss-address-autocomplete";
const HIDDEN_STATUS_CLASS = "myss-address-autocomplete__status--hidden";
const COMBOBOX_ATTRIBUTES = [
  "role",
  "aria-autocomplete",
  "aria-controls",
  "aria-expanded",
  "aria-activedescendant",
] as const;

interface AddressListboxOptions {
  readonly input: HTMLInputElement;
  readonly host: HTMLElement;
  readonly listId: string;
  readonly onChoose: (suggestion: CanadaPostAddressSuggestion) => void;
  /** Escape: the owner must cancel pending searches, or they reopen the list. */
  readonly onDismiss: () => void;
}

/**
 * The suggestion popup and live status that make a text input an ARIA
 * list-autocomplete combobox. Knows nothing about Form.io or Canada Post
 * requests; `destroy()` removes everything it added.
 */
export class AddressListbox {
  private readonly input: HTMLInputElement;
  private readonly host: HTMLElement;
  private readonly onChoose: AddressListboxOptions["onChoose"];
  private readonly onDismiss: AddressListboxOptions["onDismiss"];
  private readonly list = document.createElement("ul");
  private readonly status = document.createElement("p");
  private readonly listeners = new AbortController();
  private suggestions: readonly CanadaPostAddressSuggestion[] = [];
  private activeIndex = -1;

  constructor({
    input,
    host,
    listId,
    onChoose,
    onDismiss,
  }: AddressListboxOptions) {
    this.input = input;
    this.host = host;
    this.onChoose = onChoose;
    this.onDismiss = onDismiss;

    this.list.id = listId;
    this.list.className = "myss-address-autocomplete__list";
    this.list.setAttribute("role", "listbox");
    this.list.hidden = true;

    this.status.className = `myss-address-autocomplete__status ${HIDDEN_STATUS_CLASS}`;
    this.status.setAttribute("role", "status");
    this.status.setAttribute("aria-live", "polite");

    host.classList.add(HOST_CLASS);
    host.append(this.list, this.status);
    input.setAttribute("role", "combobox");
    input.setAttribute("aria-autocomplete", "list");
    input.setAttribute("aria-controls", listId);
    input.setAttribute("aria-expanded", "false");

    const { signal } = this.listeners;
    input.addEventListener("keydown", this.handleKeyDown, { signal });
    input.addEventListener(
      "blur",
      () => window.setTimeout(() => this.close(), 100),
      { signal },
    );
    // Keeps focus in the input, so choosing an option never blurs it.
    this.list.addEventListener(
      "pointerdown",
      (event) => event.preventDefault(),
      { signal },
    );
    this.list.addEventListener("click", this.handleClick, { signal });
  }

  show(suggestions: readonly CanadaPostAddressSuggestion[]): void {
    this.suggestions = suggestions;
    this.activeIndex = -1;
    this.list.replaceChildren(
      ...suggestions.map((suggestion, index) =>
        this.renderOption(suggestion, index),
      ),
    );
    this.list.hidden = false;
    this.input.setAttribute("aria-expanded", "true");
    this.announce(
      `${suggestions.length} address option${suggestions.length === 1 ? "" : "s"} available.`,
    );
  }

  close(): void {
    this.suggestions = [];
    this.activeIndex = -1;
    this.list.hidden = true;
    this.list.replaceChildren();
    this.input.setAttribute("aria-expanded", "false");
    this.input.removeAttribute("aria-activedescendant");
  }

  /** Screen-reader status; `visible` also shows it for sighted users. */
  announce(message: string, visible = false): void {
    this.status.textContent = message;
    this.status.classList.toggle(HIDDEN_STATUS_CLASS, !visible);
  }

  destroy(): void {
    this.listeners.abort();
    this.list.remove();
    this.status.remove();
    this.host.classList.remove(HOST_CLASS);
    for (const attribute of COMBOBOX_ATTRIBUTES) {
      this.input.removeAttribute(attribute);
    }
  }

  private renderOption(
    suggestion: CanadaPostAddressSuggestion,
    index: number,
  ): HTMLLIElement {
    const option = document.createElement("li");
    option.id = `${this.list.id}-option-${index}`;
    option.className = "myss-address-autocomplete__option";
    option.dataset.addressIndex = String(index);
    option.setAttribute("role", "option");
    option.setAttribute("aria-selected", "false");

    const text = document.createElement("span");
    text.className = "myss-address-autocomplete__option-text";
    text.textContent = suggestion.text;
    option.append(text);

    if (suggestion.description) {
      const description = document.createElement("span");
      description.className = "myss-address-autocomplete__option-description";
      description.textContent = suggestion.description;
      option.append(description);
    }
    return option;
  }

  private readonly handleKeyDown = (event: KeyboardEvent): void => {
    const count = this.suggestions.length;
    if ((event.key === "ArrowDown" || event.key === "ArrowUp") && count > 0) {
      event.preventDefault();
      const step = event.key === "ArrowDown" ? 1 : -1;
      const from =
        this.activeIndex === -1 && step === -1 ? 0 : this.activeIndex;
      this.setActive((from + step + count) % count);
    } else if (event.key === "Enter" && this.suggestions[this.activeIndex]) {
      event.preventDefault();
      this.onChoose(this.suggestions[this.activeIndex]);
    } else if (event.key === "Escape") {
      this.close();
      this.onDismiss();
    }
  };

  private readonly handleClick = (event: MouseEvent): void => {
    if (!(event.target instanceof Element)) return;
    const option = event.target.closest<HTMLElement>("[data-address-index]");
    const suggestion = option
      ? this.suggestions[Number(option.dataset.addressIndex)]
      : undefined;
    if (suggestion) this.onChoose(suggestion);
  };

  private setActive(index: number): void {
    this.activeIndex = index;
    const options = this.list.querySelectorAll<HTMLElement>('[role="option"]');
    options.forEach((option, optionIndex) => {
      option.setAttribute("aria-selected", String(optionIndex === index));
    });

    const active = options[index];
    if (active) {
      this.input.setAttribute("aria-activedescendant", active.id);
      active.scrollIntoView({ block: "nearest" });
    }
  }
}
