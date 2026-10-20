import { TagGroup, TagList } from "@bcgov/design-system-react-components";

// The status of an application, as text plus colour (never colour alone).
// Wraps the design system's Tag; react-aria requires a Tag to live inside a
// TagList, so the group and list are part of the wrapper.

/**
 * Display text for each status code the API returns. Interim: the handbook has
 * these resolving from the content engine by key, so the code stays the
 * contract and only the wording here is provisional. ESCALATED reads "Review",
 * the review story's label for an escalated file.
 */
const STATUS_LABELS: Record<string, string> = {
  DRAFT: "Draft",
  SUBMITTED: "Submitted",
  UNDER_REVIEW: "Under Review",
  RETURNED: "Returned",
  RESUBMITTED: "Re-submitted",
  ESCALATED: "Review",
  ACCEPTED: "Accepted",
  DENIED: "Denied",
  PROMOTION_FAILED: "Promotion Failed",
};

type TagColor = "gray" | "green" | "blue" | "yellow" | "red";

const STATUS_COLORS: Record<string, TagColor> = {
  DRAFT: "gray",
  SUBMITTED: "blue",
  UNDER_REVIEW: "yellow",
  RETURNED: "yellow",
  RESUBMITTED: "blue",
  ESCALATED: "yellow",
  ACCEPTED: "green",
  DENIED: "red",
  PROMOTION_FAILED: "red",
};

/**
 * The display text for a status code; the code itself when none is mapped.
 * A file under review names its assignee when one is given ("Under Review -
 * IDIR"), which only the review views pass (now on the admin site, which
 * keeps its own copy of this pill).
 */
function statusLabel(status: string, assignee?: string | null): string {
  const label = STATUS_LABELS[status] ?? status;
  return status === "UNDER_REVIEW" && assignee
    ? `${label} - ${assignee}`
    : label;
}

export default function StatusPill({
  status,
  assignee,
}: Readonly<{ status: string; assignee?: string | null }>) {
  const label = statusLabel(status, assignee);
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
