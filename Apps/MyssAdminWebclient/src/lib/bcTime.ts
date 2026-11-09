const PARTS = new Intl.DateTimeFormat("en-CA", {
  timeZone: "America/Vancouver",
  year: "numeric",
  month: "numeric",
  day: "numeric",
  hour: "2-digit",
  minute: "2-digit",
  hourCycle: "h23",
});

// Fixed names: en-GB's short September is "Sept", unlike the other months.
const MONTHS = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

/** A timestamp as BC staff read it, such as "3 Nov 2026 at 10:42". */
export function formatBcDateTime(timestamp: string): string {
  const part = Object.fromEntries(
    PARTS.formatToParts(new Date(timestamp)).map(({ type, value }) => [type, value]),
  );
  return `${Number(part.day)} ${MONTHS[Number(part.month) - 1]} ${part.year} at ${part.hour}:${part.minute}`;
}
