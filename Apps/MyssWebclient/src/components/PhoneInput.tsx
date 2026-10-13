import { TextField } from "@bcgov/design-system-react-components";
import type { ComponentProps } from "react";

type PhoneInputProps = ComponentProps<typeof TextField>;

export default function PhoneInput(props: Readonly<PhoneInputProps>) {
  return (
    <TextField
      {...props}
      type="tel"
      autoComplete={props.autoComplete ?? "tel-national"}
    />
  );
}