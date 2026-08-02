import { Link } from "react-router";

import PocForm from "@/components/PocForm";
import { useSubmissions } from "@/hooks/usePocForm";

const FORM_SPEC_ID = "poc-test-form";

/**
 * Forms tech demo: the Strapi-authored, version-stamped Form.io loop.
 */
export default function FormsTechDemo() {
  const { data: submissions } = useSubmissions(FORM_SPEC_ID);

  return (
    <>
      <nav aria-label="Breadcrumb">
        <Link to="/techdemos">← Tech demos</Link>
      </nav>
      <h1>Forms tech demo</h1>
      <PocForm />
      <section>
        <h3>Previous submissions</h3>
        {submissions && submissions.length === 0 && <p>None yet.</p>}
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
