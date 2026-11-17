import { useParams } from "react-router";

import WorkerOverrideForm from "@/widgets/workerOverride/WorkerOverrideForm";
import pageStyles from "./Page.module.css";

/**
 * One worker override application (MYSS-321), opened from its link: the draft
 * to fill in and submit, or what was submitted.
 */
export default function WorkerOverridePage() {
    const { id } = useParams<{ id: string }>();

    return (
        <div className={pageStyles.page}>
            <h1>Worker override: Application for Assistance</h1>
            <WorkerOverrideForm applicationId={id!} />
        </div>
    );
}
