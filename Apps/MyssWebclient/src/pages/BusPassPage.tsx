import { Link } from "react-router";

import BusPassForm from "@/components/BusPassForm";
import { useSubmissions } from "@/hooks/usePocForm";

const FORM_SPEC_ID = "bc-bus-pass";

export default function BusPassPage() {
  const { data: submissions } = useSubmissions(FORM_SPEC_ID);

  return (
    <>
      <nav aria-label="Breadcrumb">
        <Link to="/techdemos">← Tech demos</Link>
      </nav>
      <BusPassForm />

      <section>
        <h3>Previous submissions</h3>
        {submissions?.length === 0 && <p>None yet.</p>}
        {submissions && submissions.length > 0 && (
          <ul>
            {submissions.map((s) => (
              <li key={s.id}>
                <Link to={`/techdemos/forms/submissions/${s.id}`}>
                  {new Date(s.submittedAt).toLocaleString()} - spec v
                  {s.formSpecVersion} - <code>{s.id.slice(0, 8)}…</code>
                </Link>
              </li>
            ))}
          </ul>
        )}
      </section>
    </>
  );
}
