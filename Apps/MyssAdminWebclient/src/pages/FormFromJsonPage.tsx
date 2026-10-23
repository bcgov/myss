import { Button, TextField } from "@bcgov/design-system-react-components";
import type { FormType } from "@formio/react/lib/components/Form";
import { useQueryClient } from "@tanstack/react-query";
import {
  Fragment,
  useCallback,
  useEffect,
  useId,
  useMemo,
  useRef,
  useState,
  type ReactNode,
  type RefObject,
} from "react";
import {
  Link,
  matchPath,
  useBeforeUnload,
  useBlocker,
  useNavigate,
  useParams,
  useSearchParams,
} from "react-router";

import { SpecRejectedError, isVersionTaken } from "@/api/forms";
import type { FormSummary, SaveDraftInput } from "@/api/forms";
import ErrorSummary, { type ErrorSummaryItem } from "@/components/ErrorSummary";
import {
  useAdminForms,
  useFormVersion,
  usePublishForm,
  useSaveDraft,
} from "@/hooks/useForms";
import { isValidFormId } from "@/lib/formSpecId";
import {
  confirmValue,
  currentVersionOf,
  describeSaveTarget,
  isConfirmed,
  saveTargetFor,
  type ExistingFormTarget,
  type SaveTarget,
} from "@/lib/formVersions";
import FormPreview from "@/widgets/formio/FormPreview";
import {
  EXAMPLE_SPEC,
  checkSpec,
  formatSpecJson,
  locateKey,
  parseSpecJson,
  type SpecCheck,
  type SpecProblem,
  type TextPosition,
} from "@myss/formio";
import { formFromJsonPath, paths } from "@/routes/paths";
import pageStyles from "./Page.module.css";
import adminStyles from "./AdminPage.module.css";
import styles from "./FormFromJsonPage.module.css";

// How long typing must pause before the JSON is checked again.
const CHECK_DELAY_MS = 400;

const EXAMPLE_TEXT = JSON.stringify(EXAMPLE_SPEC, null, 2);

/** What a read of the forms list says about the page's form ID. */
interface TargetDecision {
  readonly formSpecId: string;
  readonly target: SaveTarget;
  /** The title the page shows and its saves send; null shows the ID instead. */
  readonly title: string | null;
  /** The version pasted JSON is compared with; null for a new form. */
  readonly currentVersion: number | null;
  /** A new form, or an existing form whose target the URL confirms. */
  readonly opened: boolean;
}

/**
 * What the next save sends: a versioned save creates that version and is
 * refused if it exists; an ordinary save updates the form's unpublished draft.
 */
type NextSave =
  | { readonly kind: "versioned"; readonly version: number }
  | { readonly kind: "ordinary" };

/** The decision the text box opened with, fixed until the guard runs again. */
interface Session extends TargetDecision {
  /** The guard run that opened it. */
  readonly run: number;
  /** The current version's spec once loaded; null for a new form. */
  readonly current: FormType | null | undefined;
  readonly nextSave: NextSave;
  /** The removed questions the administrator agreed to drop, comma-joined. */
  readonly confirmedRemoved: string | null;
}

/** What this page last stored. */
interface SaveRecord {
  /** The text stored, to tell whether there are unsaved changes. */
  readonly text: string;
  readonly version: number;
  readonly published: boolean;
}

type SaveAction = "save" | "publish";

/** When the forms list last changed before a guard run started. */
interface ReadMark {
  readonly dataAt: number;
  readonly errorAt: number;
}

/** One check of the save target; it waits for a read that ends after it starts. */
interface GuardRun {
  readonly id: number;
  readonly since: ReadMark;
  /** The ID it checks; the URL can show a new ID only after the run starts. */
  readonly formSpecId: string;
}

type ReadState = "checking" | "failed" | "ready";

// React Query first returns the list it cached earlier, and keeps it after a
// failed re-read, so only a read that ended after the run started counts.
function readStateSince(
  forms: ReturnType<typeof useAdminForms>,
  since: ReadMark,
): ReadState {
  if (forms.isError && forms.errorUpdatedAt > since.errorAt) return "failed";
  if (forms.isSuccess && forms.dataUpdatedAt > since.dataAt) return "ready";
  return "checking";
}

// An existing form keeps its own title, so a title in the URL cannot rename it.
function decide(
  forms: readonly FormSummary[],
  formSpecId: string,
  titleParam: string | null,
  confirm: string | null,
): TargetDecision {
  const form = forms.find((entry) => entry.formSpecId === formSpecId);
  const target = saveTargetFor(form?.versions);
  return {
    formSpecId,
    target,
    title: form ? form.title?.trim() || null : titleParam,
    currentVersion: currentVersionOf(form?.versions),
    opened: target.kind === "new-form" || isConfirmed(target, confirm),
  };
}

// A comparison version already loaded for the same form is kept, with the
// removed questions confirmed against it.
function openSession(
  decision: TargetDecision,
  run: number,
  previous: Session | null,
): Session {
  const kept =
    previous?.formSpecId === decision.formSpecId &&
    previous.currentVersion === decision.currentVersion
      ? previous
      : null;
  return {
    ...decision,
    run,
    current: decision.currentVersion === null ? null : kept?.current,
    nextSave: firstSaveFor(decision.target),
    confirmedRemoved: kept?.confirmedRemoved ?? null,
  };
}

function firstSaveFor(target: SaveTarget): NextSave {
  switch (target.kind) {
    case "new-form":
      return { kind: "versioned", version: 1 };
    case "new-version":
      return { kind: "versioned", version: target.version };
    case "replace-draft":
      return { kind: "ordinary" };
  }
}

function saveInput(
  spec: FormType,
  title: string | null,
  next: NextSave,
): SaveDraftInput {
  return next.kind === "versioned"
    ? { spec, title, newVersion: next.version }
    : { spec, title };
}

function targetLabel(target: SaveTarget): string {
  switch (target.kind) {
    case "new-form":
      return "New form";
    case "new-version":
      return `New version ${target.version}`;
    case "replace-draft":
      return `Replaces draft version ${target.version}`;
  }
}

function saveStateText(record: SaveRecord | null, unsaved: boolean): string {
  if (!record) return "Not saved yet";
  const stored = record.published
    ? `Version ${record.version} published`
    : `Draft version ${record.version} saved`;
  return unsaved ? `${stored} · unsaved changes` : stored;
}

function outcomeText(record: SaveRecord | null, unsaved: boolean): string {
  if (!record || unsaved) return "";
  return record.published
    ? `Published version ${record.version}.`
    : `Draft version ${record.version} saved.`;
}

function leaveQuestion(busy: boolean, saved: boolean): string {
  if (busy)
    return "Leave before the save finishes? Your JSON may still be saved.";
  return saved
    ? "Discard your changes since the last save? The saved draft is kept."
    : "Discard this JSON? Nothing has been saved.";
}

/** The text box's content, parsed and checked. */
type Analysis =
  | { readonly state: "empty" }
  | { readonly state: "invalid"; readonly problem: SpecProblem }
  | {
      readonly state: "checked";
      readonly spec: FormType;
      readonly check: SpecCheck;
    };

function analyse(text: string, current: FormType | null): Analysis {
  const parsed = parseSpecJson(text);
  if (parsed.state === "empty") return parsed;
  if (parsed.state === "invalid") return parsed;
  return {
    state: "checked",
    spec: parsed.value as FormType,
    check: checkSpec(parsed.value, current ?? undefined),
  };
}

function problemsOf(analysis: Analysis): readonly SpecProblem[] {
  if (analysis.state === "invalid") return [analysis.problem];
  if (analysis.state === "checked") return analysis.check.problems;
  return [];
}

function validSpecOf(analysis: Analysis): FormType | null {
  return analysis.state === "checked" && analysis.check.problems.length === 0
    ? analysis.spec
    : null;
}

function plural(count: number, noun: string): string {
  return `${count} ${noun}${count === 1 ? "" : "s"}`;
}

type StatusTone = "neutral" | "valid" | "invalid";

function statusOf(
  analysis: Analysis,
  formatRefused: boolean,
  currentVersion: number | null,
  comparing: boolean,
): { tone: StatusTone; text: string } {
  if (comparing) {
    return {
      tone: "neutral",
      text: `Loading version ${currentVersion} to compare with.`,
    };
  }
  if (formatRefused) {
    return { tone: "invalid", text: "Fix the JSON before formatting it." };
  }
  if (analysis.state === "empty") {
    return {
      tone: "neutral",
      text: "Paste the form's JSON, or insert the example.",
    };
  }
  if (analysis.state === "invalid") {
    return { tone: "invalid", text: analysis.problem.message };
  }
  const { problems, removedQuestions, warnings, questionCount } =
    analysis.check;
  if (problems.length > 0) {
    return {
      tone: "invalid",
      text: `${plural(problems.length, "problem")}. Fix ${problems.length === 1 ? "it" : "them"} to save.`,
    };
  }
  const sentences = [`Valid. ${plural(questionCount, "question")}.`];
  if (removedQuestions.length > 0) {
    sentences.push(
      `${plural(removedQuestions.length, "question")} removed compared with version ${currentVersion}: ${removedQuestions.join(", ")}.`,
    );
  }
  return { tone: "valid", text: [...sentences, ...warnings].join(" ") };
}

// A syntax error's own position, or else where its component's key is.
function problemPosition(
  problem: SpecProblem,
  text: string,
): TextPosition | undefined {
  if (problem.line !== undefined) {
    return { line: problem.line, column: problem.column ?? 1 };
  }
  return problem.key === undefined ? undefined : locateKey(text, problem.key);
}

// Focuses the text box with the caret at a line and column, scrolled to
// show that line. Long lines wrap, so the scroll position is an estimate.
function placeCaret(
  box: HTMLTextAreaElement,
  position: TextPosition | undefined,
) {
  box.focus();
  if (!position) return;
  const lines = box.value.split("\n");
  const lineIndex = Math.min(Math.max(position.line, 1), lines.length) - 1;
  const lineStart = lines
    .slice(0, lineIndex)
    .reduce((offset, line) => offset + line.length + 1, 0);
  const caret =
    lineStart +
    Math.min(Math.max(position.column - 1, 0), lines[lineIndex].length);
  box.setSelectionRange(caret, caret);
  box.scrollTop =
    (lineIndex / lines.length) * box.scrollHeight - box.clientHeight / 2;
}

// A refusal other than a taken version: the API's reasons, each linked to its
// component when the text has one, or a plain message with the status.
function refusalItems(
  error: unknown,
  action: SaveAction,
  text: string,
  goToProblem: (problem: SpecProblem) => void,
): ErrorSummaryItem[] {
  const rejection = error instanceof SpecRejectedError ? error : null;
  if (rejection?.errors.length) {
    return rejection.errors.map(({ message, field }) => ({
      message,
      onSelect: locateKey(text, field)
        ? () => goToProblem({ message, key: field })
        : undefined,
    }));
  }
  const status = rejection ? ` (error ${rejection.status})` : "";
  return [
    { message: `Could not ${action} this form${status}. Please try again.` },
  ];
}

// The save target and the version pasted JSON is compared with, decided from
// a read of the forms list that ended after the current guard run started.
function useTargetGuard(
  formSpecId: string,
  titleParam: string | null,
  confirm: string | null,
) {
  const queryClient = useQueryClient();
  const forms = useAdminForms();

  // Read from the cache rather than the last render, since a read can finish
  // while a save that leads to a new run is in flight.
  const markNow = useCallback((): ReadMark => {
    const state = queryClient.getQueryState(["admin-forms"]);
    return {
      dataAt: state?.dataUpdatedAt ?? 0,
      errorAt: state?.errorUpdatedAt ?? 0,
    };
  }, [queryClient]);

  const [run, setRun] = useState<GuardRun>(() => ({
    id: 0,
    since: markNow(),
    formSpecId,
  }));
  const [session, setSession] = useState<Session | null>(null);
  const [bodyShown, setBodyShown] = useState(false);

  const read =
    run.formSpecId === formSpecId
      ? readStateSince(forms, run.since)
      : "checking";
  const decision =
    read === "ready" && forms.data
      ? decide(forms.data, formSpecId, titleParam, confirm)
      : null;
  const active = session?.run !== run.id;
  if (active && decision?.opened) {
    setSession(openSession(decision, run.id, session));
  }

  const loadingVersion =
    session?.current === undefined ? session?.currentVersion : null;
  const currentRead = useFormVersion(session?.formSpecId, loadingVersion);
  if (session && session.current === undefined && currentRead.data) {
    setSession({ ...session, current: currentRead.data.spec });
  }
  if (!bodyShown && session && session.current !== undefined) {
    setBodyShown(true);
  }

  return {
    session,
    updateSession: setSession,
    /** The current run has not opened the text box yet. */
    active,
    read,
    decision,
    error: forms.error,
    currentRead,
    // Once open, the text box stays while a later run's comparison version
    // loads, so the JSON stays in view.
    bodyOpen: session !== null && (session.current !== undefined || bodyShown),
    runAgain(id = formSpecId) {
      setRun((previous) => ({
        id: previous.id + 1,
        since: markNow(),
        formSpecId: id,
      }));
      void forms.refetch();
    },
  };
}

// The JSON being edited, checked once typing pauses. The preview keeps the
// last spec that passed, so a half-finished edit does not clear it.
function useJsonText(current: FormType | null) {
  const [text, setText] = useState("");
  const [checkedText, setCheckedText] = useState("");
  const [lastValid, setLastValid] = useState<FormType | null>(null);
  const [formatRefused, setFormatRefused] = useState(false);

  useEffect(() => {
    if (text === checkedText) return;
    const timer = window.setTimeout(() => setCheckedText(text), CHECK_DELAY_MS);
    return () => window.clearTimeout(timer);
  }, [text, checkedText]);

  const analysis = useMemo(
    () => analyse(checkedText, current),
    [checkedText, current],
  );
  const validSpec = validSpecOf(analysis);
  if (validSpec && validSpec !== lastValid) setLastValid(validSpec);

  // Replaces the text and checks it at once, without waiting for a pause.
  function replace(next: string) {
    setText(next);
    setCheckedText(next);
    setFormatRefused(false);
  }

  return {
    text,
    analysis,
    lastValid,
    formatRefused,
    showingOlder: validSpec === null && lastValid !== null,
    edit(next: string) {
      setText(next);
      setFormatRefused(false);
    },
    replace,
    format() {
      if (text.trim() === "") return;
      const formatted = formatSpecJson(text);
      if (formatted === null) setFormatRefused(true);
      else replace(formatted);
    },
    // Checks the text as it is now, for a save that must not wait for a pause.
    checkNow(): Analysis {
      setCheckedText(text);
      return analyse(text, current);
    },
  };
}

type JsonText = ReturnType<typeof useJsonText>;

// Asks before unsaved JSON is left behind, in the app or by a reload or a
// closed tab. Moving to another address of this page is not leaving.
function useLeaveGuard(blocking: boolean) {
  const blocker = useBlocker(
    ({ nextLocation }) =>
      blocking &&
      matchPath(paths.formFromJson, nextLocation.pathname) === null,
  );
  useBeforeUnload(
    useCallback(
      (event: BeforeUnloadEvent) => {
        if (blocking) event.preventDefault();
      },
      [blocking],
    ),
  );
  return blocker;
}

// The existing-form warning, inline. It takes focus when it appears, since
// nothing else on the page can be done until it is answered.
function SaveTargetWarning({
  target,
  formName,
  formSpecId,
  reason,
  headingRef,
  onConfirm,
  onCancel,
}: Readonly<{
  target: ExistingFormTarget;
  formName: string;
  formSpecId: string;
  /** Why the warning is shown again, read right after its heading. */
  reason: string | null;
  headingRef: RefObject<HTMLHeadingElement | null>;
  onConfirm: () => void;
  onCancel: () => void;
}>) {
  useEffect(() => {
    headingRef.current?.focus();
  }, [headingRef]);

  const { heading, body, confirmLabel } = describeSaveTarget(
    target,
    formName,
    formSpecId,
  );
  return (
    <section className={styles.warning} aria-labelledby="save-target-heading">
      <h2
        id="save-target-heading"
        ref={headingRef}
        tabIndex={-1}
        className={styles.warningHeading}
      >
        {heading}
      </h2>
      {reason && <p className={styles.warningBody}>{reason}</p>}
      <p className={styles.warningBody}>{body}</p>
      <div className={styles.actions}>
        <Button onPress={onConfirm}>{confirmLabel}</Button>
        <Button variant="secondary" onPress={onCancel}>
          Cancel
        </Button>
      </div>
    </section>
  );
}

// What the guard shows until the text box can open: the read in progress, its
// failure, or the existing-form warning.
function TargetGuard({
  read,
  error,
  decision,
  reason,
  warningRef,
  statusRef,
  onConfirm,
  onCancel,
  onRetry,
}: Readonly<{
  read: ReadState;
  error: Error | null;
  decision: TargetDecision | null;
  reason: string | null;
  warningRef: RefObject<HTMLHeadingElement | null>;
  statusRef: RefObject<HTMLParagraphElement | null>;
  onConfirm: (target: SaveTarget) => void;
  onCancel: () => void;
  onRetry: () => void;
}>) {
  const lead = reason ? <p className={styles.status}>{reason}</p> : null;
  if (read === "checking") {
    return (
      <div className={styles.guard}>
        {lead}
        <p ref={statusRef} tabIndex={-1} className={styles.status}>
          Checking…
        </p>
      </div>
    );
  }
  if (read === "failed") {
    return (
      <div className={styles.guard}>
        {lead}
        <p ref={statusRef} tabIndex={-1} role="alert" className={styles.error}>
          Could not check this form's versions: {error?.message}
        </p>
        <div>
          <Button variant="secondary" size="small" onPress={onRetry}>
            Try again
          </Button>
        </div>
      </div>
    );
  }
  if (!decision || decision.opened || decision.target.kind === "new-form") {
    return null;
  }
  const { target } = decision;
  return (
    <SaveTargetWarning
      target={target}
      formName={decision.title ?? decision.formSpecId}
      formSpecId={decision.formSpecId}
      reason={reason}
      headingRef={warningRef}
      onConfirm={() => onConfirm(target)}
      onCancel={onCancel}
    />
  );
}

// Loading and failure states of the version pasted JSON is compared with.
function CurrentVersionStatus({
  version,
  error,
  statusRef,
  onRetry,
}: Readonly<{
  version: number | null;
  error: Error | null;
  statusRef: RefObject<HTMLParagraphElement | null>;
  onRetry: () => void;
}>) {
  if (!error) {
    return (
      <p ref={statusRef} tabIndex={-1} className={styles.status}>
        Loading version {version}…
      </p>
    );
  }
  return (
    <div className={styles.guard}>
      <p ref={statusRef} tabIndex={-1} role="alert" className={styles.error}>
        Could not load version {version} of this form: {error.message}
      </p>
      <div>
        <Button variant="secondary" size="small" onPress={onRetry}>
          Try again
        </Button>
      </div>
    </div>
  );
}

// A question asked in place, which takes focus when it appears.
function InlineConfirm({
  heading,
  children,
  confirmLabel,
  cancelLabel,
  danger = false,
  keepInViewOnBack = false,
  onConfirm,
  onCancel,
}: Readonly<{
  heading: string;
  children?: ReactNode;
  confirmLabel: string;
  cancelLabel: string;
  danger?: boolean;
  /** Shows and focuses it again after each browser Back or Forward. */
  keepInViewOnBack?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}>) {
  const headingId = useId();
  const sectionRef = useRef<HTMLElement>(null);
  const headingRef = useRef<HTMLHeadingElement>(null);
  useEffect(() => {
    headingRef.current?.focus();
    // After a blocked browser Back, the browser restores the scroll of the
    // page it was going to, once this has run; the next frame undoes that.
    const frame = requestAnimationFrame(() =>
      sectionRef.current?.scrollIntoView({ block: "nearest" }),
    );
    return () => cancelAnimationFrame(frame);
  }, []);
  // The browser can restore a scroll position after any Back or Forward,
  // the router's undo of a blocked Back included.
  useEffect(() => {
    if (!keepInViewOnBack) return;
    let frame = 0;
    const onPopState = () => {
      cancelAnimationFrame(frame);
      frame = requestAnimationFrame(() => {
        headingRef.current?.focus();
        sectionRef.current?.scrollIntoView({ block: "nearest" });
      });
    };
    window.addEventListener("popstate", onPopState);
    return () => {
      window.removeEventListener("popstate", onPopState);
      cancelAnimationFrame(frame);
    };
  }, [keepInViewOnBack]);

  return (
    <section
      ref={sectionRef}
      className={danger ? styles.confirmDanger : styles.confirm}
      aria-labelledby={headingId}
    >
      <h3
        id={headingId}
        ref={headingRef}
        tabIndex={-1}
        className={styles.confirmHeading}
      >
        {heading}
      </h3>
      {children}
      <div className={styles.actions}>
        <Button danger={danger} onPress={onConfirm}>
          {confirmLabel}
        </Button>
        <Button variant="secondary" onPress={onCancel}>
          {cancelLabel}
        </Button>
      </div>
    </section>
  );
}

// The text box with its toolbar, status line and problem list. Tab leaves
// the box as usual, so keyboard users are never trapped in it.
function JsonSection({
  json,
  textRef,
  currentVersion,
  comparing,
  summaryItems,
  focusKey,
  children,
}: Readonly<{
  json: JsonText;
  textRef: RefObject<HTMLTextAreaElement | null>;
  currentVersion: number | null;
  /** The version to compare with is still loading. */
  comparing: boolean;
  summaryItems: readonly ErrorSummaryItem[];
  focusKey: number | null;
  /** Shown below the problem list. */
  children?: ReactNode;
}>) {
  const [confirmingExample, setConfirmingExample] = useState(false);
  const status = statusOf(
    json.analysis,
    json.formatRefused,
    currentVersion,
    comparing,
  );

  function insertExample() {
    json.replace(EXAMPLE_TEXT);
    setConfirmingExample(false);
    textRef.current?.focus();
  }

  return (
    <section className={styles.section}>
      <h2 className={styles.sectionTitle}>
        <label htmlFor="form-json">Form JSON</label>
      </h2>
      <p id="form-json-hint" className={styles.hint}>
        Paste the whole form. The preview below updates as you type.
      </p>
      <div className={styles.actions}>
        <Button
          variant="secondary"
          size="small"
          onPress={() =>
            json.text.trim() === ""
              ? insertExample()
              : setConfirmingExample(true)
          }
        >
          Insert example
        </Button>
        <Button variant="secondary" size="small" onPress={json.format}>
          Format
        </Button>
      </div>
      {confirmingExample && (
        <InlineConfirm
          heading="Replace the text in the box with the example?"
          confirmLabel="Replace"
          cancelLabel="Keep my text"
          onConfirm={insertExample}
          onCancel={() => {
            setConfirmingExample(false);
            textRef.current?.focus();
          }}
        />
      )}
      <textarea
        id="form-json"
        ref={textRef}
        className={styles.json}
        rows={20}
        spellCheck={false}
        autoComplete="off"
        autoCapitalize="off"
        aria-describedby="form-json-hint form-json-status"
        value={json.text}
        onChange={(event) => json.edit(event.target.value)}
      />
      <p id="form-json-status" className={styles[status.tone]}>
        {status.text}
      </p>
      {/* Never announced, as it changes while the administrator types; it
          takes focus only through focusKey. */}
      <ErrorSummary items={summaryItems} focusKey={focusKey} announce={false} />
      {children}
    </section>
  );
}

// The form as citizens will see it, built from the last valid spec.
function PreviewSection({ json }: Readonly<{ json: JsonText }>) {
  return (
    <section className={styles.section} aria-labelledby="preview-heading">
      <h2 id="preview-heading" className={styles.sectionTitle}>
        Preview
      </h2>
      <p className={styles.hint}>
        How citizens will see this form. Answers here are not saved or sent.
      </p>
      {json.showingOlder && (
        <p className={styles.note}>Showing the last valid version.</p>
      )}
      {json.lastValid ? (
        <div className={styles.preview}>
          <FormPreview spec={json.lastValid} />
        </div>
      ) : (
        <p className={styles.previewEmpty}>
          The preview appears here when the JSON is valid.
        </p>
      )}
    </section>
  );
}

// Presses during a save are ignored rather than the buttons being disabled,
// so focus stays where it is.
function ActionBar({
  busy,
  record,
  unsaved,
  onSave,
  onPublish,
  onLeave,
}: Readonly<{
  busy: SaveAction | null;
  record: SaveRecord | null;
  unsaved: boolean;
  onSave: () => void;
  onPublish: () => void;
  onLeave: () => void;
}>) {
  return (
    <div className={styles.actionBar}>
      <Button onPress={onSave}>
        {busy === "save" ? "Saving…" : "Save draft"}
      </Button>
      <Button variant="secondary" onPress={onPublish}>
        {busy === "publish" ? "Publishing…" : "Publish"}
      </Button>
      <Button variant="tertiary" onPress={onLeave}>
        {record ? "Close" : "Cancel"}
      </Button>
      <output className={styles.outcome}>{outcomeText(record, unsaved)}</output>
    </div>
  );
}

// Asks for another ID once the page's ID has been taken by someone else.
function NewIdForm({
  onUse,
}: Readonly<{ onUse: (formSpecId: string) => string | null }>) {
  const formRef = useRef<HTMLFormElement>(null);
  const [value, setValue] = useState("");
  const [error, setError] = useState<string | null>(null);

  return (
    <form
      ref={formRef}
      className={styles.newId}
      aria-label="Use another ID"
      onSubmit={(event) => {
        event.preventDefault();
        const refused = onUse(value.trim());
        setError(refused);
        // Back to the field, so its error is read out.
        if (refused) formRef.current?.querySelector("input")?.focus();
      }}
    >
      <TextField
        label="Form ID"
        value={value}
        onChange={(next) => {
          setValue(next);
          setError(null);
        }}
        isInvalid={error !== null}
        errorMessage={error ?? undefined}
        autoComplete="off"
        spellCheck="false"
      />
      <div>
        <Button type="submit">Use this ID</Button>
      </div>
    </form>
  );
}

// The questions the current version has and the pasted JSON lacks, before
// a save that would drop them.
function RemovedQuestionsConfirm({
  keys,
  currentVersion,
  onConfirm,
  onCancel,
}: Readonly<{
  keys: readonly string[];
  currentVersion: number | null;
  onConfirm: () => void;
  onCancel: () => void;
}>) {
  const heading =
    keys.length === 1
      ? `A question from version ${currentVersion} is missing`
      : `${keys.length} questions from version ${currentVersion} are missing`;
  return (
    <InlineConfirm
      heading={heading}
      confirmLabel="Save anyway"
      cancelLabel="Keep editing"
      onConfirm={onConfirm}
      onCancel={onCancel}
    >
      <p>
        These questions are in version {currentVersion} but not in your JSON:{" "}
        {keys.map((key, index) => (
          <Fragment key={key}>
            {index > 0 && ", "}
            <code>{key}</code>
          </Fragment>
        ))}
        . The new version will not collect them, and features that read them by
        key will stop receiving them. Earlier submissions are not affected.
      </p>
    </InlineConfirm>
  );
}

// The page for a valid form ID. The JSON and what has been saved are held
// here, so they survive a change of save target or ID.
function JsonPage({ formSpecId }: Readonly<{ formSpecId: string }>) {
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const titleParam = searchParams.get("title")?.trim() || null;
  const confirm = searchParams.get("confirm");

  const guard = useTargetGuard(formSpecId, titleParam, confirm);
  const { session } = guard;
  const comparing = session !== null && session.current === undefined;

  const json = useJsonText(session?.current ?? null);
  const textRef = useRef<HTMLTextAreaElement>(null);
  const warningRef = useRef<HTMLHeadingElement>(null);
  const guardStatusRef = useRef<HTMLParagraphElement>(null);

  const saveDraft = useSaveDraft(session?.formSpecId);
  const publish = usePublishForm(session?.formSpecId);
  const [record, setRecord] = useState<SaveRecord | null>(null);
  const [busy, setBusy] = useState<SaveAction | null>(null);
  const [refusal, setRefusal] = useState<ErrorSummaryItem[]>([]);
  const [guardReason, setGuardReason] = useState<string | null>(null);
  const [idTaken, setIdTaken] = useState(false);
  const [blockedEmpty, setBlockedEmpty] = useState(false);
  const [focusKey, setFocusKey] = useState<number | null>(null);
  const [removedPrompt, setRemovedPrompt] = useState<{
    keys: readonly string[];
    action: SaveAction;
  } | null>(null);

  const unsaved =
    record === null ? json.text.trim() !== "" : json.text !== record.text;
  const blocker = useLeaveGuard(unsaved || busy !== null);

  // Answering the warning or choosing another ID removes the control that
  // had focus; focus goes to the text box once it is open again.
  const focusTextOnOpen = useRef(false);
  useEffect(() => {
    if (!guard.bodyOpen || !focusTextOnOpen.current) return;
    focusTextOnOpen.current = false;
    textRef.current?.focus();
  }, [guard.bodyOpen, session]);

  function goToProblem(problem: SpecProblem) {
    const box = textRef.current;
    if (box) placeCaret(box, problemPosition(problem, box.value));
  }

  function focusSummary() {
    setFocusKey((key) => (key ?? 0) + 1);
  }

  // Records the accepted target in the URL, which opens the text box.
  function confirmTarget(target: SaveTarget) {
    const value = confirmValue(target);
    if (value === null) return;
    setGuardReason(null);
    focusTextOnOpen.current = true;
    setSearchParams(
      (params) => {
        const next = new URLSearchParams(params);
        next.set("confirm", value);
        return next;
      },
      { replace: true },
    );
  }

  // Moves the page, and the JSON on it, to another ID, and decides that ID's
  // target. Returns the reason the ID was refused, if it was.
  function switchToId(id: string): string | null {
    if (!isValidFormId(id)) {
      return "Enter a form ID using only lowercase letters, numbers and hyphens.";
    }
    setIdTaken(false);
    setRefusal([]);
    focusTextOnOpen.current = true;
    void navigate(formFromJsonPath(id, titleParam ?? ""), {
      replace: true,
    });
    guard.runAgain(id);
    return null;
  }

  // A taken version 1 means someone took the ID, so another is asked for; a
  // later version means the form changed, so the guard runs again.
  function showRefusal(
    error: unknown,
    stage: SaveAction,
    id: string,
    next: NextSave,
  ) {
    if (
      stage === "save" &&
      next.kind === "versioned" &&
      isVersionTaken(error)
    ) {
      if (next.version === 1) {
        setIdTaken(true);
        setRefusal([
          {
            message: `The ID ${id} was taken while you were working. Your JSON is kept. Enter another ID.`,
          },
        ]);
        focusSummary();
      } else {
        setGuardReason(
          `Version ${next.version} of ${id} was saved by someone else while you were working. Your JSON is kept.`,
        );
        guard.runAgain();
      }
      return;
    }
    setRefusal(refusalItems(error, stage, json.text, goToProblem));
    focusSummary();
  }

  // Publish saves first and records that save, so a retry after a failed
  // publish updates the stored draft rather than creating it again.
  async function send(open: Session, spec: FormType, action: SaveAction) {
    const sentText = json.text;
    const next = open.nextSave;
    setBusy(action);
    setRefusal([]);
    setGuardReason(null);
    setIdTaken(false);
    let stage: SaveAction = "save";
    try {
      const stored = await saveDraft.mutateAsync(
        saveInput(spec, open.title, next),
      );
      setRecord({ text: sentText, version: stored.version, published: false });
      guard.updateSession((s) => s && { ...s, nextSave: { kind: "ordinary" } });
      if (action === "publish") {
        stage = "publish";
        const published = await publish.mutateAsync();
        setRecord({
          text: sentText,
          version: published.version,
          published: true,
        });
        // With no draft of its own left, the next save creates the next
        // version rather than updating a draft someone else starts.
        guard.updateSession(
          (s) =>
            s && {
              ...s,
              nextSave: { kind: "versioned", version: published.version + 1 },
            },
        );
      }
    } catch (error) {
      showRefusal(error, stage, open.formSpecId, next);
    } finally {
      setBusy(null);
    }
  }

  // Checks the text as it is now. Problems move focus to the list; questions
  // the current version has and the JSON lacks need confirming first.
  async function attemptSave(
    action: SaveAction,
    confirmed: string | null = session?.confirmedRemoved ?? null,
  ) {
    if (busy) return;
    if (!session || guard.active || session.current === undefined) {
      (warningRef.current ?? guardStatusRef.current)?.focus();
      return;
    }
    setRemovedPrompt(null);
    const analysis = json.checkNow();
    setBlockedEmpty(analysis.state === "empty");
    if (analysis.state !== "checked" || analysis.check.problems.length > 0) {
      focusSummary();
      return;
    }
    const removed = analysis.check.removedQuestions;
    if (removed.length > 0 && removed.join(",") !== confirmed) {
      setRemovedPrompt({ keys: removed, action });
      return;
    }
    await send(session, analysis.spec, action);
  }

  const summaryItems: ErrorSummaryItem[] = [
    ...(blockedEmpty && json.analysis.state === "empty"
      ? [{ message: "Paste the form's JSON before saving." }]
      : []),
    ...(comparing ? [] : problemsOf(json.analysis)).map((problem) => ({
      message: problem.message,
      onSelect: () => goToProblem(problem),
    })),
    ...refusal,
  ];

  const shown = guard.active ? (guard.decision ?? session) : session;
  const checking = guard.active && !guard.decision;

  return (
    <>
      <header className={adminStyles.heading}>
        <p className={adminStyles.eyebrow}>My Self Serve</p>
        <h1>{shown?.title ?? formSpecId}</h1>
        <p className={styles.meta}>
          ID <code>{formSpecId}</code> ·{" "}
          {checking || !shown ? "Checking…" : targetLabel(shown.target)} ·{" "}
          {saveStateText(record, unsaved)}
        </p>
      </header>

      {guard.active && (
        <TargetGuard
          read={guard.read}
          error={guard.error}
          decision={guard.decision}
          reason={guardReason}
          warningRef={warningRef}
          statusRef={guardStatusRef}
          onConfirm={confirmTarget}
          onCancel={() => void navigate(paths.formManagement)}
          onRetry={() => guard.runAgain()}
        />
      )}

      {comparing && session && (
        <CurrentVersionStatus
          version={session.currentVersion}
          error={guard.currentRead.error}
          statusRef={guardStatusRef}
          onRetry={() => void guard.currentRead.refetch()}
        />
      )}

      {guard.bodyOpen && session && (
        <>
          <JsonSection
            json={json}
            textRef={textRef}
            currentVersion={session.currentVersion}
            comparing={comparing}
            summaryItems={summaryItems}
            focusKey={focusKey}
          >
            {idTaken && <NewIdForm onUse={switchToId} />}
          </JsonSection>
          <PreviewSection json={json} />
          <ActionBar
            busy={busy}
            record={record}
            unsaved={unsaved}
            onSave={() => void attemptSave("save")}
            onPublish={() => void attemptSave("publish")}
            onLeave={() => void navigate(paths.formManagement)}
          />
          {removedPrompt && (
            <RemovedQuestionsConfirm
              key={removedPrompt.keys.join(",")}
              keys={removedPrompt.keys}
              currentVersion={session.currentVersion}
              onConfirm={() => {
                const keys = removedPrompt.keys.join(",");
                guard.updateSession(
                  (s) => s && { ...s, confirmedRemoved: keys },
                );
                textRef.current?.focus();
                void attemptSave(removedPrompt.action, keys);
              }}
              onCancel={() => {
                setRemovedPrompt(null);
                textRef.current?.focus();
              }}
            />
          )}
        </>
      )}

      {blocker.state === "blocked" && (
        <InlineConfirm
          danger
          keepInViewOnBack
          heading={leaveQuestion(busy !== null, record !== null)}
          confirmLabel="Discard"
          cancelLabel="Keep editing"
          onConfirm={() => blocker.proceed()}
          onCancel={() => {
            blocker.reset();
            textRef.current?.focus();
          }}
        />
      )}
    </>
  );
}

// Saves pasted JSON as a new form, or, once the administrator has confirmed
// it, as a new version of an existing form. Nothing is stored before the
// first Save draft or Publish.
export default function FormFromJsonPage() {
  const { formSpecId = "" } = useParams<{ formSpecId: string }>();
  const valid = isValidFormId(formSpecId);

  return (
    <div className={pageStyles.page}>
      <nav aria-label="Breadcrumb">
        <Link to={paths.formManagement}>← Form management</Link>
      </nav>

      {valid ? (
        <JsonPage formSpecId={formSpecId} />
      ) : (
        <>
          <header className={adminStyles.heading}>
            <p className={adminStyles.eyebrow}>My Self Serve</p>
            <h1>Create form from JSON</h1>
          </header>
          <p role="alert" className={styles.error}>
            The address does not contain a valid form ID. A form ID uses only
            lowercase letters, numbers and hyphens.{" "}
            <Link to={paths.formManagement}>Back to Form management</Link>
          </p>
        </>
      )}
    </div>
  );
}
