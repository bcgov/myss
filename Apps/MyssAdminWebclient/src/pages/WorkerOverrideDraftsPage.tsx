import { Link } from "react-router";

import WorkerOverrideDrafts from "@/widgets/workerOverride/WorkerOverrideDrafts";
import { paths } from "@/routes/paths";
import pageStyles from "./Page.module.css";

/**
 * Unsubmitted IA applications (MYSS-322): every worker override Application
 * for Assistance a worker has started and not yet submitted, whoever started
 * it. Each opens to be finished.
 */
export default function WorkerOverrideDraftsPage() {
  return (
    <div className={pageStyles.page}>
      <h1>Unsubmitted IA applications</h1>
      <p>
        Worker override applications for assistance that have been started but
        not submitted. Any worker can open one to finish and submit it.
      </p>
      <p>
        <Link to={paths.workerOverride}>Start a new application</Link>
      </p>
      <WorkerOverrideDrafts />
    </div>
  );
}
