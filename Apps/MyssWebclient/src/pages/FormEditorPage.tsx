import { Form, FormBuilder } from "@formio/react";
import type { FormType } from "@formio/react/lib/components/Form";
import { Utils } from "@formio/js";
import {
    Fragment,
    memo,
    useCallback,
    useEffect,
    useMemo,
    useRef,
    useState,
} from "react";
import { Link, useNavigate, useParams, useSearchParams } from "react-router";

import "@formio/js/dist/formio.form.min.css";
import "@formio/js/dist/formio.builder.min.css";

import { FormLoadError, SpecRejectedError } from "@/api/forms";
import type { FormSpecPayload, FormValidationError } from "@/api/forms";
import { registerBcgovComponents } from "@/widgets/formio/bcgovComponents";
import { builderOptions } from "@/widgets/formio/builderOptions";
import { revealAllComponents } from "@/widgets/formio/specPreview";
import {
    flattenComponents,
    listEditableComponents,
    setLabel,
    setRequired,
    type EditableComponent,
} from "@/widgets/formio/specEdit";
import {
    findDuplicateKeys,
    findUnknownConditionalTargets,
} from "@/widgets/formio/specRules";
import { useDraft, usePublishForm, useSaveDraft } from "@/hooks/useForms";
import { adminFormEditorPath, paths } from "@/routes/paths";
import RatesPanel from "./RatesPanel";
import homeStyles from "./HomePage.module.css";
import adminStyles from "./AdminPage.module.css";
import styles from "./FormEditorPage.module.css";

// The one form the eligibility rate table applies to; its rates show below the
// editor only when this form is open.
const ESTIMATOR_FORM_SPEC_ID = "eligibility-estimator";

// What a brand-new form starts from. The server refuses an empty components
// array, and every form needs a way to submit.
const NEW_FORM_TEMPLATE = {
    display: "form",
    components: [
        {
            type: "button",
            key: "submit",
            action: "submit",
            label: "Submit",
            input: true,
        },
    ],
} as unknown as FormType;

// Register the custom bcgovAccordion type so the preview renders it as a real
// component rather than a blank slot when a spec includes one. Idempotent.
registerBcgovComponents();

/** A reason a save/publish was refused; `field` links it to a component row. */
interface EditorError {
    message: string;
    field?: string;
}

/** Which editing surface is on screen. */
type EditorMode = "labels" | "build";

/** The save bar action that ran last; scopes which errors the summary shows. */
type EditorAction = "save" | "publish";

/** The builder handle `onBuilderReady` returns. */
interface BuilderHandle {
    instance?: { schema?: FormType };
}

/** The working copy and the editing state that travels with it. */
interface EditorState {
    /** The form the working copy was seeded for; undefined before the first. */
    seededFor: string | undefined;
    /** The edited copy the panel drives and Save draft/Publish send. */
    workingSpec: FormType | null;
    dirty: boolean;
    mode: EditorMode;
    /**
     * The spec the builder was constructed from, set only when the builder is
     * opened: changing it rebuilds the builder and drops any open dialog.
     */
    builderSeed: FormType | null;
    /**
     * Whether the working copy is the local new-form template rather than a
     * stored draft. Cleared by the first save; while set, a draft arriving from
     * the server outranks the template (see `nextSeed`).
     */
    fromTemplate: boolean;
}

const INITIAL_EDITOR_STATE: EditorState = {
    seededFor: undefined,
    workingSpec: null,
    dirty: false,
    mode: "labels",
    builderSeed: null,
    fromTemplate: false,
};

function sameSpec(a: FormType | null, b: FormType | null): boolean {
    return Utils._.isEqual(a, b);
}

function isNotFound(error: Error | null): boolean {
    return error instanceof FormLoadError && error.status === 404;
}

function seededFromDraft(
    state: EditorState,
    formSpecId: string | undefined,
    spec: FormType,
): EditorState {
    return {
        ...state,
        seededFor: formSpecId,
        workingSpec: spec,
        dirty: false,
        mode: "labels",
        builderSeed: null,
        fromTemplate: false,
    };
}

// Nothing stored yet: start from the local template, in the builder, since a
// new form is about adding fields.
function seededFromTemplate(
    state: EditorState,
    formSpecId: string | undefined,
): EditorState {
    const template = structuredClone(NEW_FORM_TEMPLATE);
    return {
        ...state,
        seededFor: formSpecId,
        workingSpec: template,
        dirty: false,
        mode: "build",
        builderSeed: template,
        fromTemplate: true,
    };
}

// The state to adopt this render, or null when the current one stands. The
// working copy is seeded from the loaded draft once per form (keyed on
// formSpecId). Keying on the form, not the draft object, means a background
// refetch (e.g. after a save) never re-seeds and discards in-progress edits;
// only opening a different form does. The loaded draft stays untouched.
function nextSeed(
    state: EditorState,
    formSpecId: string | undefined,
    draft: FormSpecPayload | undefined,
    creating: boolean,
): EditorState | null {
    if (state.seededFor === formSpecId) {
        // The form turned out to exist after the template seeded — a stale 404
        // in the cache, or another admin created the same ID. Show the stored
        // draft rather than let a save overwrite it with the template.
        if (state.fromTemplate && draft && !state.dirty) {
            return seededFromDraft(state, formSpecId, draft.spec);
        }
        return null;
    }
    if (draft) return seededFromDraft(state, formSpecId, draft.spec);
    if (creating) return seededFromTemplate(state, formSpecId);
    if (state.workingSpec !== null) return { ...state, workingSpec: null };
    return null;
}

// An edit from the labels panel: the setters return a new spec each edit.
function editedSpec(
    state: EditorState,
    edit: (spec: FormType) => FormType,
): EditorState {
    if (!state.workingSpec) return state;
    return { ...state, workingSpec: edit(state.workingSpec), dirty: true };
}

// An edit from the builder. It reports a change when a dialog is merely opened
// or cancelled, hence the no-op check.
function withBuilderSpec(state: EditorState, next: FormType): EditorState {
    if (sameSpec(state.workingSpec, next)) return state;
    return { ...state, workingSpec: next, dirty: true };
}

// Re-seed the builder from the working copy so edits made in the other mode
// carry over; without this the builder would load the spec as fetched and its
// first change would overwrite them. Re-selecting the mode it is already in
// must not re-seed, which would rebuild the builder and drop an open dialog.
function openBuilder(state: EditorState): EditorState {
    if (state.mode === "build") return state;
    return { ...state, mode: "build", builderSeed: state.workingSpec };
}

// The working copy and the editing surface. Seeded by adjusting state during
// render — the sanctioned alternative to seeding in an effect.
function useWorkingSpec(
    formSpecId: string | undefined,
    draft: FormSpecPayload | undefined,
    creating: boolean,
) {
    const [state, setState] = useState<EditorState>(INITIAL_EDITOR_STATE);
    const seeded = nextSeed(state, formSpecId, draft, creating);
    if (seeded) setState(seeded);

    const builderInstance = useRef<BuilderHandle | null>(null);
    const captureBuilder = useCallback((builder: BuilderHandle) => {
        builderInstance.current = builder;
    }, []);

    // Read the builder's schema, not the form its onChange hands over: the form
    // carries every Form.io default plus a regenerated id per component, so
    // saving it would rewrite the stored spec on the first interaction. Kept
    // referentially stable, or the builder is rebuilt on every render.
    const editInBuilder = useCallback(() => {
        const next = builderInstance.current?.instance?.schema;
        if (next) setState((s) => withBuilderSpec(s, next));
    }, []);

    return {
        ...state,
        captureBuilder,
        editInBuilder,
        editLabel: (key: string, label: string) =>
            setState((s) =>
                editedSpec(s, (spec) => setLabel(spec, key, label)),
            ),
        editRequired: (key: string, required: boolean) =>
            setState((s) =>
                editedSpec(s, (spec) => setRequired(spec, key, required)),
            ),
        showLabels: () => setState((s) => ({ ...s, mode: "labels" })),
        showBuilder: () => setState(openBuilder),
        // The working copy is stored: no longer dirty, and no longer the template.
        markSaved: () =>
            setState((s) => ({ ...s, dirty: false, fromTemplate: false })),
    };
}

// Preview shows every component at once (conditionals stripped), flattened to
// one field per row so each lines up with its settings.
function previewRows(spec: FormType | null): Record<string, unknown>[] {
    if (!spec) return [];
    return flattenComponents(revealAllComponents(spec));
}

function indexEditable(spec: FormType | null): Map<string, EditableComponent> {
    const map = new Map<string, EditableComponent>();
    if (!spec) return map;
    for (const component of listEditableComponents(spec)) {
        map.set(component.key, component);
    }
    return map;
}

function rowKey(component: Record<string, unknown>, index: number): string {
    return typeof component.key === "string" ? component.key : `row-${index}`;
}

// A stored draft's title heads the page; a form still being created takes the
// one from the URL; failing both, the ID.
function pageTitle(
    draft: FormSpecPayload | undefined,
    creating: boolean,
    newTitle: string | null,
    formSpecId: string | undefined,
): string | undefined {
    if (draft?.title?.trim()) return draft.title;
    return (creating && newTitle) || formSpecId;
}

// A stored draft's title wins; only a form still being created takes the one
// from the URL, so a `title` param cannot rename an existing form.
function saveTitleFor(
    draft: FormSpecPayload | undefined,
    creating: boolean,
    newTitle: string | null,
): string | null {
    return draft?.title ?? (creating ? newTitle : null);
}

function loadErrorMessage(error: Error): string {
    if (isNotFound(error)) {
        return "This form could not be found — check the address.";
    }
    return `Could not load this form: ${error.message}`;
}

// Whether the editor can show: the draft is loaded, or it is a form being
// created, whose expected 404 is not a failure.
function editorOpen(
    isPending: boolean,
    error: Error | null,
    creating: boolean,
    workingSpec: FormType | null,
): boolean {
    if (isPending || !workingSpec) return false;
    return !error || creating;
}

// A version other than 1 on the first save of a new form means the ID was
// already taken and the save landed on that form — say so instead of a quiet
// overwrite.
function createWarning(creating: boolean, savedVersion: number): string | null {
    if (creating && savedVersion !== 1) {
        return `A form with this ID already existed — this save created draft version ${savedVersion} of it.`;
    }
    return null;
}

// The error for the current action: a publish runs save then publish, so it
// can come from either; a save only from save. Scoped by lastAction so a
// stale error from a previous action doesn't linger.
function activeActionError(
    lastAction: EditorAction | null,
    saveError: Error | null,
    publishError: Error | null,
): Error | null {
    if (lastAction === "publish") return publishError ?? saveError;
    if (lastAction === "save") return saveError;
    return null;
}

// A 422 carries a field-error list; any other refusal (a 502 from the
// content engine, a network drop) carries none, so fall back to a plain
// message with the status rather than failing silently.
function refusalErrors(
    lastAction: EditorAction | null,
    error: Error,
): EditorError[] {
    const rejection = error instanceof SpecRejectedError ? error : null;
    if (rejection?.errors.length) {
        return rejection.errors.map((e: FormValidationError) => ({
            message: e.message,
            field: e.field,
        }));
    }
    const verb = lastAction === "publish" ? "publish" : "save";
    const status = rejection ? ` (error ${rejection.status})` : "";
    return [
        { message: `Could not ${verb} this form${status}. Please try again.` },
    ];
}

// What the summary lists: client-side rule errors take precedence over a
// server refusal from save or publish.
function shownEditorErrors(
    clientErrors: EditorError[],
    lastAction: EditorAction | null,
    saveError: Error | null,
    publishError: Error | null,
): EditorError[] {
    if (clientErrors.length) return clientErrors;
    const error = activeActionError(lastAction, saveError, publishError);
    return error ? refusalErrors(lastAction, error) : [];
}

// Isolated so the page's own re-renders do not reach the wrapper: it re-attaches
// its Form.io listeners on every render and never detaches them.
const SpecBuilder = memo(function SpecBuilder({
    seed,
    onReady,
    onChange,
}: {
    seed: FormType;
    onReady: (builder: BuilderHandle) => void;
    onChange: () => void;
}) {
    return (
        <FormBuilder
            initialForm={seed}
            options={builderOptions}
            onBuilderReady={
                onReady as unknown as React.ComponentProps<
                    typeof FormBuilder
                >["onBuilderReady"]
            }
            onChange={onChange}
        />
    );
});

// Client-side mirror of the cheap publish rules, so an obvious problem shows
// before the round-trip. The server stays the authoritative gate on publish.
function ruleErrors(spec: FormType): EditorError[] {
    const errors: EditorError[] = [];
    for (const key of findDuplicateKeys(spec)) {
        errors.push({ message: `Duplicate field key: ${key}`, field: key });
    }
    for (const key of findUnknownConditionalTargets(spec)) {
        errors.push({
            message: `A condition refers to a field that doesn't exist: ${key}`,
        });
    }
    return errors;
}

// Move focus and scroll to the field an error belongs to: its settings row in
// labels mode, or the component itself in the builder. A miss is silent — the
// message is already on screen.
function focusRow(field: string): void {
    const escaped = CSS.escape(field);
    const className = JSON.stringify(`formio-component-${field}`);
    const target =
        document.querySelector<HTMLElement>(
            `input[aria-label="Label for ${escaped}"]`,
        ) ?? document.querySelector<HTMLElement>(`[class~=${className}]`);
    if (!target) return;
    // The builder wraps each component in a plain div, which takes focus only
    // once it has a tabindex.
    if (!target.hasAttribute("tabindex")) target.tabIndex = -1;
    target.focus();
    target.scrollIntoView({ block: "center", behavior: "smooth" });
}

// One rendered field, read-only. Rendered on its own so each field can sit in
// the same grid row as its settings. Memoized on the component's content so
// only the field being edited re-initialises Form.io, not the whole form.
const PreviewCell = memo(
    function PreviewCell({
        component,
    }: {
        component: Record<string, unknown>;
    }) {
        const src = useMemo(
            () =>
                ({
                    display: "form",
                    components: [component],
                }) as unknown as FormType,
            [component],
        );
        return <Form src={src} options={{ readOnly: true, noAlerts: true }} />;
    },
    (a, b) => JSON.stringify(a.component) === JSON.stringify(b.component),
);

// The accessible error summary: one list of every reason, focused when it
// appears so a screen-reader user is told, not left at the button in silence.
// Each reason that maps to a field links to its settings row.
function EditorErrorSummary({ errors }: Readonly<{ errors: EditorError[] }>) {
    const headingRef = useRef<HTMLHeadingElement>(null);
    const signal = errors.map((e) => e.message).join("|");
    useEffect(() => {
        headingRef.current?.focus();
    }, [signal]);

    return (
        <div className={styles.errorSummary} role="alert">
            <h2 ref={headingRef} tabIndex={-1} className={styles.errorTitle}>
                There is a problem
            </h2>
            <ul className={styles.errorList}>
                {errors.map((error, index) => {
                    const field = error.field;
                    return (
                        <li key={`${field ?? ""}:${error.message}:${index}`}>
                            {field ? (
                                <button
                                    type="button"
                                    className={styles.errorLink}
                                    onClick={() => focusRow(field)}
                                >
                                    {error.message}
                                </button>
                            ) : (
                                error.message
                            )}
                        </li>
                    );
                })}
            </ul>
        </div>
    );
}

// Loading and failure states; nothing once the form is open. A new form's
// expected 404 is not a failure.
function FormLoadStatus({
    isPending,
    error,
    creating,
}: Readonly<{ isPending: boolean; error: Error | null; creating: boolean }>) {
    if (isPending) return <p>Loading form…</p>;
    if (error && !creating)
        return <p role="alert">{loadErrorMessage(error)}</p>;
    return null;
}

// The two editing surfaces, as toggle buttons.
function ModeBar({
    mode,
    onLabels,
    onBuild,
}: Readonly<{ mode: EditorMode; onLabels: () => void; onBuild: () => void }>) {
    return (
        <fieldset className={styles.modeBar} aria-label="Editing mode">
            <button
                type="button"
                className={styles.modeButton}
                aria-pressed={mode === "labels"}
                onClick={onLabels}
            >
                Edit labels
            </button>
            <button
                type="button"
                className={styles.modeButton}
                aria-pressed={mode === "build"}
                onClick={onBuild}
            >
                Build form
            </button>
        </fieldset>
    );
}

/** Edits the labels panel makes to one field. */
interface FieldEdits {
    onLabel: (key: string, label: string) => void;
    onRequired: (key: string, required: boolean) => void;
}

// The settings for one field: its key, label and required flag. A field that
// cannot be edited gets a placeholder so the row still lines up.
function FieldSettings({
    editable,
    onLabel,
    onRequired,
}: Readonly<FieldEdits & { editable: EditableComponent | undefined }>) {
    if (!editable) {
        return (
            <span className={styles.noEdit} aria-hidden="true">
                —
            </span>
        );
    }
    return (
        <div className={styles.editControls}>
            <code className={styles.key}>{editable.key}</code>
            <label className={styles.labelField}>
                <span className={styles.fieldLabel}>Label</span>
                <input
                    type="text"
                    value={editable.label}
                    aria-label={`Label for ${editable.key}`}
                    onChange={(e) => onLabel(editable.key, e.target.value)}
                />
            </label>
            <label className={styles.requiredField}>
                <input
                    type="checkbox"
                    checked={editable.required}
                    aria-label={`Required: ${editable.key}`}
                    onChange={(e) => onRequired(editable.key, e.target.checked)}
                />
                <span>Required</span>
            </label>
        </div>
    );
}

// Labels mode: each field rendered read-only (the real BC Gov skin) in the
// same grid row as its settings.
function LabelsEditor({
    rows,
    editableByKey,
    onLabel,
    onRequired,
}: Readonly<
    FieldEdits & {
        rows: Record<string, unknown>[];
        editableByKey: Map<string, EditableComponent>;
    }
>) {
    return (
        <div className={styles.grid}>
            <div className={styles.headCell}>Preview</div>
            <div className={styles.headCell}>Field settings</div>

            {rows.map((component, index) => {
                const key = rowKey(component, index);
                return (
                    <Fragment key={key}>
                        <div className={styles.previewCell}>
                            <PreviewCell component={component} />
                        </div>
                        <div className={styles.editCell}>
                            <FieldSettings
                                editable={editableByKey.get(key)}
                                onLabel={onLabel}
                                onRequired={onRequired}
                            />
                        </div>
                    </Fragment>
                );
            })}
        </div>
    );
}

// Save draft and Publish, with the outcome of the last action beside them.
function SaveBar({
    dirty,
    lastAction,
    saveDraft,
    publish,
    hasClientErrors,
    warning,
    onSave,
    onPublish,
}: Readonly<{
    dirty: boolean;
    lastAction: EditorAction | null;
    saveDraft: ReturnType<typeof useSaveDraft>;
    publish: ReturnType<typeof usePublishForm>;
    hasClientErrors: boolean;
    warning: string | null;
    onSave: () => void;
    onPublish: () => void;
}>) {
    const busy = saveDraft.isPending || publish.isPending;
    const saving = lastAction === "save" && saveDraft.isPending;
    const publishing = lastAction === "publish" && busy;
    const saved =
        lastAction === "save" &&
        saveDraft.isSuccess &&
        !dirty &&
        !hasClientErrors;
    const publishedVersion =
        lastAction === "publish" && publish.isSuccess && !dirty
            ? publish.data?.version
            : undefined;

    return (
        <div className={styles.saveBar}>
            <button
                type="button"
                className={styles.saveButton}
                onClick={onSave}
                disabled={busy}
            >
                {saving ? "Saving…" : "Save draft"}
            </button>
            <button
                type="button"
                className={styles.publishButton}
                onClick={onPublish}
                disabled={busy}
            >
                {publishing ? "Publishing…" : "Publish"}
            </button>
            {saved && <output className={styles.saved}>Draft saved.</output>}
            {publishedVersion !== undefined && (
                <output className={styles.saved}>
                    Published version {publishedVersion}.
                </output>
            )}
            {warning && (
                <span role="alert" className={styles.warning}>
                    {warning}
                </span>
            )}
        </div>
    );
}

interface EditorActionsInput {
    formSpecId: string | undefined;
    workingSpec: FormType | null;
    saveTitle: string | null;
    isNew: boolean;
    creating: boolean;
    saveDraft: ReturnType<typeof useSaveDraft>;
    publish: ReturnType<typeof usePublishForm>;
    /** Called once the working copy is stored. */
    onSaved: () => void;
}

// Save draft and Publish. Each records which action ran, so the summary scopes
// its errors to it, and runs the client-side rules before the round-trip.
function useEditorActions(input: EditorActionsInput) {
    const {
        formSpecId,
        workingSpec,
        saveTitle,
        isNew,
        creating,
        saveDraft,
        publish,
        onSaved,
    } = input;
    const navigate = useNavigate();
    const [clientErrors, setClientErrors] = useState<EditorError[]>([]);
    const [lastAction, setLastAction] = useState<EditorAction | null>(null);
    const [saveWarning, setSaveWarning] = useState<string | null>(null);

    // The spec to send, or null when a rule failed and the reasons are already
    // on screen.
    function beginAction(action: EditorAction): FormType | null {
        if (!workingSpec) return null;
        setLastAction(action);
        setSaveWarning(null);
        const errs = ruleErrors(workingSpec);
        setClientErrors(errs);
        return errs.length ? null : workingSpec;
    }

    // After the first save of a new form, drop the new-form flag from the URL
    // and history, so revisiting this page cannot seed the template over the
    // stored draft.
    function finishSave(savedVersion: number) {
        onSaved();
        if (!isNew || !formSpecId) return;
        setSaveWarning(createWarning(creating, savedVersion));
        void navigate(adminFormEditorPath(formSpecId), { replace: true });
    }

    function handleSave() {
        const spec = beginAction("save");
        if (!spec) return;
        saveDraft.mutate(
            { spec, title: saveTitle },
            { onSuccess: (saved) => finishSave(saved.version) },
        );
    }

    async function handlePublish() {
        const spec = beginAction("publish");
        if (!spec) return;
        try {
            // Persist the working copy, then publish that draft as the next
            // version. A 422 from either call surfaces via its mutation error.
            const saved = await saveDraft.mutateAsync({
                spec,
                title: saveTitle,
            });
            await publish.mutateAsync();
            finishSave(saved.version);
        } catch {
            // Handled through the error summary; the working copy is untouched.
        }
    }

    return {
        lastAction,
        saveWarning,
        hasClientErrors: clientErrors.length > 0,
        shownErrors: shownEditorErrors(
            clientErrors,
            lastAction,
            saveDraft.error,
            publish.error,
        ),
        handleSave,
        handlePublish,
    };
}

// Opens one form: each field is rendered read-only (the real BC Gov skin) in the
// same row as a panel for editing its label and required flag. Edits update a
// working copy so the row re-renders live; Save draft stores it, Publish
// releases it as the next version.
export default function FormEditorPage() {
    const { formSpecId } = useParams<{ formSpecId: string }>();
    const [searchParams] = useSearchParams();
    const { data: draft, isPending, error } = useDraft(formSpecId);
    const saveDraft = useSaveDraft(formSpecId);
    const publish = usePublishForm(formSpecId);

    // A form being created has no stored rows yet, so the draft load's 404 is
    // the expected answer; its title travels in the URL until the first save.
    const isNew = searchParams.get("new") === "1";
    const newTitle = searchParams.get("title")?.trim() || null;
    const creating = isNew && isNotFound(error);

    const editor = useWorkingSpec(formSpecId, draft, creating);
    const { workingSpec } = editor;
    const rows = useMemo(() => previewRows(workingSpec), [workingSpec]);
    const editableByKey = useMemo(
        () => indexEditable(workingSpec),
        [workingSpec],
    );

    const actions = useEditorActions({
        formSpecId,
        workingSpec,
        saveTitle: saveTitleFor(draft, creating, newTitle),
        isNew,
        creating,
        saveDraft,
        publish,
        onSaved: editor.markSaved,
    });

    const open = editorOpen(isPending, error, creating, workingSpec);

    return (
        <div className={homeStyles.page}>
            <nav aria-label="Breadcrumb">
                <Link to={paths.adminFormManagement}>← Form management</Link>
            </nav>

            <header className={adminStyles.heading}>
                <p className={adminStyles.eyebrow}>My Self Serve</p>
                <h1>{pageTitle(draft, creating, newTitle, formSpecId)}</h1>
            </header>

            <FormLoadStatus
                isPending={isPending}
                error={error}
                creating={creating}
            />

            {open && (
                <>
                    <ModeBar
                        mode={editor.mode}
                        onLabels={editor.showLabels}
                        onBuild={editor.showBuilder}
                    />

                    {editor.mode === "build" && editor.builderSeed && (
                        <div className={styles.builder}>
                            <SpecBuilder
                                seed={editor.builderSeed}
                                onReady={editor.captureBuilder}
                                onChange={editor.editInBuilder}
                            />
                        </div>
                    )}

                    {editor.mode === "labels" && (
                        <LabelsEditor
                            rows={rows}
                            editableByKey={editableByKey}
                            onLabel={editor.editLabel}
                            onRequired={editor.editRequired}
                        />
                    )}

                    {actions.shownErrors.length > 0 && (
                        <EditorErrorSummary errors={actions.shownErrors} />
                    )}

                    <SaveBar
                        dirty={editor.dirty}
                        lastAction={actions.lastAction}
                        saveDraft={saveDraft}
                        publish={publish}
                        hasClientErrors={actions.hasClientErrors}
                        warning={actions.saveWarning}
                        onSave={actions.handleSave}
                        onPublish={actions.handlePublish}
                    />

                    {formSpecId === ESTIMATOR_FORM_SPEC_ID && <RatesPanel />}
                </>
            )}
        </div>
    );
}
