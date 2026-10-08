import { TextField } from "@bcgov/design-system-react-components";

// One 4-digit PIN input (MYSS-258), the same at registration and on Account
// Info: masked, a numeric keypad on a phone, and no more than four characters.
// The checks themselves are in lib/pin; this only collects the digits.

interface PinFieldProps {
  label: string;
  /** Also the input's name, which the error summaries use to find it. */
  name: string;
  value: string;
  onChange: (value: string) => void;
  errorMessage?: string;
  /** "new-password" for a PIN being chosen, "current-password" for one being proven. */
  autoComplete: "new-password" | "current-password";
}

export default function PinField({
  label,
  name,
  value,
  onChange,
  errorMessage,
  autoComplete,
}: Readonly<PinFieldProps>) {
  return (
    <TextField
      label={label}
      name={name}
      type="password"
      inputMode="numeric"
      maxLength={4}
      autoComplete={autoComplete}
      isRequired
      // The errors are ours (lib/pin and the API), not the browser's.
      validationBehavior="aria"
      value={value}
      onChange={onChange}
      isInvalid={Boolean(errorMessage)}
      errorMessage={errorMessage}
    />
  );
}
