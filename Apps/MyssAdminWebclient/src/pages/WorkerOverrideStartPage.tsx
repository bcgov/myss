import { Button } from "@bcgov/design-system-react-components";
import { useCallback } from "react";
import { Link, useNavigate } from "react-router";
import { ApplicationRequestError } from "@myss/web-common/api";

import {
    useStartWorkerOverride,
    WORKER_OVERRIDE_KEYWORDS,
} from "@/hooks/useWorkerOverride";
import { paths, workerOverridePath } from "@/routes/paths";
import pageStyles from "./Page.module.css";

/**
 * Worker override (MYSS-321): where a worker starts an Application for
 * Assistance for a client who cannot submit their own. Starting makes a draft
 * and opens it; the unsubmitted list (MYSS-322) is how it is found again.
 */
export default function WorkerOverrideStartPage() {
    const navigate = useNavigate();
    const start = useStartWorkerOverride();
    const { mutate } = start;

    const handleStart = useCallback(() => {
        mutate(undefined, {
            onSuccess: (draft) => {
                void navigate(workerOverridePath(draft.id));
            },
        });
    }, [mutate, navigate]);

    const notPublished =
        start.error instanceof ApplicationRequestError &&
        start.error.keyword === WORKER_OVERRIDE_KEYWORDS.specNotPublished;

    return (
        <div className={pageStyles.page}>
            <h1>Worker override</h1>
            <p>
                Submit an Application for Assistance for a client who cannot
                submit their own. It asks for the client&apos;s first and last
                name, email, phone number, date of birth and SIN. Submitting
                creates a worker override account for the client and puts the
                application in the review queue. Nothing is sent to the client.
            </p>
            <p>
                To carry on with one already started, see the{" "}
                <Link to={paths.workerOverrideDrafts}>
                    unsubmitted IA applications
                </Link>
                .
            </p>
            {start.error && (
                <p role="alert">
                    {notPublished
                        ? "The worker override form has not been published yet."
                        : `The application could not be started: ${start.error.message}`}
                </p>
            )}
            <div>
                <Button
                    variant="primary"
                    onPress={handleStart}
                    isDisabled={start.isPending}
                >
                    Start an application
                </Button>
            </div>
        </div>
    );
}
