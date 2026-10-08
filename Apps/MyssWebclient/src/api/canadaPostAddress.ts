import { CANADA_POST_API_KEY } from "@/constants";

const FIND_URL =
  "https://ws1.postescanada-canadapost.ca/AddressComplete/Interactive/Find/v2.10/json3.ws";
const RETRIEVE_URL =
  "https://ws1.postescanada-canadapost.ca/AddressComplete/Interactive/Retrieve/v2.11/json3.ws";

type CanadaPostNextStep = "Find" | "Retrieve";

export interface CanadaPostAddressSuggestion {
  readonly id: string;
  readonly text: string;
  readonly description: string;
  readonly next: CanadaPostNextStep;
}

export interface CanadaPostAddress {
  readonly line1: string;
  readonly line2: string;
  readonly city: string;
  readonly provinceCode: string;
  readonly postalCode: string;
}

export class CanadaPostAddressError extends Error {
  readonly code?: string;
  readonly causeDescription?: string;
  readonly resolution?: string;

  constructor(
    message: string,
    options?: {
      code?: string;
      causeDescription?: string;
      resolution?: string;
    },
  ) {
    super(message);
    this.name = "CanadaPostAddressError";
    this.code = options?.code;
    this.causeDescription = options?.causeDescription;
    this.resolution = options?.resolution;
    Object.setPrototypeOf(this, CanadaPostAddressError.prototype);
  }
}

interface FindOptions {
  readonly searchTerm: string;
  readonly lastId?: string;
  readonly apiKey?: string;
  readonly signal?: AbortSignal;
}

interface RetrieveOptions {
  readonly id: string;
  readonly apiKey?: string;
  readonly signal?: AbortSignal;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function itemString(
  item: Record<string, unknown>,
  field: string,
  required = true,
): string {
  const value = item[field];
  if (typeof value === "string") {
    if (!required || value.length > 0) return value;
  } else if (typeof value === "number") {
    return String(value);
  }

  if (!required) return "";
  throw new CanadaPostAddressError(
    `Canada Post returned an address without ${field}.`,
  );
}

async function readItems(
  response: Response,
  operation: "search" | "retrieve",
): Promise<Record<string, unknown>[]> {
  if (!response.ok) {
    throw new CanadaPostAddressError(
      `Canada Post address ${operation} failed (${response.status}).`,
    );
  }

  let body: unknown;
  try {
    body = await response.json();
  } catch {
    throw new CanadaPostAddressError(
      `Canada Post address ${operation} returned invalid JSON.`,
    );
  }

  if (!isRecord(body) || !Array.isArray(body.Items)) {
    throw new CanadaPostAddressError(
      `Canada Post address ${operation} returned an invalid response.`,
    );
  }

  const items = body.Items.filter(isRecord);
  const first = items[0];
  if (first?.Error !== undefined && first?.Error !== "") {
    const description = itemString(first, "Description", false);
    throw new CanadaPostAddressError(
      description || `Canada Post address ${operation} failed.`,
      {
        code: itemString(first, "Error", false),
        causeDescription: itemString(first, "Cause", false),
        resolution: itemString(first, "Resolution", false),
      },
    );
  }

  return items;
}

function requiredApiKey(apiKey: string | undefined): string {
  const key = apiKey?.trim();
  if (!key) {
    throw new CanadaPostAddressError(
      "Canada Post address suggestions are not configured.",
    );
  }
  return key;
}

export async function findCanadaPostAddresses({
  searchTerm,
  lastId,
  apiKey = CANADA_POST_API_KEY,
  signal,
}: FindOptions): Promise<CanadaPostAddressSuggestion[]> {
  const term = searchTerm.trim();
  if (!term && !lastId) return [];

  const params = new URLSearchParams({
    Key: requiredApiKey(apiKey),
    SearchTerm: term,
    Country: "CAN",
    LanguagePreference: "en",
    MaxSuggestions: "7",
  });
  if (lastId) params.set("LastId", lastId);

  const response = await fetch(`${FIND_URL}?${params}`, { signal });
  const items = await readItems(response, "search");

  return items.map((item) => {
    const next = itemString(item, "Next");
    if (next !== "Find" && next !== "Retrieve") {
      throw new CanadaPostAddressError(
        `Canada Post returned an unsupported next step: ${next}.`,
      );
    }

    return {
      id: itemString(item, "Id"),
      text: itemString(item, "Text"),
      description: itemString(item, "Description", false),
      next,
    };
  });
}

export async function retrieveCanadaPostAddress({
  id,
  apiKey = CANADA_POST_API_KEY,
  signal,
}: RetrieveOptions): Promise<CanadaPostAddress> {
  const params = new URLSearchParams({
    Key: requiredApiKey(apiKey),
    Id: id,
  });

  const response = await fetch(`${RETRIEVE_URL}?${params}`, { signal });
  const items = await readItems(response, "retrieve");
  const english = items.find(
    (item) => itemString(item, "Language", false).toUpperCase() === "ENG",
  );

  if (!english) {
    throw new CanadaPostAddressError(
      "Canada Post did not return an English address.",
    );
  }

  return {
    line1: itemString(english, "Line1"),
    line2: itemString(english, "Line2", false),
    city: itemString(english, "City"),
    provinceCode: itemString(english, "ProvinceCode"),
    postalCode: itemString(english, "PostalCode"),
  };
}
