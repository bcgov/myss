import { DatePicker } from "@bcgov/design-system-react-components";
import type { CalendarDate } from "@internationalized/date";
import type { ComponentProps } from "react";

type DateInputProps = ComponentProps<typeof DatePicker<CalendarDate>>;

/** The BC Gov date picker with segmented entry and an optional calendar. */
export default function DateInput(props: Readonly<DateInputProps>) {
  return <DatePicker {...props} validationBehavior="aria" />;
}
