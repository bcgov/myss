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
  announce(message: string): void;
  checkData(data?: unknown, flags?: unknown, row?: unknown): boolean;
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

function removalMessage(label: string, count: number, previous: number): string {
  return previous - count === 1
    ? `${label} ${previous} removed.`
    : `${label} ${count + 1} to ${previous} removed.`;
}

export class BcgovRepeatSectionComponent extends DataGridComponent {
  /** What the live region says once the section has drawn its new rows. */
  private pendingAnnouncement?: string;

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

  hasAddButton(): boolean {
    return false;
  }

  hasRemoveButtons(): boolean {
    return false;
  }

  checkData(data?: unknown, flags?: unknown, row?: unknown): boolean {
    this.matchRowsToCount(data);
    return super.checkData(data, flags, row);
  }

  /**
   * Adds or removes rows at the end until they number the count answer. A
   * count that is blank or out of range leaves the rows as they are, so
   * retyping the number keeps the answers already given.
   */
  private matchRowsToCount(data: unknown): void {
    if (this.builderMode || this.options.readOnly || !this.visible) return;
    const count = this.countAnswer(data);
    const rows = this.dataValue as Row[];
    if (count === undefined || count === rows.length) return;

    if (count < rows.length) {
      this.pendingAnnouncement = removalMessage(this.rowLabel, count, rows.length);
    }
    const next = rows.slice(0, count);
    while (next.length < count) next.push({});
    this.setValue(next);
  }

  private countAnswer(data: unknown): number | undefined {
    const key = textOf(this.properties.myssRepeatCount).trim();
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

    return renderBareWrapper.call(
      this,
      `<div>${rows}<span class="${styles.visuallyHidden}" ref="liveRegion" aria-live="polite"></span></div>`,
    );
  }

  attach(element: HTMLElement): Promise<void> {
    if (this.builderMode) return NestedArrayPrototype.attach.call(this, element);

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
      if (announcement) this.announce(announcement);
    });
  }
}
