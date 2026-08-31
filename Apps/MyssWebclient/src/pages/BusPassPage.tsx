import { Link } from "react-router";

import { authHeaders } from "@/auth/accessToken";
import BusPassForm from "@/components/BusPassForm";
import { API_URL } from "@/constants";
import { useSubmissions } from "@/hooks/usePocForm";

const FORM_SPEC_ID = "bc-bus-pass";

async function openSubmissionPdf(id: string) {
  try {
    const res = await fetch(`${API_URL}/v1/bus-pass/submissions/${id}/pdf`, {
      headers: authHeaders(),
    });

    if (!res.ok) {
      console.error(`PDF fetch failed (${res.status})`);
      return;
    }

    const blob = await res.blob();
    const objectUrl = URL.createObjectURL(blob);
    const popup = window.open(objectUrl, "_blank", "noopener,noreferrer");

    if (!popup) {
      window.location.assign(objectUrl);
      return;
    }

    // Give the new tab time to load the blob before revoking.
    window.setTimeout(() => URL.revokeObjectURL(objectUrl), 60_000);
  } catch (err) {
    console.error("Failed to open submission PDF", err);
  }
}

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
          <table>
            <thead>
              <tr>
                <th scope="col">Submission</th>
                <th scope="col">PDF</th>
              </tr>
            </thead>
            <tbody>
              {submissions.map((s) => (
                <tr key={s.id}>
                  <td>
                    <Link to={`/techdemos/forms/submissions/${s.id}`}>
                      {new Date(s.submittedAt).toLocaleString()} - spec v
                      {s.formSpecVersion} - <code>{s.id.slice(0, 8)}…</code>
                    </Link>
                  </td>
                  <td>
                    <button
                      type="button"
                      onClick={() => {
                        void openSubmissionPdf(s.id);
                      }}
                    >
                      View PDF
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>
    </>
  );
}
