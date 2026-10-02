import { Link } from "react-router";

import BusPassForm from "@/widgets/BusPassForm";
import { API_URL } from "@/constants";
import { useSubmissions } from "@/hooks/usePocForm";

const FORM_SPEC_ID = "bc-bus-pass";

// Replaces the popup's blank document with a plain explanation; the popup was
// opened before the fetch so the browser does not treat it as a blocked one.
function showUnavailable(popup: Window, message: string) {
  const { document } = popup;
  document.title = "PDF unavailable";
  const heading = document.createElement("h1");
  heading.textContent = "PDF unavailable";
  const paragraph = document.createElement("p");
  paragraph.textContent = message;
  document.body.replaceChildren(heading, paragraph);
}

async function openSubmissionPdf(id: string) {
  const popup = window.open("", "_blank");
  if (!popup) {
    console.warn("PDF popup was blocked by the browser.");
    return;
  }

  popup.opener = null;

  try {
    // Public endpoint: bus pass PDFs do not require a signed-in session.
    const res = await fetch(`${API_URL}/v1/bus-pass/submissions/${id}/pdf`);

    if (!res.ok) {
      console.error(`PDF fetch failed (${res.status})`);
      showUnavailable(popup, "The PDF could not be loaded.");
      return;
    }

    const blob = await res.blob();
    const objectUrl = URL.createObjectURL(blob);
    popup.document.title = "Bus pass PDF";
    popup.location.href = objectUrl;
    window.setTimeout(() => URL.revokeObjectURL(objectUrl), 60_000);
  } catch (err) {
    console.error("Failed to open submission PDF", err);
    showUnavailable(popup, "There was a problem opening this PDF.");
  }
}

export default function BusPassPage() {
  const { data: submissions } = useSubmissions(FORM_SPEC_ID);

  return (
    <>
      <nav aria-label="Breadcrumb">
        <Link to="/">← Home</Link>
      </nav>
      <h1>BC Bus Pass</h1>
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
