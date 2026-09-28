import { TagGroup, TagList } from "@bcgov/design-system-react-components";

// The status of an application, as text plus colour (never colour alone).
// Wraps the design system's Tag; react-aria requires a Tag to live inside a
// TagList, so the group and list are part of the wrapper.

/**
 * Display text for each status code the API returns. Interim: the handbook has
 * these resolving from the content engine by key, so the code stays the
 * contract and only the wording here is provisional.
 */
const STATUS_LABELS: Record<string, string> = {
  DRAFT: "Draft",
  SUBMITTED: "Submitted",
};

type TagColor = "gray" | "green" | "blue";

const STATUS_COLORS: Record<string, TagColor> = {
  DRAFT: "gray",
  SUBMITTED: "green",
};

/** The display text for a status code; the code itself when none is mapped. */
function statusLabel(status: string): string {
  return STATUS_LABELS[status] ?? status;
}

export default function StatusPill({ status }: { status: string }) {
  const label = statusLabel(status);
  return (
    <TagGroup aria-label="Status">
      <TagList
        items={[
          {
            id: status,
            textValue: label,
            color: STATUS_COLORS[status] ?? "blue",
          },
        ]}
      />
    </TagGroup>
  );
}
