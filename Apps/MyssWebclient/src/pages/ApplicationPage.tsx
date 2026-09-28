import { Link, useParams } from "react-router";

import { paths } from "@/routes/paths";
import ApplicationForm from "@/widgets/intake/ApplicationForm";

/**
 * One Income Assistance application: editable while it is a draft, read-only
 * once submitted. Reached from the dashboard's "My applications" list.
 */
export default function ApplicationPage() {
  const { id } = useParams<{ id: string }>();

  return (
    <>
      <nav aria-label="Breadcrumb">
        <Link to={paths.dashboard}>← My applications</Link>
      </nav>
      <h1>Income Assistance application</h1>
      <ApplicationForm applicationId={id!} />
    </>
  );
}
