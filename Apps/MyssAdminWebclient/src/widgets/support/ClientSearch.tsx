import { type SubmitEvent, useState } from "react";
import { Button, TextField } from "@bcgov/design-system-react-components";
import { ApplicationRequestError } from "@myss/web-common/api";

import tableStyles from "@/widgets/review/ReviewQueue.module.css";
import styles from "./ClientSearch.module.css";

import {
  CLIENT_SEARCH_KEYWORDS,
  useClientSearch,
  type ClientSearchCriteria,
  type ClientSignInType,
  type ClientSummaryPayload,
} from "@/hooks/useClientSearch";

// Support View client search (MYSS-275): a worker searches by first name, last
// name, SIN or BCeID user name, any one or several together, and sees enough to
// tell the clients apart. Names and the BCeID user name match on any part; the
// SIN only in full, and a result shows its last three digits only. The checks
// here spare a round trip; the API makes the same ones.

const EMPTY: Required<ClientSearchCriteria> = {
  firstName: "",
  lastName: "",
  sin: "",
  bceidUsername: "",
};

const SIGN_IN_LABELS: Record<ClientSignInType, string> = {
  BCEID: "BCeID",
  BC_SERVICES_CARD: "BC Services Card",
  WORKER_OVERRIDE: "Worker override",
};

const CRITERIA_REQUIRED = "Enter at least one search field.";
const SIN_INVALID = "Enter the full 9-digit SIN.";

/** A SIN field is usable when it holds nine digits, spaces and dashes aside. */
function isFullSin(value: string): boolean {
  return /^[\d\s-]*$/.test(value) && value.replace(/\D/g, "").length === 9;
}

function errorMessage(error: Error): string {
  if (!(error instanceof ApplicationRequestError) || error.status !== 400) {
    return `Could not search for clients: ${error.message}`;
  }
  return error.keyword === CLIENT_SEARCH_KEYWORDS.sinInvalid
    ? SIN_INVALID
    : CRITERIA_REQUIRED;
}

function maskedSin(lastThree?: string | null): string {
  return lastThree ? `***-***-${lastThree}` : "—";
}

function Results({ criteria }: Readonly<{ criteria: ClientSearchCriteria }>) {
  const { data, error, isPending } = useClientSearch(criteria);

  if (isPending) return <output>Searching…</output>;
  if (error) return <p role="alert">{errorMessage(error)}</p>;
  if (data.clients.length === 0) {
    return <output>No matching clients found.</output>;
  }

  return (
    <>
      <table className={tableStyles.table}>
        <caption className={tableStyles.caption}>
          {data.clients.length === 1
            ? "1 matching client"
            : `${data.clients.length} matching clients`}
        </caption>
        <thead>
          <tr>
            <th scope="col">Name</th>
            <th scope="col">BCeID</th>
            <th scope="col">Sign-in</th>
            <th scope="col">SIN</th>
            <th scope="col">Date of Birth</th>
          </tr>
        </thead>
        <tbody>
          {data.clients.map((client: ClientSummaryPayload) => (
            <tr key={client.id}>
              <td>
                {client.lastName}, {client.firstName}
              </td>
              <td>{client.bceidUsername ?? "—"}</td>
              <td>
                {client.signInType ? SIGN_IN_LABELS[client.signInType] : "—"}
              </td>
              <td>{maskedSin(client.sinLastThree)}</td>
              <td>{client.dateOfBirth}</td>
            </tr>
          ))}
        </tbody>
      </table>
      {data.hasMore && (
        <p>
          Showing the first {data.clients.length} matches. Add more search
          fields to narrow the search.
        </p>
      )}
    </>
  );
}

export default function ClientSearch() {
  const [fields, setFields] = useState(EMPTY);
  const [submitted, setSubmitted] = useState<ClientSearchCriteria | null>(
    null,
  );
  const [formError, setFormError] = useState<string | null>(null);
  const [sinError, setSinError] = useState<string | null>(null);

  const set = (key: keyof ClientSearchCriteria) => (value: string) =>
    setFields((current) => ({ ...current, [key]: value }));

  function onSearch(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault();
    const trimmed = {
      firstName: fields.firstName.trim(),
      lastName: fields.lastName.trim(),
      sin: fields.sin.trim(),
      bceidUsername: fields.bceidUsername.trim(),
    };
    const anyField = Object.values(trimmed).some((v) => v !== "");
    const sinOk = trimmed.sin === "" || isFullSin(trimmed.sin);

    setFormError(anyField ? null : CRITERIA_REQUIRED);
    setSinError(sinOk ? null : SIN_INVALID);
    // Nothing from an earlier search stays on screen beside a refusal.
    setSubmitted(anyField && sinOk ? trimmed : null);
  }

  function onClear() {
    setFields(EMPTY);
    setSubmitted(null);
    setFormError(null);
    setSinError(null);
  }

  return (
    <>
      <form
        className={styles.form}
        onSubmit={onSearch}
        aria-label="Search for clients"
        noValidate
      >
        <div className={styles.fields}>
          <TextField
            label="First name"
            value={fields.firstName}
            onChange={set("firstName")}
          />
          <TextField
            label="Last name"
            value={fields.lastName}
            onChange={set("lastName")}
          />
          <TextField
            label="SIN"
            description="The full 9-digit SIN."
            value={fields.sin}
            onChange={set("sin")}
            isInvalid={sinError !== null}
            errorMessage={sinError ?? undefined}
          />
          <TextField
            label="BCeID user name"
            value={fields.bceidUsername}
            onChange={set("bceidUsername")}
          />
        </div>
        {formError && (
          <p role="alert" className={styles.error}>
            {formError}
          </p>
        )}
        <div className={styles.actions}>
          <Button variant="primary" type="submit">
            Search
          </Button>
          <Button variant="secondary" type="button" onPress={onClear}>
            Clear
          </Button>
        </div>
      </form>
      {submitted && (
        <section className={styles.results} aria-label="Search results">
          <Results criteria={submitted} />
        </section>
      )}
    </>
  );
}
