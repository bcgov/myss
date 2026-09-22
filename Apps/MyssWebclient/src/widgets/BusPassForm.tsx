import { Button } from "@bcgov/design-system-react-components";
import { Form } from "@formio/react";
import { useEffect, useRef } from "react";

import "@formio/js/dist/formio.form.min.css";
import "@/components/PocForm.css";
import styles from "./BusPassForm.module.css";

import {
    BUS_PASS_KEYWORDS,
    BusPassUnavailableError,
    type BusPassSubmissionPayload,
} from "@/api/busPass";
import SubmissionErrors from "@/components/SubmissionErrors";
import { useSubmitBusPass } from "@/hooks/useBusPass";
import { useFormSpec } from "@/hooks/usePocForm";

const FORM_SPEC_ID = "bc-bus-pass";

// Display text for each outcome keyword the API can return. Interim: the
// handbook has these resolving from the content engine by key, so the keyword
// stays the contract and only the wording here is provisional.
const KEYWORD_MESSAGES: Record<string, string> = {
    [BUS_PASS_KEYWORDS.rejected]:
        "The BC Bus Pass Program could not accept this request. Check that the details you entered match what the ministry has on file, or contact the program for help.",
    [BUS_PASS_KEYWORDS.rateLimited]:
        "Too many requests have been sent from your connection in a short time. Wait a few minutes and try again.",
};

const FALLBACK_REJECTED =
    "The BC Bus Pass Program could not accept this request.";
const MESSAGE_MAY_HAVE_REACHED =
    "Your request was saved, and the ministry may already have it. Do not submit it again. If you do not hear back, contact the ministry and quote the submission ID below.";
const MESSAGE_RETRY_SAFE =
    "Your request was saved but could not be sent to the ministry right now. Try again in a few minutes, or contact the ministry and quote the submission ID below.";

/** The parts of the Form.io instance this component talks to. */
interface FormInstance {
    emit: (event: string, ...args: unknown[]) => void;
}

/**
 * Moves focus to a result heading when it appears, so a screen-reader user is
 * told the outcome rather than being left at the submit button.
 */
function useFocusOnMount() {
    const headingRef = useRef<HTMLHeadingElement>(null);
    useEffect(() => {
        headingRef.current?.focus();
    }, []);
    return headingRef;
}

/** The ministry answered: the request was accepted, or it was declined. */
function SubmissionOutcome({
    result,
    onStartAgain,
}: {
    result: BusPassSubmissionPayload;
    onStartAgain: () => void;
}) {
    const headingRef = useFocusOnMount();

    if (result.outcome === "Accepted") {
        return (
            <section className={styles.outcome} aria-live="polite">
                <h2 ref={headingRef} tabIndex={-1} className={styles.heading}>
                    Request submitted
                </h2>
                <p>
                    The BC Bus Pass Program has received your request. Your
                    reference number is{" "}
                    <span className={styles.reference}>
                        {result.referenceNumber}
                    </span>
                    . Keep it for your records.
                </p>
                <dl className={styles.details}>
                    <dt>Submission ID</dt>
                    <dd>
                        <code>{result.submissionId}</code>
                    </dd>
                </dl>
            </section>
        );
    }

    const message =
        (result.keyword && KEYWORD_MESSAGES[result.keyword]) ??
        FALLBACK_REJECTED;

    return (
        <section
            className={`${styles.outcome} ${styles.rejected}`}
            aria-live="polite"
        >
            <h2 ref={headingRef} tabIndex={-1} className={styles.heading}>
                Request not accepted
            </h2>
            <p>{message}</p>
            <dl className={styles.details}>
                {result.referenceNumber && (
                    <>
                        <dt>Reference number</dt>
                        <dd>{result.referenceNumber}</dd>
                    </>
                )}
                {result.errorCode && (
                    <>
                        <dt>Ministry error code</dt>
                        <dd>
                            <code>{result.errorCode}</code>
                        </dd>
                    </>
                )}
                <dt>Submission ID</dt>
                <dd>
                    <code>{result.submissionId}</code>
                </dd>
            </dl>
            <div className={styles.actions}>
                <Button variant="secondary" onPress={onStartAgain}>
                    Start a new request
                </Button>
            </div>
        </section>
    );
}

/**
 * The request could not be delivered to the ministry. Two cases, decided by
 * the API: when the ministry may already hold it, the form is gone and the
 * citizen is told not to submit again (each submission files a service
 * request); when it provably never arrived, this sits above the form so they
 * can try again. A throttled request reads the same way as the second case.
 */
function SubmissionUnavailable({ error }: { error: BusPassUnavailableError }) {
    const headingRef = useFocusOnMount();
    const throttled = error.keyword === BUS_PASS_KEYWORDS.rateLimited;
    const message = throttled
        ? KEYWORD_MESSAGES[BUS_PASS_KEYWORDS.rateLimited]
        : error.mayHaveReachedIcm
          ? MESSAGE_MAY_HAVE_REACHED
          : MESSAGE_RETRY_SAFE;

    return (
        <section
            className={`${styles.outcome} ${styles.unavailable}`}
            role="alert"
        >
            <h2 ref={headingRef} tabIndex={-1} className={styles.heading}>
                {throttled
                    ? "Too many requests"
                    : error.mayHaveReachedIcm
                      ? "Request saved"
                      : "Request saved but not sent"}
            </h2>
            <p>{message}</p>
            {error.submissionId && (
                <dl className={styles.details}>
                    <dt>Submission ID</dt>
                    <dd>
                        <code>{error.submissionId}</code>
                    </dd>
                </dl>
            )}
        </section>
    );
}

/**
 * Renders the BC Bus Pass Form.io spec fetched from the MyssApi forms endpoint
 * and posts the answers to the bus pass endpoint with the exact spec version
 * the form was rendered under. The endpoint validates, stores and hands the
 * request to the ministry; every outcome it reports is shown here.
 */
export default function BusPassForm() {
    const { data: spec, error, isPending } = useFormSpec(FORM_SPEC_ID);
    const submit = useSubmitBusPass();

    // Form.io disables its submit button when clicked and re-enables it only
    // when told the submit finished. With an in-memory spec nobody tells it,
    // so this component does, once the mutation settles: on failure the button
    // must work again for another attempt, and it must not fire twice while a
    // submission is in flight.
    const formRef = useRef<FormInstance | null>(null);

    if (isPending) return <p>Loading form…</p>;
    if (error) return <p>Could not load the form: {error.message}</p>;

    if (submit.data) {
        return (
            <SubmissionOutcome
                result={submit.data}
                onStartAgain={() => submit.reset()}
            />
        );
    }

    const unavailable =
        submit.error instanceof BusPassUnavailableError ? submit.error : null;

    // The ministry may already have it: no form, so it cannot be sent twice.
    if (
        unavailable &&
        unavailable.mayHaveReachedIcm &&
        unavailable.keyword !== BUS_PASS_KEYWORDS.rateLimited
    ) {
        return <SubmissionUnavailable error={unavailable} />;
    }

    return (
        <section>
            {submit.error &&
                (unavailable ? (
                    <SubmissionUnavailable error={unavailable} />
                ) : (
                    <SubmissionErrors error={submit.error} />
                ))}
            <Form
                src={spec.spec}
                onFormReady={(instance) => {
                    formRef.current = instance as unknown as FormInstance;
                }}
                onSubmit={(submission: { data: Record<string, unknown> }) => {
                    if (submit.isPending) return;
                    submit.mutate(
                        {
                            formSpecVersion: spec.version,
                            answers: submission.data,
                        },
                        {
                            // `cancelSubmit` re-enables the button without
                            // Form.io's own "please fix the errors" message,
                            // which would contradict the notice shown above.
                            onError: () =>
                                formRef.current?.emit("cancelSubmit"),
                        },
                    );
                }}
            />
        </section>
    );
}
