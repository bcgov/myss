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
import type { FormValidationError } from "@/api/forms";
import { registerBcgovComponents } from "@/formio/bcgovComponents";
import { builderOptions } from "@/formio/builderOptions";
import { revealAllComponents } from "@/formio/specPreview";
import {
    flattenComponents,
    listEditableComponents,
    setLabel,
    setRequired,
    type EditableComponent,
} from "@/formio/specEdit";
import {
    findDuplicateKeys,
    findUnknownConditionalTargets,
} from "@/formio/specRules";
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

/** The builder handle `onBuilderReady` returns. */
interface BuilderHandle {
    instance?: { schema?: FormType };
}

function sameSpec(a: FormType | null, b: FormType | null): boolean {
    return Utils._.isEqual(a, b);
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
    const target =
        document.querySelector<HTMLElement>(
            `input[aria-label="Label for ${escaped}"]`,
        ) ??
        document.querySelector<HTMLElement>(
            `[class~=${JSON.stringify(`formio-component-${field}`)}]`,
        );
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
    function PreviewCell({ component }: { component: Record<string, unknown> }) {
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
function EditorErrorSummary({ errors }: { errors: EditorError[] }) {
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

// Opens one form: each field is rendered read-only (the real BC Gov skin) in the
// same row as a panel for editing its label and required flag. Edits update a
// working copy so the row re-renders live; Save draft stores it, Publish
// releases it as the next version.
export default function FormEditorPage() {
    const { formSpecId } = useParams<{ formSpecId: string }>();
    const [searchParams] = useSearchParams();
    const navigate = useNavigate();
    const { data: draft, isPending, error } = useDraft(formSpecId);
    const saveDraft = useSaveDraft(formSpecId);
    const publish = usePublishForm(formSpecId);

    // A form being created has no stored rows yet, so the draft load's 404 is
    // the expected answer; its title travels in the URL until the first save.
    const isNew = searchParams.get("new") === "1";
    const newTitle = searchParams.get("title")?.trim() || null;
    const creating =
        isNew && error instanceof FormLoadError && error.status === 404;

    // The edited copy the panel drives and Save draft/Publish send. It is seeded
    // from the loaded draft once per form (keyed on formSpecId) by adjusting
    // state during render — the sanctioned alternative to seeding in an effect.
    // Keying on the form, not the draft object, means a background refetch (e.g.
    // after a save) never re-seeds and discards in-progress edits; only opening a
    // different form does. The loaded draft stays untouched; the setters return a
    // new spec each edit.
    const [workingSpec, setWorkingSpec] = useState<FormType | null>(null);
    const [seededFor, setSeededFor] = useState<string | undefined>(undefined);
    const [dirty, setDirty] = useState(false);
    const [mode, setMode] = useState<EditorMode>("labels");
    // The spec the builder was constructed from, set only when the builder is
    // opened: changing it rebuilds the builder and drops any open dialog.
    const [builderSeed, setBuilderSeed] = useState<FormType | null>(null);
    const builderInstance = useRef<BuilderHandle | null>(null);
    // Mirrors workingSpec for the builder's change handler, which has to stay
    // referentially stable or the builder is rebuilt on every render.
    const latestSpec = useRef<FormType | null>(null);
    useEffect(() => {
        latestSpec.current = workingSpec;
    }, [workingSpec]);
    // Whether the working copy is the local new-form template rather than a
    // stored draft. Cleared by the first save; while set, a draft arriving from
    // the server outranks the template (see below).
    const [seededFromTemplate, setSeededFromTemplate] = useState(false);
    if (seededFor !== formSpecId) {
        if (draft) {
            setSeededFor(formSpecId);
            setWorkingSpec(draft.spec);
            setDirty(false);
            setMode("labels");
            setBuilderSeed(null);
            setSeededFromTemplate(false);
        } else if (creating) {
            // Nothing stored yet: start from the local template, in the
            // builder, since a new form is about adding fields.
            const template = structuredClone(NEW_FORM_TEMPLATE);
            setSeededFor(formSpecId);
            setWorkingSpec(template);
            setDirty(false);
            setMode("build");
            setBuilderSeed(template);
            setSeededFromTemplate(true);
        } else if (workingSpec !== null) {
            setWorkingSpec(null);
        }
    } else if (seededFromTemplate && draft && !dirty) {
        // The form turned out to exist after the template seeded — a stale 404
        // in the cache, or another admin created the same ID. Show the stored
        // draft rather than let a save overwrite it with the template.
        setSeededFromTemplate(false);
        setWorkingSpec(draft.spec);
        setMode("labels");
        setBuilderSeed(null);
    }

    // Preview shows every component at once (conditionals stripped), flattened to
    // one field per row so each lines up with its settings.
    const rows = useMemo(
        () =>
            workingSpec
                ? flattenComponents(revealAllComponents(workingSpec))
                : [],
        [workingSpec],
    );
    const editableByKey = useMemo(() => {
        const map = new Map<string, EditableComponent>();
        if (workingSpec) {
            for (const component of listEditableComponents(workingSpec)) {
                map.set(component.key, component);
            }
        }
        return map;
    }, [workingSpec]);

    const [clientErrors, setClientErrors] = useState<EditorError[]>([]);
    const [lastAction, setLastAction] = useState<"save" | "publish" | null>(
        null,
    );

    // A server refusal (422) from save or publish, surfaced in the summary;
    // client-side rule errors take precedence when present.
    // The error for the current action: a publish runs save then publish, so it
    // can come from either; a save only from save. Scoped by lastAction so a
    // stale error from a previous action doesn't linger.
    const activeError =
        lastAction === "publish"
            ? (publish.error ?? saveDraft.error)
            : lastAction === "save"
              ? saveDraft.error
              : null;
    // A 422 carries a field-error list; any other refusal (a 502 from the
    // content engine, a network drop) carries none, so fall back to a plain
    // message with the status rather than failing silently.
    const rejection =
        activeError instanceof SpecRejectedError ? activeError : null;
    const serverErrors: EditorError[] = rejection
        ? rejection.errors.map((e: FormValidationError) => ({
              message: e.message,
              field: e.field,
          }))
        : [];
    const shownErrors: EditorError[] = clientErrors.length
        ? clientErrors
        : serverErrors.length
          ? serverErrors
          : activeError
            ? [
                  {
                      message: `Could not ${
                          lastAction === "publish" ? "publish" : "save"
                      } this form${
                          rejection ? ` (error ${rejection.status})` : ""
                      }. Please try again.`,
                  },
              ]
            : [];
    const busy = saveDraft.isPending || publish.isPending;

    function editLabel(key: string, value: string) {
        setWorkingSpec((s) => (s ? setLabel(s, key, value) : s));
        setDirty(true);
    }

    function editRequired(key: string, required: boolean) {
        setWorkingSpec((s) => (s ? setRequired(s, key, required) : s));
        setDirty(true);
    }

    const captureBuilder = useCallback((builder: BuilderHandle) => {
        builderInstance.current = builder;
    }, []);

    // Read the builder's schema, not the form its onChange hands over: the form
    // carries every Form.io default plus a regenerated id per component, so
    // saving it would rewrite the stored spec on the first interaction. The
    // builder also reports a change when a dialog is merely opened or
    // cancelled, hence the no-op check.
    const editInBuilder = useCallback(() => {
        const next = builderInstance.current?.instance?.schema;
        if (!next || sameSpec(latestSpec.current, next)) return;
        latestSpec.current = next;
        setWorkingSpec(next);
        setDirty(true);
    }, []);

    // Re-seed from the working copy so edits made in the other mode carry over;
    // without this the builder would load the spec as fetched and its first
    // change would overwrite them. Re-selecting the mode it is already in must
    // not re-seed, which would rebuild the builder and drop an open dialog.
    function showBuilder() {
        if (mode === "build") return;
        setBuilderSeed(workingSpec);
        setMode("build");
    }

    // A stored draft's title wins; only a form still being created takes the
    // one from the URL, so a `title` param cannot rename an existing form.
    const saveTitle = draft?.title ?? (creating ? newTitle : null);
    const [saveWarning, setSaveWarning] = useState<string | null>(null);

    // After the first save of a new form, drop the new-form flag from the URL
    // and history, so revisiting this page cannot seed the template over the
    // stored draft. A version other than 1 means the ID was already taken and
    // the save landed on that form — say so instead of a quiet overwrite.
    function finishCreate(savedVersion: number) {
        if (!isNew || !formSpecId) return;
        setSeededFromTemplate(false);
        setSaveWarning(
            creating && savedVersion !== 1
                ? `A form with this ID already existed — this save created draft version ${savedVersion} of it.`
                : null,
        );
        navigate(adminFormEditorPath(formSpecId), { replace: true });
    }

    function handleSave() {
        if (!workingSpec) return;
        setLastAction("save");
        setSaveWarning(null);
        const errs = ruleErrors(workingSpec);
        setClientErrors(errs);
        if (errs.length) return;
        saveDraft.mutate(
            { spec: workingSpec, title: saveTitle },
            {
                onSuccess: (saved) => {
                    setDirty(false);
                    finishCreate(saved.version);
                },
            },
        );
    }

    async function handlePublish() {
        if (!workingSpec) return;
        setLastAction("publish");
        setSaveWarning(null);
        const errs = ruleErrors(workingSpec);
        setClientErrors(errs);
        if (errs.length) return;
        try {
            // Persist the working copy, then publish that draft as the next
            // version. A 422 from either call surfaces via its mutation error.
            const saved = await saveDraft.mutateAsync({
                spec: workingSpec,
                title: saveTitle,
            });
            await publish.mutateAsync();
            setDirty(false);
            finishCreate(saved.version);
        } catch {
            // Handled through the error summary; the working copy is untouched.
        }
    }

    return (
        <div className={homeStyles.page}>
            <nav aria-label="Breadcrumb">
                <Link to={paths.adminFormManagement}>← Form management</Link>
            </nav>

            <header className={adminStyles.heading}>
                <p className={adminStyles.eyebrow}>My Self Serve</p>
                <h1>
                    {draft?.title?.trim()
                        ? draft.title
                        : (creating && newTitle) || formSpecId}
                </h1>
            </header>

            {isPending && <p>Loading form…</p>}

            {!isPending && error && !creating && (
                <p role="alert">
                    {error instanceof FormLoadError && error.status === 404
                        ? "This form could not be found — check the address."
                        : `Could not load this form: ${error.message}`}
                </p>
            )}

            {!isPending && (!error || creating) && workingSpec && (
                <>
                    <div
                        className={styles.modeBar}
                        role="group"
                        aria-label="Editing mode"
                    >
                        <button
                            type="button"
                            className={styles.modeButton}
                            aria-pressed={mode === "labels"}
                            onClick={() => setMode("labels")}
                        >
                            Edit labels
                        </button>
                        <button
                            type="button"
                            className={styles.modeButton}
                            aria-pressed={mode === "build"}
                            onClick={showBuilder}
                        >
                            Build form
                        </button>
                    </div>

                    {mode === "build" && builderSeed && (
                        <div className={styles.builder}>
                            <SpecBuilder
                                seed={builderSeed}
                                onReady={captureBuilder}
                                onChange={editInBuilder}
                            />
                        </div>
                    )}

                    {mode === "labels" && (
                        <div className={styles.grid}>
                            <div className={styles.headCell}>Preview</div>
                            <div className={styles.headCell}>Field settings</div>

                            {rows.map((component, index) => {
                                const key =
                                    typeof component.key === "string"
                                        ? component.key
                                        : `row-${index}`;
                                const editable = editableByKey.get(key);
                                return (
                                    <Fragment key={key}>
                                        <div className={styles.previewCell}>
                                            <PreviewCell component={component} />
                                        </div>
                                        <div className={styles.editCell}>
                                            {editable ? (
                                                <div className={styles.editControls}>
                                                    <code className={styles.key}>
                                                        {editable.key}
                                                    </code>
                                                    <label
                                                        className={styles.labelField}
                                                    >
                                                        <span
                                                            className={
                                                                styles.fieldLabel
                                                            }
                                                        >
                                                            Label
                                                        </span>
                                                        <input
                                                            type="text"
                                                            value={editable.label}
                                                            aria-label={`Label for ${editable.key}`}
                                                            onChange={(e) =>
                                                                editLabel(
                                                                    editable.key,
                                                                    e.target.value,
                                                                )
                                                            }
                                                        />
                                                    </label>
                                                    <label
                                                        className={
                                                            styles.requiredField
                                                        }
                                                    >
                                                        <input
                                                            type="checkbox"
                                                            checked={
                                                                editable.required
                                                            }
                                                            aria-label={`Required: ${editable.key}`}
                                                            onChange={(e) =>
                                                                editRequired(
                                                                    editable.key,
                                                                    e.target.checked,
                                                                )
                                                            }
                                                        />
                                                        Required
                                                    </label>
                                                </div>
                                            ) : (
                                                <span
                                                    className={styles.noEdit}
                                                    aria-hidden="true"
                                                >
                                                    —
                                                </span>
                                            )}
                                        </div>
                                    </Fragment>
                                );
                            })}
                        </div>
                    )}

                    {shownErrors.length > 0 && (
                        <EditorErrorSummary errors={shownErrors} />
                    )}

                    <div className={styles.saveBar}>
                        <button
                            type="button"
                            className={styles.saveButton}
                            onClick={handleSave}
                            disabled={busy}
                        >
                            {saveDraft.isPending && lastAction === "save"
                                ? "Saving…"
                                : "Save draft"}
                        </button>
                        <button
                            type="button"
                            className={styles.publishButton}
                            onClick={handlePublish}
                            disabled={busy}
                        >
                            {busy && lastAction === "publish"
                                ? "Publishing…"
                                : "Publish"}
                        </button>
                        {lastAction === "save" &&
                            saveDraft.isSuccess &&
                            !dirty &&
                            clientErrors.length === 0 && (
                                <span role="status" className={styles.saved}>
                                    Draft saved.
                                </span>
                            )}
                        {lastAction === "publish" &&
                            publish.isSuccess &&
                            !dirty &&
                            publish.data && (
                                <span role="status" className={styles.saved}>
                                    Published version {publish.data.version}.
                                </span>
                            )}
                        {saveWarning && (
                            <span role="alert" className={styles.warning}>
                                {saveWarning}
                            </span>
                        )}
                    </div>

                    {formSpecId === ESTIMATOR_FORM_SPEC_ID && <RatesPanel />}
                </>
            )}
        </div>
    );
}
