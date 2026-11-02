import styles from "./RegistrationSteps.module.css";

// Where the citizen is in creating their account (MYSS-295): the sign-in,
// done before the form opens, then the form's own steps. An ordered list, the
// current step marked with aria-current, and each state said in words as well
// as drawn, so it does not rest on the icon or its colour.

export interface RegistrationStep {
  /** "Step 1:" and so on; absent for the sign-in, which has no number. */
  prefix?: string;
  label: string;
}

type Status = "complete" | "current" | "upcoming";

const STATUS_TEXT: Readonly<Record<Status, string>> = {
  complete: "completed",
  current: "current step",
  upcoming: "not started",
};

function Marker({ status }: Readonly<{ status: Status }>) {
  if (status === "complete") {
    return (
      <svg
        className={styles.marker}
        viewBox="0 0 24 24"
        aria-hidden="true"
        focusable="false"
      >
        <circle cx="12" cy="12" r="10.5" className={styles.completeRing} />
        <path d="M7 12.5l3.2 3.2L17 9" className={styles.check} />
      </svg>
    );
  }
  return (
    <svg
      className={styles.marker}
      viewBox="0 0 24 24"
      aria-hidden="true"
      focusable="false"
    >
      <circle
        cx="12"
        cy="12"
        r="10.5"
        className={
          status === "current" ? styles.currentRing : styles.upcomingRing
        }
      />
      {status === "current" && (
        <circle cx="12" cy="12" r="6" className={styles.currentDot} />
      )}
    </svg>
  );
}

export default function RegistrationSteps({
  steps,
  current,
}: Readonly<{
  steps: readonly RegistrationStep[];
  /** The index in `steps` the citizen is on. */
  current: number;
}>) {
  return (
    <ol className={styles.steps} aria-label="Account creation steps">
      {steps.map((step, index) => {
        let status: Status = "upcoming";
        if (index < current) status = "complete";
        else if (index === current) status = "current";
        return (
          <li
            key={`${step.prefix ?? ""}${step.label}`}
            className={styles.step}
            aria-current={status === "current" ? "step" : undefined}
          >
            <Marker status={status} />
            <span>
              {step.prefix && <strong>{step.prefix} </strong>}
              {step.label}
              <span className={styles.visuallyHidden}>
                {" "}
                ({STATUS_TEXT[status]})
              </span>
            </span>
          </li>
        );
      })}
    </ol>
  );
}
