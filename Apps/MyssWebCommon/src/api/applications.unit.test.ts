import { describe, expect, it } from "vitest";

import {
  ApplicationRequestError,
  readProblem,
  throwApplicationRequestError,
} from "./applications";

function problemResponse(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/problem+json" },
  });
}

describe("readProblem", () => {
  it("reads the keyword, current version and detail", async () => {
    const problem = await readProblem(
      problemResponse(409, {
        keyword: "PLATFORM.EVENTSTORE.CONFLICT",
        currentVersion: 4,
        detail: "Someone else changed it.",
      }),
    );

    expect(problem).toEqual({
      keyword: "PLATFORM.EVENTSTORE.CONFLICT",
      currentVersion: 4,
      detail: "Someone else changed it.",
    });
  });

  it("drops fields of the wrong type", async () => {
    const problem = await readProblem(
      problemResponse(409, { keyword: 7, currentVersion: "4", detail: null }),
    );

    expect(problem).toEqual({
      keyword: undefined,
      currentVersion: undefined,
      detail: undefined,
    });
  });

  it("tolerates a body that is not JSON", async () => {
    const problem = await readProblem(
      new Response("<html>Bad gateway</html>", { status: 502 }),
    );

    expect(problem).toEqual({});
  });
});

describe("throwApplicationRequestError", () => {
  it("throws the status and the problem's fields", async () => {
    const error = await throwApplicationRequestError(
      problemResponse(409, {
        keyword: "INTAKE.REVIEW.NOT_ALLOWED",
        currentVersion: 2,
      }),
    ).catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApplicationRequestError);
    expect(error).toMatchObject({
      status: 409,
      keyword: "INTAKE.REVIEW.NOT_ALLOWED",
      currentVersion: 2,
      isConflict: true,
    });
  });

  it("words the error from the status when there is no detail", async () => {
    const error = await throwApplicationRequestError(
      new Response("", { status: 500 }),
    ).catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApplicationRequestError);
    expect((error as Error).message).toBe("Application request failed (500)");
    expect((error as ApplicationRequestError).isConflict).toBe(false);
  });
});
