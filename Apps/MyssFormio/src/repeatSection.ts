// bcgovRepeatSection: a group of questions asked once per unit of a number
// question elsewhere on the form ("How many children?" 3 shows three groups).
// Form.io's data grid keeps the answers, one object per row under the
// section's key, and checks each row's fields; this class sets the number of
// rows from the count answer instead of add and remove buttons, and draws each
// row as a headed group of stacked questions instead of a table row. In the
// builder it draws, and takes dropped questions, like a panel: its heading
// once, then its questions one under another.

import {
  renderBareWrapper,
  stockComponent,
  textOf,
  type FormioFieldInstance,
} from "./bcgovField";
import { escapeHtml } from "./html";
import { isRecord } from "./specWalk";
import styles from "./repeatSection.module.css";

/** The most rows a section takes when its spec sets no maximum. */
const DEFAULT_MAX_ROWS = 10;

/** The highest maximum a section may set; the publish rule refuses more. */
const MAX_ROWS_CEILING = 50;

const REPEAT_SECTION_TYPE = "bcgovRepeatSection";

type Row = Record<string, unknown>;

/** Form.io's data grid, with the members this class uses. */
interface DataGridInstance extends FormioFieldInstance {
  type: string;
  rows: Record<string, FormioFieldInstance>[];
  builderMode: boolean;
  datagridKey: string;
  nestedKey: string;
  renderComponents(components?: FormioFieldInstance[]): string;
  attachComponents(
    element: HTMLElement | undefined,
    components: FormioFieldInstance[],
    container?: unknown,
  ): Promise<unknown>;
  getComponentsContainer(): unknown;
  checkData(data?: unknown, flags?: unknown, row?: unknown): boolean;
  init(): void;
  hasCondition(): boolean;
  conditionallyVisible(data?: unknown, row?: unknown): boolean;
}

/** A Form.io component or container whose own conditions the section reads. */
interface ConditionalNode {
  parent?: ConditionalNode | null;
  hasCondition(): boolean;
  conditionallyVisible(data?: unknown, row?: unknown): boolean;
}

/** The count question, as the section reads and adjusts it. */
interface CountQuestion extends ConditionalNode {
  component: Record<string, unknown>;
  redraw(): unknown;
}

// eslint-disable-next-line @typescript-eslint/no-explicit-any
type DataGridCtor = (new (...args: any[]) => DataGridInstance) & {
  schema(...extend: unknown[]): Record<string, unknown>;
};

const DataGridComponent = stockComponent("datagrid") as unknown as DataGridCtor;

/** The data grid's parent, whose drawing, attach and builder handling this class keeps. */
const NestedArrayPrototype = Object.getPrototypeOf(
  DataGridComponent.prototype,
) as DataGridInstance;

/** A whole number from 1 to the ceiling, as a JSON number or as digits. */
function readMaxRows(value: unknown): number {
  const text = textOf(value).trim();
  const max = /^\d+$/.test(text) ? Number(text) : Number.NaN;
  return max >= 1 ? Math.min(max, MAX_ROWS_CEILING) : DEFAULT_MAX_ROWS;
}

/** A numeric limit as a spec stores it, a number or digits; undefined when blank. */
function limitOf(value: unknown): number | undefined {
  const text = textOf(value).trim();
  const limit = text === "" ? Number.NaN : Number(text);
  return Number.isFinite(limit) ? limit : undefined;
}

function removalMessage(label: string, count: number, previous: number): string {
  return previous - count === 1
    ? `${label} ${previous} removed.`
    : `${label} ${count + 1} to ${previous} removed.`;
}

let liveRegion: HTMLElement | undefined;

/**
 * One polite live region for every section, outside their markup: a region
 * drawn afresh with the rows may be new to a screen reader when it speaks.
 */
function sharedLiveRegion(): HTMLElement {
  if (!liveRegion?.isConnected) {
    liveRegion = document.createElement("div");
    liveRegion.setAttribute("role", "status");
    liveRegion.className = styles.visuallyHidden;
    document.body.append(liveRegion);
  }
  return liveRegion;
}

function announce(message: string): void {
  const region = sharedLiveRegion();
  region.textContent = "";
  setTimeout(() => {
    region.textContent = message;
  }, 100);
}

export class BcgovRepeatSectionComponent extends DataGridComponent {
  /** What the live region says once the section has drawn its new rows. */
  private pendingAnnouncement?: string;

  /** Rows a lower count took off, put back in order if the count rises again. */
  private removedRows: Row[] = [];

  /** How many rows the section last left, to tell its own changes from others'. */
  private syncedRows?: number;

  constructor(...args: unknown[]) {
    super(...args);
    // Form.io's builder drops questions into a data grid as table columns;
    // under its own type the section takes them as a panel does.
    if (this.builderMode) this.type = REPEAT_SECTION_TYPE;
  }

  static schema(...extend: unknown[]): Record<string, unknown> {
    return DataGridComponent.schema(
      {
        type: REPEAT_SECTION_TYPE,
        key: "repeatSection",
        label: "Repeat section",
        initEmpty: true,
        disableAddingRemovingRows: true,
        properties: {
          myssRepeatCount: "",
          myssRepeatMax: DEFAULT_MAX_ROWS,
          myssRowLabel: "",
        },
      },
      ...extend,
    );
  }

  static get builderInfo() {
    return {
      title: "Repeat section",
      group: "data",
      icon: "clone",
      showPreview: false,
      schema: BcgovRepeatSectionComponent.schema(),
    };
  }

  get defaultSchema(): Record<string, unknown> {
    return BcgovRepeatSectionComponent.schema();
  }

  private get properties(): Record<string, unknown> {
    return isRecord(this.component.properties) ? this.component.properties : {};
  }

  private get maxRows(): number {
    return readMaxRows(this.properties.myssRepeatMax);
  }

  private get rowLabel(): string {
    return (
      textOf(this.properties.myssRowLabel).trim() ||
      textOf(this.component.label).trim()
    );
  }

  private get countKey(): string {
    return textOf(this.properties.myssRepeatCount).trim();
  }

  hasAddButton(): boolean {
    return false;
  }

  hasRemoveButtons(): boolean {
    return false;
  }

  init(): void {
    super.init();
    this.applyCountRules();
  }

  hasCondition(): boolean {
    return super.hasCondition() || (!this.builderMode && this.countKey !== "");
  }

  /** The section shows only while its count question can be seen. */
  conditionallyVisible(data?: unknown, row?: unknown): boolean {
    return super.conditionallyVisible(data, row) && this.countQuestionShown(data);
  }

  private countQuestion(): CountQuestion | undefined {
    const root = this.root as { getComponent?(path: string): unknown } | null | undefined;
    const found = this.countKey ? root?.getComponent?.(this.countKey) : undefined;
    return found && found !== this ? (found as CountQuestion) : undefined;
  }

  /**
   * Whether the count question and every container above it pass their
   * conditions on these answers, read afresh: a container drawn after the
   * section has not yet updated its own visibility in this pass.
   */
  private countQuestionShown(data: unknown): boolean {
    let node: ConditionalNode | null | undefined = this.builderMode
      ? undefined
      : this.countQuestion();
    while (node) {
      if (node.hasCondition() && !node.conditionallyVisible(data)) return false;
      node = node.parent;
    }
    return true;
  }

  /**
   * Holds the count question to what the section can draw, as the server
   * does: an answer is required, with no decimals, from 1 to the maximum or
   * the question's own narrower limits.
   */
  private applyCountRules(redraw = false): void {
    const count = this.builderMode ? undefined : this.countQuestion();
    if (!count) return;

    const validate = isRecord(count.component.validate) ? count.component.validate : {};
    const ownMin = limitOf(validate.min);
    const ownMax = limitOf(validate.max);
    const min = ownMin !== undefined && ownMin > 1 ? ownMin : 1;
    const max = ownMax !== undefined && ownMax < this.maxRows ? ownMax : this.maxRows;
    const applied =
      validate.required === true &&
      validate.min === min &&
      validate.max === max &&
      count.component.decimalLimit === 0;
    if (applied) return;

    count.component.validate = { ...validate, required: true, min, max };
    count.component.decimalLimit = 0;
    if (redraw) count.redraw();
  }

  /** Matches the rows once Form.io has settled the section's visibility for these answers. */
  checkData(data?: unknown, flags?: unknown, row?: unknown): boolean {
    const valid = super.checkData(data, flags, row);
    this.matchRowsToCount(data);
    return valid;
  }

  /**
   * Adds or removes rows at the end until they number the count answer. A
   * count that is blank or out of range leaves the rows as they are, so
   * retyping the number keeps the answers already given, and rows a lower
   * count took off come back if it rises again while the section shows.
   */
  private matchRowsToCount(data: unknown): void {
    if (this.builderMode || this.options.readOnly) return;
    const rows = this.dataValue as Row[];
    if (!this.visible || rows.length !== this.syncedRows) this.removedRows = [];
    this.syncedRows = rows.length;
    if (!this.visible) return;

    const count = this.countAnswer(data);
    if (count === undefined || count === rows.length) return;
    const next = count < rows.length ? this.removeRows(rows, count) : this.restoreRows(rows, count);
    this.syncedRows = next.length;
    this.setValue(next);
  }

  private removeRows(rows: Row[], count: number): Row[] {
    this.pendingAnnouncement = removalMessage(this.rowLabel, count, rows.length);
    this.removedRows = [...rows.slice(count), ...this.removedRows];
    return rows.slice(0, count);
  }

  private restoreRows(rows: Row[], count: number): Row[] {
    const restored = this.removedRows.slice(0, count - rows.length);
    this.removedRows = this.removedRows.slice(restored.length);
    const next = [...rows, ...restored];
    while (next.length < count) next.push({});
    return next;
  }

  private countAnswer(data: unknown): number | undefined {
    const key = this.countKey;
    const answers = isRecord(data) ? data : this.rootValue;
    const value = key ? answers?.[key] : undefined;
    return typeof value === "number" &&
      Number.isInteger(value) &&
      value >= 1 &&
      value <= this.maxRows
      ? value
      : undefined;
  }

  render(): string {
    if (this.builderMode) {
      return NestedArrayPrototype.render.call(
        this,
        `<p class="${styles.legend}">${escapeHtml(this.rowLabel)}</p>` +
          `<div ref="${this.nestedKey}">${this.renderComponents()}</div>`,
      );
    }

    const label = escapeHtml(this.rowLabel);
    const total = this.rows.length;
    const rows = this.rows
      .map(
        (row, index) =>
          `<fieldset class="${styles.row}">` +
          `<legend class="${styles.legend}">${label}` +
          `<span class="${styles.visuallyHidden}"> ${index + 1} of ${total}</span></legend>` +
          `<div ref="${this.datagridKey}-row">` +
          Object.values(row)
            .map((component) => component.render())
            .join("") +
          `</div></fieldset>`,
      )
      .join("");

    return renderBareWrapper.call(this, `<div>${rows}</div>`);
  }

  attach(element: HTMLElement): Promise<void> {
    if (this.builderMode) return NestedArrayPrototype.attach.call(this, element);

    this.applyCountRules(true);
    sharedLiveRegion();
    this.loadRefs(element, { [`${this.datagridKey}-row`]: "multiple" });
    const bodies = this.refs[`${this.datagridKey}-row`];
    const rowBodies = bodies instanceof NodeList ? Array.from(bodies) : [];
    const rowsAttached = this.rows.map((row, index) =>
      this.attachComponents(
        rowBodies[index],
        Object.values(row),
        this.getComponentsContainer(),
      ),
    );

    const announcement = this.pendingAnnouncement;
    this.pendingAnnouncement = undefined;
    return Promise.all([
      NestedArrayPrototype.attach.call(this, element),
      ...rowsAttached,
    ]).then(() => {
      if (announcement) announce(announcement);
    });
  }
}
