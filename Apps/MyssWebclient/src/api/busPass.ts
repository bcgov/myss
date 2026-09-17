import { API_URL } from "@/constants";
import { readValidationErrors, SubmissionRejectedError } from "@/api/forms";

// Calls to the BC Bus Pass API (/v1/bus-pass): the public submit endpoint that
// validates and stores a request, then hands it to the ministry's case system
// through the ICM middleware. No authHeaders(): the page is public, like the
// legacy form it replaces, and the API's route allows anonymous callers.
//
// Not in the generated client yet, hence the raw fetch. Swap for the SDK after
// regenerating the schema.

/** How the hand-off to the ministry ended, as the API reports it. */
export type BusPassOutcome = "Accepted" | "Rejected" | "Failed";

/** Mirrors BusPassSubmissionResponseModel in MyssApi/Models/BusPassModels.cs. */
export interface BusPassSubmissionPayload {
    submissionId: string;
    formSpecId: string;
    formSpecVersion: number;
    /** The number the ministry assigned, when it did. */
    referenceNumber?: string | null;
    outcome: BusPassOutcome;
    /** The stable keyword for a non-accepted outcome (BUSPASS.SUBMIT.*). */
    keyword?: string | null;
    /** The ministry's own error code for a rejected request. */
    errorCode?: string | null;
}

/** The keywords the API returns for a stored request the ministry did not accept. */
export const BUS_PASS_KEYWORDS = {
    rejected: "BUSPASS.SUBMIT.REJECTED",
    icmUnavailable: "BUSPASS.SUBMIT.ICM_UNAVAILABLE",
    rateLimited: "BUSPASS.SUBMIT.RATE_LIMITED",
} as const;

/**
 * The request was validated and stored but could not be delivered to the
 * ministry (503), or the call failed in some other way that carried no error
 * collection. `submissionId` is present when the API stored the request, so
 * the citizen can quote it; `keyword` when the body was problem details.
 */
export class BusPassUnavailableError extends Error {
    readonly status: number;
    readonly keyword?: string;
    readonly submissionId?: string;
    /**
     * The API's verdict on whether the ministry might already hold the
     * request. It files one service request per call, so when this is true the
     * citizen must not submit again; when false a retry is safe. Unknown counts
     * as true, since a guess the other way files duplicates.
     */
    readonly mayHaveReachedIcm: boolean;

    constructor(
        status: number,
        keyword?: string,
        submissionId?: string,
        detail?: string,
        mayHaveReachedIcm = true,
    ) {
        super(detail ?? `Submission failed (${status})`);
        this.name = "BusPassUnavailableError";
        this.status = status;
        this.keyword = keyword;
        this.submissionId = submissionId;
        this.mayHaveReachedIcm = mayHaveReachedIcm;

        // See SubmissionRejectedError: keep `instanceof` reliable after downlevel.
        Object.setPrototypeOf(this, BusPassUnavailableError.prototype);
    }
}

/**
 * Submits a bus pass request stamped with the spec version it was rendered
 * with. 200 is an outcome to show, accepted or rejected alike; 422 throws
 * SubmissionRejectedError with every field error; anything else throws
 * BusPassUnavailableError.
 */
export async function submitBusPass(input: {
    formSpecVersion: number;
    answers: Record<string, unknown>;
}): Promise<BusPassSubmissionPayload> {
    const res = await fetch(`${API_URL}/v1/bus-pass/submissions`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(input),
    });

    if (res.status === 422) {
        throw new SubmissionRejectedError(
            res.status,
            await readValidationErrors(res),
        );
    }

    if (!res.ok) {
        let keyword: string | undefined;
        let submissionId: string | undefined;
        let detail: string | undefined;
        // A throttled request (429) was never stored, so a retry is always safe.
        let mayHaveReachedIcm = res.status !== 429;
        try {
            const problem = await res.json();
            if (typeof problem?.keyword === "string") keyword = problem.keyword;
            if (typeof problem?.submissionId === "string")
                submissionId = problem.submissionId;
            if (typeof problem?.detail === "string") detail = problem.detail;
            if (typeof problem?.mayHaveReachedIcm === "boolean")
                mayHaveReachedIcm = problem.mayHaveReachedIcm;
        } catch {
            // Not a ProblemDetails body; the status alone will have to do.
        }
        throw new BusPassUnavailableError(
            res.status,
            keyword,
            submissionId,
            detail,
            mayHaveReachedIcm,
        );
    }

    return (await res.json()).payload;
}
