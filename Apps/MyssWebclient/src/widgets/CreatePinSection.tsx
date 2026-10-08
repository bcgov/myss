import PinField from "@/components/PinField";
import { REGISTRATION_PIN_FIELDS as PIN_FIELDS } from "@/lib/pin";
import styles from "./CreatePinSection.module.css";

// "Create your Personal Identification Number (PIN)" in registration
// (MYSS-258): shown to Basic BCeID users only, below the form and above
// Complete registration. Presentational: RegistrationForm holds the values,
// runs the checks and sends the PIN beside the answers, never inside them.

interface CreatePinSectionProps {
  pin: string;
  confirmation: string;
  onPinChange: (value: string) => void;
  onConfirmationChange: (value: string) => void;
  /** Messages keyed by the field names in REGISTRATION_PIN_FIELDS. */
  errors: Readonly<Record<string, string>>;
}

export default function CreatePinSection({
  pin,
  confirmation,
  onPinChange,
  onConfirmationChange,
  errors,
}: Readonly<CreatePinSectionProps>) {
  return (
    <section aria-labelledby="create-pin" className={styles.section}>
      <h2 id="create-pin">Create your Personal Identification Number (PIN)</h2>
      <p>To protect your identity, create a 4-digit PIN.</p>
      <p>
        Choose a PIN you can remember, you will need it when you apply for
        services and sign documents in My Self Serve.
      </p>
      {/* Layout classes go on a wrapper: a className on a BCDS field replaces
          its own and drops the design system's styling. */}
      <div className={styles.fields}>
        <PinField
          label="Type in a PIN"
          name={`data[${PIN_FIELDS.pin}]`}
          autoComplete="new-password"
          value={pin}
          onChange={onPinChange}
          errorMessage={errors[PIN_FIELDS.pin]}
        />
        <PinField
          label="Re-type in the PIN"
          name={`data[${PIN_FIELDS.confirmation}]`}
          autoComplete="new-password"
          value={confirmation}
          onChange={onConfirmationChange}
          errorMessage={errors[PIN_FIELDS.confirmation]}
        />
      </div>
    </section>
  );
}
