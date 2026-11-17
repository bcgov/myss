import { useCallback, useEffect, useRef, useState, type ReactNode } from "react";
import { useNavigate, useSearchParams } from "react-router";
import { Accordion, AccordionGroup, Button, InlineAlert } from "@bcgov/design-system-react-components";
import { SubmissionErrors } from "@myss/web-common/components";
import { type Session, useSession } from "@/auth/useSession";
import { useAccountCreationAnswers } from "@/hooks/useAccountCreationAnswers";
import {
    type KeptDocumentsUpload,
    type RefusedDocument,
    useAttachKeptDocuments,
} from "@/hooks/useAttachKeptDocuments";
import {
    ACCOUNT_CONTEXTS,
    type AccountContext,
    type AccountContextValue,
    type AccountCreationAnswers,
    accountCreationRecapRows,
    allowsBcServicesCard,
    contextFromSlug,
    contextFromValue,
    contextsFor,
    documentFieldsFor,
    isRepresentative,
    openingAnswers,
    registrationPathFor,
    toAccountCreationRequest,
} from "@/lib/accountContext";
import { canKeepFiles, clearKept, readKeptAnswers } from "@/lib/keptAccountCreation";
import { paths } from "@/routes/paths";
import RegistrationForm, { type RegistrationAccountCreation } from "@/widgets/RegistrationForm";
import AboutMySS from "@/widgets/home/AboutMySS";
import AccountCreationQuestions from "@/widgets/registration/AccountCreationQuestions";
import WhatsNeeded from "@/widgets/registration/WhatsNeeded";
import styles from "./RegistrationPage.module.css";

type Login = Session["login"];

/**
 * Shown in place of the form once the API accepts the registration. The
 * heading takes focus so a screen-reader user hears that the page changed.
 */
function RegistrationComplete({ pinCreated }: Readonly<{ pinCreated: boolean }>) {
    const navigate = useNavigate();
    const headingRef = useRef<HTMLHeadingElement>(null);

    useEffect(() => {
        headingRef.current?.focus();
        // The answers and files the tab kept for the registration are done with.
        void clearKept();
    }, []);

    return (
        <div className={styles.page}>
            <h1 ref={headingRef} tabIndex={-1} className={styles.title}>
                Account registration complete
            </h1>
            <InlineAlert
                variant="success"
                role="status"
                title="Your account is being prepared. This should take less than 5 minutes."
            />
            {pinCreated && (
                // MYSS-258 AC7: the citizen is told the PIN was created.
                <InlineAlert
                    variant="success"
                    title="Your PIN has been created."
                    description="You will need it when you apply for services and sign documents in My Self Serve."
                />
            )}
            <p>Please try signing in again shortly. Thank you for your patience.</p>
            <div>
                <Button variant="secondary" onPress={() => navigate(paths.home)}>
                    Return to the MySS homepage
                </Button>
            </div>
        </div>
    );
}

/** Whether this tab can keep files through the sign-in; assumed until the browser says. */
function useCanKeepFiles(): boolean {
    const [canKeep, setCanKeep] = useState(true);
    useEffect(() => {
        let active = true;
        void canKeepFiles().then((result) => {
            if (active) setCanKeep(result);
        });
        return () => {
            active = false;
        };
    }, []);
    return canKeep;
}

/** The answers the questions open with: the tab's kept ones, else Self; an address's context wins. */
function startingAnswers(addressContext: AccountContext | null): AccountCreationAnswers {
    const base = readKeptAnswers()?.answers ?? openingAnswers();
    return addressContext ? { ...base, context: addressContext.value } : base;
}

/**
 * Whether answers were confirmed for the address's context: kept in the tab,
 * or confirmed on this page when the tab could not keep them.
 */
function hasAnswersFor(context: AccountContext, confirmedContext: AccountContextValue | null): boolean {
    return readKeptAnswers()?.answers.context === context.value || confirmedContext === context.value;
}

function AccountHelp() {
    return (
        <>
            <p>
                <strong>Notice:</strong> If you have used MySS previously, do not register for a new account, PLACEHOLDER TEXT. ADD REAL TEXT.
            </p>
            <h2>MySS account Help</h2>
            <AccordionGroup>
                <Accordion label="I don’t have a BC Services Card or BCeID">
                    <p>Information coming soon.</p>
                </Accordion>
                <Accordion label="Why do I need to log in with my government ID?">
                    <p>Information coming soon.</p>
                </Accordion>
                <Accordion label="How do I log into my previous MySS account?">
                    <p>Information coming soon.</p>
                </Accordion>
            </AccordionGroup>
            <section className={styles.aboutBand}>
                <AboutMySS />
            </section>
        </>
    );
}

/** The sign-in options the context allows; the sign-in returns with the context. */
function SignInOptions({ context, login }: Readonly<{ context: AccountContext; login: Login }>) {
    const headingRef = useRef<HTMLHeadingElement>(null);
    useEffect(() => {
        headingRef.current?.focus();
    }, []);

    const returnTo = registrationPathFor(context);
    const bothSignIns = allowsBcServicesCard(context.value);
    return (
        <>
            <h1 ref={headingRef} tabIndex={-1} className={styles.title}>
                Create your MySS account
            </h1>
            {bothSignIns ? (
                <p>
                    To create your MySS account, you will need to first sign in with a B.C. government ID. You can use your BC Services Card or BCeID. <br />
                    After signing in you can start your MySS registration.
                </p>
            ) : (
                <p>
                    To create an account for someone else, you will need to first sign in with Basic BCeID. <br />
                    After signing in you can start your MySS registration.
                </p>
            )}
            <div className={styles.actions}>
                {bothSignIns && (
                    <Button variant="primary" size="large" onPress={() => login("bcServicesCard", returnTo)}>
                        Register with BC Services Card
                    </Button>
                )}
                <Button
                    variant={bothSignIns ? "secondary" : "primary"}
                    size="large"
                    onPress={() => login("bceid", returnTo)}
                >
                    Register with Basic BCeID
                </Button>
            </div>
            <AccountHelp />
        </>
    );
}

/**
 * Before sign-in: who the account is for and that context's own questions.
 * Continue keeps the answers in the tab and moves to the address that names
 * the context, where the sign-in options it allows are shown.
 */
function SignedOutRegistration({ login }: Readonly<{ login: Login }>) {
    const navigate = useNavigate();
    const [searchParams] = useSearchParams();
    const addressContext = contextFromSlug(searchParams.get("context"));
    const canKeep = useCanKeepFiles();
    const questions = useAccountCreationAnswers(() => startingAnswers(addressContext));
    const [confirmedContext, setConfirmedContext] = useState<AccountContextValue | null>(null);
    const showingSignIn = addressContext !== null && hasAnswersFor(addressContext, confirmedContext);

    // Back from the sign-in options replaces their focused heading, so focus this one.
    const titleRef = useRef<HTMLHeadingElement>(null);
    const wasShowingSignIn = useRef(showingSignIn);
    useEffect(() => {
        if (wasShowingSignIn.current && !showingSignIn) titleRef.current?.focus();
        wasShowingSignIn.current = showingSignIn;
    }, [showingSignIn]);

    if (addressContext && showingSignIn) {
        return <SignInOptions context={addressContext} login={login} />;
    }

    function onContinue() {
        if (!questions.check({ canAttach: canKeep })) return;
        const context = contextFromValue(questions.answers.context);
        if (!context) return;
        const filesNotKept = !canKeep && documentFieldsFor(questions.answers).length > 0;
        questions.keep({ filesNotKept });
        setConfirmedContext(context.value);
        void navigate(registrationPathFor(context));
    }

    return (
        <>
            <h1 ref={titleRef} tabIndex={-1} className={styles.title}>
                Create your MySS account
            </h1>
            <WhatsNeeded />
            {questions.summary && <SubmissionErrors error={questions.summary} />}
            <AccountCreationQuestions
                answers={questions.answers}
                contexts={ACCOUNT_CONTEXTS}
                documentMode="keep"
                canKeep={canKeep}
                errors={questions.errors}
                onChange={questions.change}
                onKeepFile={questions.addFile}
                onUploaded={questions.addUploaded}
                onRemoveFile={questions.removeFile}
            />
            <div>
                <Button variant="primary" onPress={onContinue}>
                    Continue
                </Button>
            </div>
        </>
    );
}

type Reason = "missing" | "blocked" | "refused" | "notKept" | "api";

interface ViewState {
    view: "attaching" | "questions" | "form";
    reason: Reason | null;
}

/** What the sign-in arrived with: answers for the form, or why the questions are asked. */
interface Arrival {
    answers: AccountCreationAnswers | null;
    filesNotKept: boolean;
    reason: "missing" | "blocked" | null;
}

function arrivalFor(addressContext: AccountContext | null, signedInWithBceid: boolean): Arrival {
    // An address with no known context, as after Sign in: an account for oneself.
    if (!addressContext) return { answers: openingAnswers(), filesNotKept: false, reason: null };
    if (!signedInWithBceid && !allowsBcServicesCard(addressContext.value)) {
        return { answers: null, filesNotKept: false, reason: "blocked" };
    }
    const kept = readKeptAnswers();
    return kept?.answers.context === addressContext.value
        ? { answers: kept.answers, filesNotKept: kept.filesNotKept, reason: null }
        : { answers: null, filesNotKept: false, reason: "missing" };
}

/** What the page shows until the citizen acts: the questions again, the uploads, or the form. */
function arrivalView(arrival: Arrival, upload: KeptDocumentsUpload): ViewState {
    if (!arrival.answers) return { view: "questions", reason: arrival.reason };
    if (upload.state === "attaching") return { view: "attaching", reason: null };
    if (upload.refused.length > 0) return { view: "questions", reason: "refused" };
    if (arrival.filesNotKept) return { view: "questions", reason: "notKept" };
    return { view: "form", reason: null };
}

function accountCreationFor(answers: AccountCreationAnswers): RegistrationAccountCreation | null {
    const context = answers.context;
    if (!context) return null;
    return {
        request: toAccountCreationRequest({ ...answers, context }),
        rows: accountCreationRecapRows(answers),
        representative: isRepresentative(context),
    };
}

/** The registration form's current step heading, the one not hidden. */
function focusCurrentStep(container: HTMLElement | null) {
    const headings = container?.querySelectorAll<HTMLElement>('h2[tabindex="-1"]') ?? [];
    Array.from(headings)
        .find((heading) => !heading.closest("[hidden]"))
        ?.focus();
}

function RefusedDocuments({ refused }: Readonly<{ refused: RefusedDocument[] }>) {
    return (
        <InlineAlert variant="danger" role="alert">
            <span className="title" id="alert-title">
                {refused.length === 1 ? "A document was not attached" : "Some documents were not attached"}
            </span>
            <span className="description">
                <ul className={styles.refused}>
                    {refused.map((document, index) => (
                        <li key={index}>
                            <strong>{document.fileName}:</strong> {document.message}
                        </li>
                    ))}
                </ul>
                Attach a replacement below.
            </span>
        </InlineAlert>
    );
}

/** Why the questions are asked again after sign-in, when the citizen needs telling. */
function QuestionsNotice({
    reason,
    refused,
    onSignOut,
}: Readonly<{ reason: Reason | null; refused: RefusedDocument[]; onSignOut: () => void }>) {
    switch (reason) {
        case "blocked":
            return (
                <InlineAlert
                    variant="info"
                    title="You signed in with a BC Services Card"
                    description="To create an account for someone else, sign out and sign in again with Basic BCeID. To create an account for yourself, choose an answer below."
                    buttons={
                        <Button variant="secondary" size="small" onPress={onSignOut}>
                            Sign out
                        </Button>
                    }
                />
            );
        case "refused":
            return <RefusedDocuments refused={refused} />;
        case "notKept":
            return (
                <InlineAlert
                    variant="info"
                    title="Attach your documents"
                    description="Your browser could not keep your files while you signed in. Attach them now."
                />
            );
        default:
            return null;
    }
}

interface SignedInRegistrationProps {
    user: Session["user"];
    requirePin: boolean;
    signedInWith: string;
    onRegistered: () => void;
    logout: () => void;
}

/**
 * After sign-in: the files the tab kept are uploaded, then the registration
 * form opens with the answers; an address with no context opens it for an
 * account for oneself. The questions are asked again when nothing usable was
 * kept for the address's context, a file was refused, or the API refuses an
 * answer; once the form has opened, it stays mounted behind them, with its
 * answers.
 */
function SignedInRegistration({ user, requirePin, signedInWith, onRegistered, logout }: Readonly<SignedInRegistrationProps>) {
    const navigate = useNavigate();
    const [searchParams] = useSearchParams();
    const signedInWithBceid = Boolean(user?.bceidGuid);
    // Decided once, on arrival from the sign-in.
    const [arrival] = useState(() => arrivalFor(contextFromSlug(searchParams.get("context")), signedInWithBceid));
    const questions = useAccountCreationAnswers(() => arrival.answers ?? openingAnswers());
    const { change } = questions;
    const upload = useAttachKeptDocuments(arrival.answers, (finished) => change(finished.attached));
    // Set once the citizen acts; until then the arrival decides.
    const [chosen, setChosen] = useState<ViewState | null>(null);
    const [formOpened, setFormOpened] = useState(false);

    useEffect(() => {
        if (arrival.reason === "blocked") void clearKept();
    }, [arrival]);

    const initial = arrivalView(arrival, upload);
    const current = chosen ?? initial;
    const formMounted = formOpened || initial.view === "form";
    const accountCreation = accountCreationFor(questions.answers);
    const asking = current.reason === "missing" || current.reason === "blocked";

    // Focus follows the view: the questions' heading, unless their error
    // summary takes it, or the form's current step on the way back to it.
    const formView = useRef<HTMLDivElement>(null);
    const previousView = useRef(current.view);
    useEffect(() => {
        const previous = previousView.current;
        previousView.current = current.view;
        if (previous === current.view) return;
        if (current.view === "questions" && current.reason !== "api") {
            document.getElementById("account-context-heading")?.focus();
        } else if (current.view === "form" && previous === "questions") {
            focusCurrentStep(formView.current);
        }
    }, [current.view, current.reason]);

    async function cancel() {
        await clearKept();
        logout();
    }

    function show(next: ViewState) {
        setChosen(next);
        window.scrollTo?.({ top: 0 });
    }

    function onContinue() {
        if (!questions.check({ canAttach: true })) return;
        const context = contextFromValue(questions.answers.context);
        if (!context) return;
        questions.keep();
        // The address names the context, so a reload finds the kept answers.
        void navigate(registrationPathFor(context), { replace: true });
        setFormOpened(true);
        show({ view: "form", reason: null });
    }

    return (
        <>
            {current.view === "attaching" && (
                <p role="status" aria-live="polite">
                    Attaching your documents… {upload.done} of {upload.total}
                </p>
            )}
            {current.view === "questions" && (
                <>
                    <QuestionsNotice reason={current.reason} refused={upload.refused} onSignOut={() => void cancel()} />
                    {asking && <WhatsNeeded />}
                    {questions.summary && <SubmissionErrors error={questions.summary} />}
                    <AccountCreationQuestions
                        answers={questions.answers}
                        contexts={asking ? contextsFor(signedInWithBceid) : null}
                        documentMode="upload"
                        canKeep
                        errors={questions.errors}
                        onChange={questions.change}
                        onKeepFile={questions.addFile}
                        onUploaded={questions.addUploaded}
                        onRemoveFile={questions.removeFile}
                    />
                    <div className={styles.questionActions}>
                        <Button variant="secondary" onPress={() => void cancel()}>
                            Cancel
                        </Button>
                        <Button variant="primary" onPress={onContinue}>
                            Continue
                        </Button>
                    </div>
                </>
            )}
            {formMounted && accountCreation && (
                <div ref={formView} hidden={current.view !== "form"}>
                    <RegistrationForm
                        identity={user ?? {}}
                        accountCreation={accountCreation}
                        onAccountCreationRejected={(errors) => {
                            questions.showServerErrors(errors);
                            show({ view: "questions", reason: "api" });
                        }}
                        requirePin={requirePin}
                        signedInWith={signedInWith}
                        subject={user?.sub ?? ""}
                        onRegistered={onRegistered}
                        onCancel={() => void cancel()}
                    />
                </div>
            )}
        </>
    );
}

export default function RegistrationPage() {
    const { hasProfile, isAuthenticated, isLoading, isMeLoading, login, logout, user } = useSession();
    const navigate = useNavigate();
    // Set the moment the API accepts the registration. The refreshed /me then
    // reports a profile, and without this the redirect below would replace the
    // confirmation with the dashboard.
    const [registered, setRegistered] = useState(false);
    const handleRegistered = useCallback(() => setRegistered(true), []);

    useEffect(() => {
        if (isAuthenticated && !isMeLoading && hasProfile && !registered) {
            // The answers given before sign-in are for an account that exists already.
            void clearKept();
            void navigate(paths.dashboard, { replace: true });
        }
    }, [hasProfile, isAuthenticated, isMeLoading, navigate, registered]);

    // Only Basic BCeID users create a PIN (MYSS-258); BC Services Card users
    // do not have one.
    const requirePin = Boolean(user?.bceidGuid);

    if (registered) return <RegistrationComplete pinCreated={requirePin} />;

    const signedInWith = user?.idirUsername ? "IDIR" : user?.bceidGuid ? "BCeID" : "BC Services Card";

    let body: ReactNode;
    if (isLoading) {
        body = (
            <p role="status" aria-live="polite">
                Checking your session…
            </p>
        );
    } else if (isAuthenticated && (isMeLoading || hasProfile !== false)) {
        body = (
            <p role="status" aria-live="polite">
                Checking your MySS account…
            </p>
        );
    } else if (isAuthenticated) {
        body = (
            <>
                <InlineAlert variant="success">
                    {/* InlineAlert renders `title` only when it has no
                        children, so the title uses the component's own
                        markup (the target of its aria-labelledby). */}
                    <span className="title" id="alert-title">
                        Signed in successfully
                    </span>
                    <span className="description">
                        You’re signed in with your {signedInWith}.
                        <br />
                        We need some additional information to create your MySS account.
                    </span>
                </InlineAlert>
                <h1 className={styles.title}>Create your MySS account</h1>
                <SignedInRegistration
                    user={user}
                    requirePin={requirePin}
                    signedInWith={signedInWith}
                    onRegistered={handleRegistered}
                    logout={logout}
                />
            </>
        );
    } else {
        body = <SignedOutRegistration login={login} />;
    }

    return <div className={styles.page}>{body}</div>;
}
