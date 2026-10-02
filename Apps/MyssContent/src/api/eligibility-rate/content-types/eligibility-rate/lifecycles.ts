/**
 * Validation for eligibility-rate entries written through the admin panel,
 * REST or the Document Service (bulk and raw database writes bypass
 * lifecycles). The rules live in `src/lib/eligibility-rate-rules.ts`; this
 * hook loads the rows that share the effective date and turns any violation
 * into an `ApplicationError`, which the admin panel shows and REST returns as
 * a 400.
 */

import { errors } from "@strapi/utils";

import {
  evaluateRateCreate,
  evaluateRateUpdate,
  formatViolations,
  type IncomingRateTable,
  type RateTableRow,
  type Violation,
} from "../../../../lib/eligibility-rate-rules";

const { ApplicationError } = errors;

const ELIGIBILITY_RATE_UID = "api::eligibility-rate.eligibility-rate";

/**
 * Minimal structural view of the Strapi global. Declared locally so this file
 * does not depend on the generated global types being present at compile time.
 */
declare const strapi: {
  db: {
    query(uid: string): {
      findMany(params: {
        where: Record<string, unknown>;
        select: string[];
      }): Promise<RateTableRow[]>;
      findOne(params: {
        where: Record<string, unknown>;
        select: string[];
      }): Promise<RateTableRow | null>;
    };
  };
};

interface LifecycleEvent {
  params?: { data?: IncomingRateTable; where?: Record<string, unknown> };
}

async function loadSameDate(effectiveDate: unknown): Promise<RateTableRow[]> {
  if (typeof effectiveDate !== "string" || effectiveDate === "") return [];
  return strapi.db
    .query(ELIGIBILITY_RATE_UID)
    .findMany({ where: { effectiveDate }, select: ["documentId"] });
}

// Admin-panel updates carry the documentId in `data`; REST and Document
// Service updates do not, so it is read from the row being updated.
async function documentIdOf(event: LifecycleEvent): Promise<string | null | undefined> {
  const data = event.params?.data;
  if (data?.documentId) return data.documentId;
  const where = event.params?.where;
  if (!where) return undefined;
  const row = await strapi.db
    .query(ELIGIBILITY_RATE_UID)
    .findOne({ where, select: ["documentId"] });
  return row?.documentId;
}

function reject(violations: Violation[]): void {
  if (violations.length === 0) return;
  throw new ApplicationError(formatViolations(violations), {
    keywords: violations.map((violation) => violation.keyword),
  });
}

export default {
  async beforeCreate(event: LifecycleEvent) {
    const data = event.params?.data;
    if (!data) return;
    reject(evaluateRateCreate(data, await loadSameDate(data.effectiveDate)));
  },

  async beforeUpdate(event: LifecycleEvent) {
    const data = event.params?.data;
    if (!data) return;
    const sameDateRows = await loadSameDate(data.effectiveDate);
    const documentId = sameDateRows.length > 0 ? await documentIdOf(event) : data.documentId;
    reject(evaluateRateUpdate({ ...data, documentId }, sameDateRows));
  },
};
