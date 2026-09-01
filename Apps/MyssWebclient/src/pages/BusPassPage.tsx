import { Link } from "react-router";

import { authHeaders } from "@/auth/accessToken";
import BusPassForm from "@/components/BusPassForm";
import { API_URL } from "@/constants";
import { useSubmissions } from "@/hooks/usePocForm";

const FORM_SPEC_ID = "bc-bus-pass";

async function openSubmissionPdf(id: string) {
  const popup = window.open("", "_blank");
  if (!popup) {
    console.warn("PDF popup was blocked by the browser.");
    return;
  }

  popup.opener = null;

  try {
    const res = await fetch(`${API_URL}/v1/bus-pass/submissions/${id}/pdf`, {
      headers: authHeaders(),
    });

    if (!res.ok) {
      console.error(`PDF fetch failed (${res.status})`);
      popup.document.write(
        `<html><head><title>PDF unavailable</title></head><body><h1>PDF unavailable</h1><p>The PDF could not be loaded.</p></body></html>`,
      );
      popup.document.close();
      return;
    }

    const blob = await res.blob();
    const objectUrl = URL.createObjectURL(blob);
    popup.document.title = "Bus pass PDF";
    popup.location.href = objectUrl;
    window.setTimeout(() => URL.revokeObjectURL(objectUrl), 60_000);
  } catch (err) {
    console.error("Failed to open submission PDF", err);
    popup.document.write(
      `<html><head><title>PDF unavailable</title></head><body><h1>PDF unavailable</h1><p>There was a problem opening this PDF.</p></body></html>`,
    );
    popup.document.close();
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
