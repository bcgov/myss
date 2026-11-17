// The component-tree walk shared by the editor helpers (specEdit) and the
// client-side rules (specRules). Form.io nests components inside panels,
// columns, fieldsets and table cells, so a top-level scan would miss most of a
// real form. No fetch, no React.

export function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

type Visit = (component: Record<string, unknown>) => void;

/** Visit every component in `spec`, in document order. */
export function forEachComponent(spec: unknown, visit: Visit): void {
  if (isRecord(spec)) walkComponents(spec.components, visit);
}

// One function per container shape. Each hands the children it finds back to
// walkComponents, so the recursion stays the same and each shape reads alone.
function walkComponents(nodes: unknown, visit: Visit): void {
  if (!Array.isArray(nodes)) return;
  for (const node of nodes) {
    if (!isRecord(node)) continue;
    visit(node);
    walkComponents(node.components, visit);
    walkColumns(node.columns, visit);
    walkRows(node.rows, visit);
  }
}

/** `columns` (columns layout): each column carries its own `components`. */
function walkColumns(columns: unknown, visit: Visit): void {
  if (!Array.isArray(columns)) return;
  for (const column of columns) {
    if (isRecord(column)) walkComponents(column.components, visit);
  }
}

/** `rows` (table layout): a row is an array of cells, each with `components`. */
function walkRows(rows: unknown, visit: Visit): void {
  if (!Array.isArray(rows)) return;
  for (const row of rows) {
    if (!Array.isArray(row)) continue;
    for (const cell of row) {
      if (isRecord(cell)) walkComponents(cell.components, visit);
    }
  }
}
