import { Link, useParams } from "react-router";

import { AFA_FORM_SPEC_ID } from "@/hooks/useIntake";
import { paths } from "@/routes/paths";
import ApplicationForm from "@/widgets/intake/ApplicationForm";
import styles from "./DashboardPage.module.css";

/** AFA detail: the form is editable only while the request is a draft. */
export default function AfaServiceRequestPage() {
  const { id } = useParams<{ id: string }>();

  return (
    <div className={styles.page}>
      <nav aria-label="Breadcrumb">
        <Link to={paths.serviceRequests}>Service Requests</Link>
      </nav>
      <h1>Application for Assistance</h1>
      <ApplicationForm
        applicationId={id!}
        expectedFormSpecId={AFA_FORM_SPEC_ID}
      />
    </div>
  );
}
