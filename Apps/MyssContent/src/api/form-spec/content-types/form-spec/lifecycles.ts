/**
 * Phase 0.3 — publish-time validation for form specs (§8.2 rules a–e).
 *
 * This file is deliberately thin. It does one database query, delegates every
 * decision to the pure functions in `src/lib/form-spec-rules.ts`, and turns
 * any violation into an `ApplicationError` so Strapi shows the message in the
 * admin panel instead of a 500. All the reasoning — and all the tests — live
 * in that module; there is nothing here worth unit-testing that would not
 * require booting Strapi.
 *
 * The rules it enforces depend on Strapi 5 event behaviour that was MEASURED
 * on a running instance (2026-08-17) rather than taken from the docs. If you
 * are about to change the create/update branching, read the header of
 * `form-spec-rules.ts` first — the three observations recorded there are the
 * difference between a working hook and one that blocks publishing or fails
 * the boot on a fresh database.
 */

import { errors } from "@strapi/utils";

import {
  evaluateCreate,
  evaluateUpdate,
  formatViolations,
  type FormSpecRow,
  type IncomingFormSpec,
  type Violation,
} from "../../../../lib/form-spec-rules";

const { ApplicationError } = errors;

const FORM_SPEC_UID = "api::form-spec.form-spec";

/**
 * Minimal structural view of the Strapi global. Declared locally so this file
 * does not depend on the generated global types being present at compile time.
 */
declare const strapi: {
  db: {
    query(uid: string): {
      findMany(params: { where: Record<string, unknown> }): Promise<FormSpecRow[]>;
    };
  };
  log: { warn(message: string): void };
};

interface LifecycleEvent {
  params?: { data?: IncomingFormSpec };
}

/**
 * Every row sharing this `formSpecId`, drafts and published alike. The rules
 * need both: the version sequence counts all versions, and the immutability
 * check looks for a published row with the same `documentId`.
 */
async function loadSiblings(formSpecId: unknown): Promise<FormSpecRow[]> {
  if (typeof formSpecId !== "string" || formSpecId === "") return [];
  return strapi.db.query(FORM_SPEC_UID).findMany({ where: { formSpecId } });
}

function reject(violations: Violation[]): never | void {
  if (violations.length === 0) return;
  throw new ApplicationError(formatViolations(violations), {
    keywords: violations.map((violation) => violation.keyword),
  });
}

export default {
  async beforeCreate(event: LifecycleEvent) {
    const data = event.params?.data;
    if (!data) return;
    reject(evaluateCreate(data, await loadSiblings(data.formSpecId)));
  },

  async beforeUpdate(event: LifecycleEvent) {
    const data = event.params?.data;
    if (!data) return;
    reject(evaluateUpdate(data, await loadSiblings(data.formSpecId)));
  },

  /**
   * Deleting a published form spec breaks historical rendering of every
   * submission made against it — `FormsService.GetSubmissionAsync` already
   * logs a warning when an archived spec has gone missing. `beforeDelete`
   * carries no data (MEASURED: only `where: { id }`), so refusing the delete
   * properly means a cross-database check against submissions, which live in
   * the MyssApi schema rather than Strapi's. That is candidate rule (f) and is
   * not implemented here. Warn loudly rather than pretend it is handled.
   */
  beforeDelete() {
    strapi.log.warn(
      "A form-spec entry is being deleted. If it was published, submissions " +
        "referencing that version will no longer render their original form.",
    );
  },
};
